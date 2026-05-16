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
/// <b>W#67 / ADR 0046-A6 — seed-envelope payload.</b> The attestation also
/// carries the trustee's contribution to the seed-delivery protocol:
/// <see cref="TrusteeDHPublicKey"/> (the trustee's per-team X25519 public
/// key from <c>IX25519SubkeyDerivation</c>), <see cref="EncryptedSeedEnvelopeCiphertext"/>
/// (the trustee-held root seed re-encrypted to the requesting device's
/// <c>EphemeralDHPublicKey</c> via <c>IX25519KeyAgreement.Box</c>), and
/// <see cref="EncryptedSeedEnvelopeNonce"/>. These three fields are
/// included in <see cref="CanonicalBytesForSigning"/> so the trustee's
/// signature binds the envelope to the attestation — a malicious peer
/// cannot substitute a different envelope after the fact.
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
    byte[] Signature,
    byte[] TrusteeDHPublicKey,
    byte[] EncryptedSeedEnvelopeCiphertext,
    byte[] EncryptedSeedEnvelopeNonce)
{
    /// <summary>Length of the SHA-256 request hash.</summary>
    public const int RequestHashLength = 32;

    /// <summary>Length of the trustee's per-team X25519 public key (W#67).</summary>
    public const int TrusteeDHPublicKeyLength = 32;

    /// <summary>
    /// Length of the seed-envelope ciphertext: 32-byte root seed + 16-byte
    /// ChaCha20-Poly1305 auth tag (W#67 / ADR 0046-A6).
    /// </summary>
    public const int SeedEnvelopeCiphertextLength = 48;

    /// <summary>
    /// Length of the seed-envelope nonce — 24 bytes as carried on the wire
    /// by <c>IX25519KeyAgreement.Box</c> (W#67).
    /// </summary>
    public const int SeedEnvelopeNonceLength = 24;

    /// <summary>
    /// Compute SHA-256 over the request's canonical signing bytes —
    /// the value bound into <see cref="RecoveryRequestHash"/>.
    /// </summary>
    public static byte[] HashOf(RecoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var canonical = RecoveryRequest.CanonicalBytesForSigning(
            request.RequestingNodeId,
            request.EphemeralPublicKey,
            request.EphemeralDHPublicKey,
            request.RequestedAt);
        return SHA256.HashData(canonical);
    }

    /// <summary>
    /// Produce the canonical byte sequence the trustee signs:
    /// <c>"sunfish-trustee-attestation-v1\n" || TrusteeNodeId || RequestHash
    /// || AttestedAt(ISO-8601 UTC) || TrusteeDHPublicKey
    /// || SeedEnvelopeCiphertext || SeedEnvelopeNonce</c>.
    /// Domain-separated from <see cref="RecoveryRequest"/> signing.
    /// </summary>
    /// <remarks>
    /// Field order is locked per ADR 0046-A6: prefix → NodeId → RequestHash
    /// → AttestedAt → TrusteeDHPublicKey → SeedEnvelopeCiphertext
    /// → SeedEnvelopeNonce. Any reorder is a wire-format break.
    /// </remarks>
    public static byte[] CanonicalBytesForSigning(
        string trusteeNodeId,
        ReadOnlySpan<byte> recoveryRequestHash,
        DateTimeOffset attestedAt,
        ReadOnlySpan<byte> trusteeDHPublicKey,
        ReadOnlySpan<byte> encryptedSeedEnvelopeCiphertext,
        ReadOnlySpan<byte> encryptedSeedEnvelopeNonce)
    {
        ArgumentException.ThrowIfNullOrEmpty(trusteeNodeId);

        var prefix = "sunfish-trustee-attestation-v1\n"u8;
        var nodeIdBytes = Encoding.UTF8.GetBytes(trusteeNodeId);
        var timestampBytes = Encoding.UTF8.GetBytes(attestedAt.ToString("O"));

        var totalLength = prefix.Length + nodeIdBytes.Length
            + recoveryRequestHash.Length + timestampBytes.Length
            + trusteeDHPublicKey.Length
            + encryptedSeedEnvelopeCiphertext.Length
            + encryptedSeedEnvelopeNonce.Length;
        var buffer = new byte[totalLength];
        var offset = 0;
        prefix.CopyTo(buffer.AsSpan(offset)); offset += prefix.Length;
        nodeIdBytes.CopyTo(buffer.AsSpan(offset)); offset += nodeIdBytes.Length;
        recoveryRequestHash.CopyTo(buffer.AsSpan(offset)); offset += recoveryRequestHash.Length;
        timestampBytes.CopyTo(buffer.AsSpan(offset)); offset += timestampBytes.Length;
        trusteeDHPublicKey.CopyTo(buffer.AsSpan(offset)); offset += trusteeDHPublicKey.Length;
        encryptedSeedEnvelopeCiphertext.CopyTo(buffer.AsSpan(offset)); offset += encryptedSeedEnvelopeCiphertext.Length;
        encryptedSeedEnvelopeNonce.CopyTo(buffer.AsSpan(offset));

        return buffer;
    }

    /// <summary>
    /// Sign a fresh attestation for the given <paramref name="request"/>
    /// using the trustee's durable Ed25519 keypair. The signature covers
    /// the W#67 seed-envelope payload — callers MUST supply the trustee's
    /// X25519 public key + the re-encrypted seed envelope (ciphertext +
    /// nonce) bound to the requesting device's
    /// <see cref="RecoveryRequest.EphemeralDHPublicKey"/>.
    /// </summary>
    public static TrusteeAttestation Create(
        RecoveryRequest request,
        string trusteeNodeId,
        ReadOnlySpan<byte> trusteePublicKey,
        ReadOnlySpan<byte> trusteePrivateKey,
        DateTimeOffset attestedAt,
        IEd25519Signer signer,
        ReadOnlySpan<byte> trusteeDHPublicKey,
        ReadOnlySpan<byte> encryptedSeedEnvelopeCiphertext,
        ReadOnlySpan<byte> encryptedSeedEnvelopeNonce)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(trusteeNodeId);
        ArgumentNullException.ThrowIfNull(signer);

        var requestHash = HashOf(request);
        var canonical = CanonicalBytesForSigning(
            trusteeNodeId, requestHash, attestedAt,
            trusteeDHPublicKey, encryptedSeedEnvelopeCiphertext, encryptedSeedEnvelopeNonce);
        var signature = signer.Sign(canonical, trusteePrivateKey);
        return new TrusteeAttestation(
            TrusteeNodeId:                    trusteeNodeId,
            TrusteePublicKey:                 trusteePublicKey.ToArray(),
            RecoveryRequestHash:              requestHash,
            AttestedAt:                       attestedAt,
            Signature:                        signature,
            TrusteeDHPublicKey:               trusteeDHPublicKey.ToArray(),
            EncryptedSeedEnvelopeCiphertext:  encryptedSeedEnvelopeCiphertext.ToArray(),
            EncryptedSeedEnvelopeNonce:       encryptedSeedEnvelopeNonce.ToArray());
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
        if (TrusteeDHPublicKey is null) return false;
        if (EncryptedSeedEnvelopeCiphertext is null) return false;
        if (EncryptedSeedEnvelopeNonce is null) return false;

        var expectedHash = HashOf(request);
        if (!CryptographicOperations.FixedTimeEquals(RecoveryRequestHash, expectedHash))
        {
            return false;
        }

        var canonical = CanonicalBytesForSigning(
            TrusteeNodeId, RecoveryRequestHash, AttestedAt,
            TrusteeDHPublicKey, EncryptedSeedEnvelopeCiphertext, EncryptedSeedEnvelopeNonce);
        return signer.Verify(canonical, Signature, TrusteePublicKey);
    }
}
