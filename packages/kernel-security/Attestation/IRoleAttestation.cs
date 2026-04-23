namespace Sunfish.Kernel.Security.Attestation;

/// <summary>
/// Issues <see cref="RoleAttestation"/> objects — signs the attested tuple with the
/// admin's Ed25519 private key and returns a fully-signed record.
/// </summary>
public interface IAttestationIssuer
{
    /// <summary>
    /// Issues a new attestation valid for <paramref name="validity"/> starting now.
    /// </summary>
    /// <param name="teamId">16-byte team identifier.</param>
    /// <param name="subjectPublicKey">32-byte Ed25519 public key of the node being attested.</param>
    /// <param name="role">Role token.</param>
    /// <param name="validity">Lifetime of the attestation. Must be positive.</param>
    /// <param name="issuerPrivateKey">32-byte raw Ed25519 seed of the issuer (admin).</param>
    RoleAttestation Issue(
        byte[] teamId,
        byte[] subjectPublicKey,
        string role,
        TimeSpan validity,
        ReadOnlyMemory<byte> issuerPrivateKey);
}

/// <summary>
/// Verifies <see cref="RoleAttestation"/> signatures and enforces expiry / issuer binding.
/// </summary>
public interface IAttestationVerifier
{
    /// <summary>
    /// Returns <c>true</c> iff all of the following hold:
    /// <list type="number">
    ///   <item>the signature is a valid Ed25519 signature over the canonical CBOR encoding of the signed fields;</item>
    ///   <item>the signature was produced by <paramref name="expectedIssuerPublicKey"/> (matches <see cref="RoleAttestation.IssuerPublicKey"/>);</item>
    ///   <item><paramref name="now"/> is within <c>[IssuedAt, ExpiresAt)</c>;</item>
    ///   <item>field lengths are well-formed (team-id 16B, subject 32B, issuer 32B, signature 64B).</item>
    /// </list>
    /// </summary>
    bool Verify(RoleAttestation attestation, byte[] expectedIssuerPublicKey, DateTimeOffset now);
}
