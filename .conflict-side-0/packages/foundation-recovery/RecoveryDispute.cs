using System.Security.Cryptography;
using System.Text;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Foundation.Recovery;

/// <summary>
/// A signed message from a holder of the owner's original keys objecting
/// to an in-flight <see cref="RecoveryRequest"/>. Sub-pattern <b>#48e</b>
/// per ADR 0046 — the dispute path that closes the timed-recovery window
/// against an attacker who somehow assembles a fraudulent quorum.
/// </summary>
/// <remarks>
/// <para>
/// Trust model: a legitimate dispute proves the original keys are still
/// in someone's possession (the owner's), which means the recovery is
/// either an attack or no longer needed. Either way the coordinator
/// aborts.
/// </para>
/// <para>
/// The dispute binds to a specific request via
/// <see cref="RecoveryRequestHash"/> (SHA-256 of the request's canonical
/// signing bytes — the same hash <see cref="TrusteeAttestation"/> uses)
/// so a dispute cannot be replayed against a later request the original
/// owner did not see.
/// </para>
/// <para>
/// The coordinator validates that <see cref="DisputingPublicKey"/> is in
/// the configured "disputer set" — i.e., the public key matches an
/// identity the owner had before the recovery was initiated. Disputes
/// signed by an unknown key are silently dropped.
/// </para>
/// </remarks>
public sealed record RecoveryDispute(
    string DisputingNodeId,
    byte[] DisputingPublicKey,
    byte[] RecoveryRequestHash,
    DateTimeOffset DisputedAt,
    string Reason,
    byte[] Signature)
{
    /// <summary>
    /// Compute SHA-256 over the request's canonical signing bytes —
    /// the value bound into <see cref="RecoveryRequestHash"/>. Mirrors
    /// <see cref="TrusteeAttestation.HashOf"/> so disputers and trustees
    /// agree on the request identifier.
    /// </summary>
    public static byte[] HashOf(RecoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TrusteeAttestation.HashOf(request);
    }

    /// <summary>
    /// Produce the canonical byte sequence the disputer signs:
    /// <c>"sunfish-recovery-dispute-v1\n" || DisputingNodeId || RequestHash || DisputedAt(ISO-8601 UTC) || Reason</c>.
    /// Domain-separated from <see cref="RecoveryRequest"/> and
    /// <see cref="TrusteeAttestation"/> signing prefixes.
    /// </summary>
    public static byte[] CanonicalBytesForSigning(
        string disputingNodeId,
        ReadOnlySpan<byte> recoveryRequestHash,
        DateTimeOffset disputedAt,
        string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(disputingNodeId);
        ArgumentNullException.ThrowIfNull(reason);

        var prefix = "sunfish-recovery-dispute-v1\n"u8;
        var nodeIdBytes = Encoding.UTF8.GetBytes(disputingNodeId);
        var timestampBytes = Encoding.UTF8.GetBytes(disputedAt.ToString("O"));
        var reasonBytes = Encoding.UTF8.GetBytes(reason);

        var totalLength = prefix.Length + nodeIdBytes.Length + recoveryRequestHash.Length
            + timestampBytes.Length + reasonBytes.Length;
        var buffer = new byte[totalLength];
        var offset = 0;
        prefix.CopyTo(buffer.AsSpan(offset)); offset += prefix.Length;
        nodeIdBytes.CopyTo(buffer.AsSpan(offset)); offset += nodeIdBytes.Length;
        recoveryRequestHash.CopyTo(buffer.AsSpan(offset)); offset += recoveryRequestHash.Length;
        timestampBytes.CopyTo(buffer.AsSpan(offset)); offset += timestampBytes.Length;
        reasonBytes.CopyTo(buffer.AsSpan(offset));

        return buffer;
    }

    /// <summary>
    /// Sign a fresh dispute against the given <paramref name="request"/>
    /// using the disputer's durable Ed25519 keypair (typically the
    /// owner's NodeIdentity).
    /// </summary>
    public static RecoveryDispute Create(
        RecoveryRequest request,
        string disputingNodeId,
        ReadOnlySpan<byte> disputingPublicKey,
        ReadOnlySpan<byte> disputingPrivateKey,
        DateTimeOffset disputedAt,
        string reason,
        IEd25519Signer signer)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(disputingNodeId);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(signer);

        var requestHash = HashOf(request);
        var canonical = CanonicalBytesForSigning(disputingNodeId, requestHash, disputedAt, reason);
        var signature = signer.Sign(canonical, disputingPrivateKey);
        return new RecoveryDispute(
            DisputingNodeId: disputingNodeId,
            DisputingPublicKey: disputingPublicKey.ToArray(),
            RecoveryRequestHash: requestHash,
            DisputedAt: disputedAt,
            Reason: reason,
            Signature: signature);
    }

    /// <summary>
    /// Verify the dispute's signature against
    /// <see cref="DisputingPublicKey"/> and that
    /// <see cref="RecoveryRequestHash"/> matches the supplied request.
    /// Returns <c>true</c> only when both checks pass.
    /// </summary>
    public bool Verify(RecoveryRequest request, IEd25519Signer signer)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(signer);

        if (DisputingPublicKey is null || DisputingPublicKey.Length != RecoveryRequest.EphemeralPublicKeyLength) return false;
        if (Signature is null || Signature.Length != RecoveryRequest.SignatureLength) return false;
        if (RecoveryRequestHash is null || RecoveryRequestHash.Length != TrusteeAttestation.RequestHashLength) return false;

        var expectedHash = HashOf(request);
        if (!CryptographicOperations.FixedTimeEquals(RecoveryRequestHash, expectedHash))
        {
            return false;
        }

        var canonical = CanonicalBytesForSigning(DisputingNodeId, RecoveryRequestHash, DisputedAt, Reason ?? string.Empty);
        return signer.Verify(canonical, Signature, DisputingPublicKey);
    }
}
