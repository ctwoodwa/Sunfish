namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Derives per-team <b>X25519</b> keypairs from the install's root seed —
/// the W#67 / ADR 0046-A6 counterpart to <see cref="ITeamSubkeyDerivation"/>
/// (which derives <b>Ed25519</b> identity subkeys). The X25519 keypair is
/// used to encrypt the root seed for trustees during social recovery
/// setup (owner → trustee envelopes) and to re-encrypt that envelope to
/// the recovering device's ephemeral key during attestation submission.
/// </summary>
/// <remarks>
/// <para>
/// <b>Domain separation.</b> The HKDF info prefix
/// <c>"sunfish-x25519-team-v1:"</c> is distinct from the Ed25519 prefix
/// <c>"sunfish-team-subkey-v1:"</c> (<see cref="TeamSubkeyDerivation.InfoPrefix"/>)
/// and from the SQLCipher prefix <c>"sunfish:sqlcipher:v1:"</c>
/// (<see cref="SqlCipherKeyDerivation"/>), so the X25519 key for a team
/// never collides with that team's signing key or with its
/// at-rest-encryption key — even when all three derive from the same
/// 32-byte root seed.
/// </para>
/// <para>
/// <b>RFC 7748 clamping.</b> The 32 bytes returned by
/// <see cref="DeriveX25519PrivateKey"/> are raw HKDF output — RFC 7748
/// clamping (clear bits 0/1/2 of byte 0, clear bit 7 of byte 31, set bit
/// 6 of byte 31) is NOT applied by this method. NSec's
/// <c>Key.Import(KeyAgreementAlgorithm.X25519, raw, RawPrivateKey)</c>
/// applies clamping internally during scalar multiplication, so callers
/// must NOT clamp manually.
/// </para>
/// </remarks>
public interface IX25519SubkeyDerivation
{
    /// <summary>
    /// Derives a 32-byte X25519 raw private key for <paramref name="teamId"/>
    /// from <paramref name="rootSeed"/> using HKDF-Expand-SHA256 with info
    /// prefix <c>"sunfish-x25519-team-v1:"</c>. The returned bytes are
    /// suitable for <c>NSec.Cryptography.Key.Import(KeyAgreementAlgorithm.X25519,
    /// bytes, KeyBlobFormat.RawPrivateKey)</c> — do NOT pre-clamp.
    /// </summary>
    byte[] DeriveX25519PrivateKey(ReadOnlyMemory<byte> rootSeed, string teamId);

    /// <summary>
    /// Derives the X25519 public key corresponding to
    /// <see cref="DeriveX25519PrivateKey"/> for the same
    /// <paramref name="rootSeed"/> + <paramref name="teamId"/> pair.
    /// Equivalent to Curve25519 scalar multiplication of the (clamped)
    /// private key with the X25519 base point.
    /// </summary>
    byte[] DeriveX25519PublicKey(ReadOnlyMemory<byte> rootSeed, string teamId);
}
