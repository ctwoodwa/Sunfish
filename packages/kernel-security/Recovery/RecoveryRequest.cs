using System.Text;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Kernel.Security.Recovery;

/// <summary>
/// Phase 1 G6 sub-pattern <b>#48a (multi-sig social)</b> per ADR 0046 — the
/// signed request a new device sends to its designated trustees asking
/// them to attest to the device's identity so the user can re-derive
/// their team keys without the original keystore.
/// </summary>
/// <remarks>
/// <para>
/// The new device generates a fresh ephemeral Ed25519 keypair, signs this
/// request with the ephemeral private key, and broadcasts the request to
/// the user's previously-designated trustees (per the trustee-designation
/// flow that ships alongside this type). Trustees verify the signature,
/// inspect the requesting device's claimed identity (NodeId), and respond
/// with a <see cref="TrusteeAttestation"/> if they recognize the request
/// as legitimate.
/// </para>
/// <para>
/// The 7-day grace period (sub-pattern #48e) starts when quorum (3 of 5
/// trustees, per ADR 0046) is reached. During the grace window the
/// original device — if it still has its keys — can dispute the request
/// and abort the recovery. After the window expires without dispute, the
/// new device finalizes recovery and writes a <see cref="RecoveryEvent"/>
/// to the per-tenant audit log (sub-pattern #48f).
/// </para>
/// <para>
/// <b>What this type is NOT.</b> This is a portable signed message
/// envelope; it does not orchestrate trustee selection, quorum counting,
/// grace-timer state, or key reissue. Those live in the
/// <c>RecoveryCoordinator</c> implementation that ships in a follow-up
/// (the substrate here is intentionally narrow so the wire-format types
/// land independently of the workflow that uses them).
/// </para>
/// </remarks>
public sealed record RecoveryRequest(
    string RequestingNodeId,
    byte[] EphemeralPublicKey,
    DateTimeOffset RequestedAt,
    byte[] Signature)
{
    /// <summary>
    /// Length in bytes of an Ed25519 ephemeral public key as produced by
    /// <see cref="IEd25519Signer.GenerateKeyPair"/>.
    /// </summary>
    public const int EphemeralPublicKeyLength = 32;

    /// <summary>
    /// Length in bytes of an Ed25519 signature.
    /// </summary>
    public const int SignatureLength = 64;

    /// <summary>
    /// Produce the canonical byte sequence over which the request
    /// signature is computed: <c>"sunfish-recovery-request-v1\n" || NodeId || PubKey || RequestedAt(ISO-8601 UTC)</c>.
    /// Exposed so trustees can re-derive the signed bytes for verification.
    /// </summary>
    /// <remarks>
    /// The domain-separation prefix prevents signature reuse across other
    /// Sunfish protocol messages (HELLO, GOSSIP_PING, attestations) that
    /// also sign over a NodeId + timestamp tuple.
    /// </remarks>
    public static byte[] CanonicalBytesForSigning(
        string requestingNodeId,
        ReadOnlySpan<byte> ephemeralPublicKey,
        DateTimeOffset requestedAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(requestingNodeId);

        var prefix = "sunfish-recovery-request-v1\n"u8;
        var nodeIdBytes = Encoding.UTF8.GetBytes(requestingNodeId);
        var timestampBytes = Encoding.UTF8.GetBytes(requestedAt.ToString("O"));

        var totalLength = prefix.Length + nodeIdBytes.Length + ephemeralPublicKey.Length + timestampBytes.Length;
        var buffer = new byte[totalLength];
        var offset = 0;
        prefix.CopyTo(buffer.AsSpan(offset)); offset += prefix.Length;
        nodeIdBytes.CopyTo(buffer.AsSpan(offset)); offset += nodeIdBytes.Length;
        ephemeralPublicKey.CopyTo(buffer.AsSpan(offset)); offset += ephemeralPublicKey.Length;
        timestampBytes.CopyTo(buffer.AsSpan(offset));

        return buffer;
    }

    /// <summary>
    /// Sign a freshly-constructed <see cref="RecoveryRequest"/> with the
    /// ephemeral private key. The signature covers
    /// <see cref="CanonicalBytesForSigning"/>.
    /// </summary>
    public static RecoveryRequest Create(
        string requestingNodeId,
        ReadOnlySpan<byte> ephemeralPublicKey,
        ReadOnlySpan<byte> ephemeralPrivateKey,
        DateTimeOffset requestedAt,
        IEd25519Signer signer)
    {
        ArgumentException.ThrowIfNullOrEmpty(requestingNodeId);
        ArgumentNullException.ThrowIfNull(signer);
        if (ephemeralPublicKey.Length != EphemeralPublicKeyLength)
        {
            throw new ArgumentException(
                $"Ephemeral public key must be {EphemeralPublicKeyLength} bytes; got {ephemeralPublicKey.Length}.",
                nameof(ephemeralPublicKey));
        }

        var canonical = CanonicalBytesForSigning(requestingNodeId, ephemeralPublicKey, requestedAt);
        var signature = signer.Sign(canonical, ephemeralPrivateKey);
        return new RecoveryRequest(
            RequestingNodeId: requestingNodeId,
            EphemeralPublicKey: ephemeralPublicKey.ToArray(),
            RequestedAt: requestedAt,
            Signature: signature);
    }

    /// <summary>
    /// Verify that the <see cref="Signature"/> over this request is valid
    /// against the embedded <see cref="EphemeralPublicKey"/>. Returns
    /// <c>true</c> if the signature is well-formed and verifies; <c>false</c>
    /// otherwise. Trustees call this before accepting the request.
    /// </summary>
    public bool VerifySignature(IEd25519Signer signer)
    {
        ArgumentNullException.ThrowIfNull(signer);
        if (EphemeralPublicKey is null || EphemeralPublicKey.Length != EphemeralPublicKeyLength) return false;
        if (Signature is null || Signature.Length != SignatureLength) return false;

        var canonical = CanonicalBytesForSigning(RequestingNodeId, EphemeralPublicKey, RequestedAt);
        return signer.Verify(canonical, Signature, EphemeralPublicKey);
    }
}
