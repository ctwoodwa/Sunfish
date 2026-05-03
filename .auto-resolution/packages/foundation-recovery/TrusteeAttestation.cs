using System.Security.Cryptography;
using System.Text;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Foundation.Recovery;

/// <summary>
/// Phase 1 G6 sub-pattern <b>#48a (multi-sig social)</b> per ADR 0046 — a
/// trustee's signed attestation that they have inspected a
/// <see cref="RecoveryRequest"/> and recognize the requesting device as
/// a legitimate replacement for the owner. Three of five attestations
/// (per the ADR 0046 quorum) start the recovery grace period.
/// </summary>
/// <remarks>
/// <para>
/// The attestation binds a trustee's identity to a specific recovery
/// request via <see cref="RecoveryRequestHash"/> — a SHA-256 hash of the
/// request's canonical signing bytes. This prevents a trustee's
/// attestation from being replayed against a different request the
/// trustee never approved.
/// </para>
/// <para>
/// <b>Trust model.</b> The trustee signs with their durable Sunfish
/// node identity (the same Ed25519 keypair that signs gossip-protocol
/// HELLO frames). A receiving recovery coordinator checks that the
/// trustee's NodeId is in the owner's previously-designated trustee
/// set; an attestation from a non-designated node is silently dropped.
/// </para>
/// </remarks>
public sealed record TrusteeAttestation(
    string TrusteeNodeId,
    byte[] TrusteePublicKey,
    byte[] RecoveryRequestHash,
    DateTimeOffset AttestedAt,
    byte[] Signature)
{
    /// <summary>Length of the SHA-256 request hash.</summary>
    public const int RequestHashLength = 32;

    /// <summary>
    /// Compute SHA-256 over the request's canonical signing bytes —
    /// the value bound into <see cref="RecoveryRequestHash"/>.
    /// </summary>
    public static byte[] HashOf(RecoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var canonical = RecoveryRequest.CanonicalBytesForSigning(
            request.RequestingNodeId, request.EphemeralPublicKey, request.RequestedAt);
        return SHA256.HashData(canonical);
    }

    /// <summary>
    /// Produce the canonical byte sequence the trustee signs:
    /// <c>"sunfish-trustee-attestation-v1\n" || TrusteeNodeId || RequestHash || AttestedAt(ISO-8601 UTC)</c>.
    /// Domain-separated from <see cref="RecoveryRequest"/> signing.
    /// </summary>
    public static byte[] CanonicalBytesForSigning(
        string trusteeNodeId,
        ReadOnlySpan<byte> recoveryRequestHash,
        DateTimeOffset attestedAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(trusteeNodeId);

        var prefix = "sunfish-trustee-attestation-v1\n"u8;
        var nodeIdBytes = Encoding.UTF8.GetBytes(trusteeNodeId);
        var timestampBytes = Encoding.UTF8.GetBytes(attestedAt.ToString("O"));

        var totalLength = prefix.Length + nodeIdBytes.Length + recoveryRequestHash.Length + timestampBytes.Length;
        var buffer = new byte[totalLength];
        var offset = 0;
        prefix.CopyTo(buffer.AsSpan(offset)); offset += prefix.Length;
        nodeIdBytes.CopyTo(buffer.AsSpan(offset)); offset += nodeIdBytes.Length;
        recoveryRequestHash.CopyTo(buffer.AsSpan(offset)); offset += recoveryRequestHash.Length;
        timestampBytes.CopyTo(buffer.AsSpan(offset));

        return buffer;
    }

    /// <summary>
    /// Sign a fresh attestation for the given <paramref name="request"/>
    /// using the trustee's durable Ed25519 keypair.
    /// </summary>
    public static TrusteeAttestation Create(
        RecoveryRequest request,
        string trusteeNodeId,
        ReadOnlySpan<byte> trusteePublicKey,
        ReadOnlySpan<byte> trusteePrivateKey,
        DateTimeOffset attestedAt,
        IEd25519Signer signer)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(trusteeNodeId);
        ArgumentNullException.ThrowIfNull(signer);

        var requestHash = HashOf(request);
        var canonical = CanonicalBytesForSigning(trusteeNodeId, requestHash, attestedAt);
        var signature = signer.Sign(canonical, trusteePrivateKey);
        return new TrusteeAttestation(
            TrusteeNodeId: trusteeNodeId,
            TrusteePublicKey: trusteePublicKey.ToArray(),
            RecoveryRequestHash: requestHash,
            AttestedAt: attestedAt,
            Signature: signature);
    }

    /// <summary>
    /// Verify the attestation's signature against the trustee's public
    /// key AND that <see cref="RecoveryRequestHash"/> matches the supplied
    /// <paramref name="request"/>. Both checks pass for a legitimate
    /// attestation; either failing returns <c>false</c>.
    /// </summary>
    public bool Verify(RecoveryRequest request, IEd25519Signer signer)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(signer);

        if (TrusteePublicKey is null || TrusteePublicKey.Length != RecoveryRequest.EphemeralPublicKeyLength) return false;
        if (Signature is null || Signature.Length != RecoveryRequest.SignatureLength) return false;
        if (RecoveryRequestHash is null || RecoveryRequestHash.Length != RequestHashLength) return false;

        var expectedHash = HashOf(request);
        if (!CryptographicOperations.FixedTimeEquals(RecoveryRequestHash, expectedHash))
        {
            return false;
        }

        var canonical = CanonicalBytesForSigning(TrusteeNodeId, RecoveryRequestHash, AttestedAt);
        return signer.Verify(canonical, Signature, TrusteePublicKey);
    }
}
