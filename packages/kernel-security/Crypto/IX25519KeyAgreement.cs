namespace Sunfish.Kernel.Security.Crypto;

/// <summary>
/// X25519 Diffie-Hellman key agreement combined with authenticated encryption —
/// i.e. a NaCl-style sealed box. Used by the role-key wrapper (paper §11.3) to
/// encrypt a role key for a specific member.
/// </summary>
/// <remarks>
/// <para>
/// Concrete implementation: sender derives a shared secret via X25519
/// (<c>senderPrivate</c> × <c>recipientPublic</c>), stretches it with HKDF-SHA256,
/// then encrypts plaintext using ChaCha20-Poly1305 with a freshly generated 24-byte
/// nonce. The 24-byte nonce is converted to a 12-byte ChaCha20-Poly1305 nonce via a
/// deterministic HChaCha20-style derivation (XChaCha20 construction); this library
/// uses the simpler scheme of reserving the first 12 bytes of the 24-byte nonce
/// directly and keying-off the shared secret, which matches the security argument
/// for random-per-message nonces under a single-use key-agreement secret.
/// </para>
/// <para>
/// Nonce source: <see cref="System.Security.Cryptography.RandomNumberGenerator"/>.
/// The caller does not need to supply a nonce; <see cref="Box"/> returns one.
/// </para>
/// </remarks>
public interface IX25519KeyAgreement
{
    /// <summary>X25519 public-key length in bytes (32).</summary>
    int PublicKeyLength { get; }

    /// <summary>X25519 private-key length in bytes (32).</summary>
    int PrivateKeyLength { get; }

    /// <summary>Nonce length in bytes as carried on the wire (24).</summary>
    int NonceLength { get; }

    /// <summary>Generates a fresh X25519 key pair.</summary>
    (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair();

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> under the shared secret derived from
    /// <paramref name="senderPrivateKey"/> × <paramref name="recipientPublicKey"/>.
    /// Returns the ciphertext (plaintext length + 16 authentication-tag bytes) and
    /// a freshly generated 24-byte nonce.
    /// </summary>
    (byte[] Ciphertext, byte[] Nonce) Box(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> recipientPublicKey,
        ReadOnlySpan<byte> senderPrivateKey);

    /// <summary>
    /// Decrypts a sealed box. Returns <c>null</c> on any decryption or authentication
    /// failure — never throws on tampering. Throws only for malformed key or nonce lengths.
    /// </summary>
    byte[]? OpenBox(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> senderPublicKey,
        ReadOnlySpan<byte> recipientPrivateKey);
}
