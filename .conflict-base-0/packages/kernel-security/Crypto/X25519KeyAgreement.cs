using System.Security.Cryptography;
using NSec.Cryptography;

namespace Sunfish.Kernel.Security.Crypto;

/// <summary>
/// Default <see cref="IX25519KeyAgreement"/> backed by <c>NSec.Cryptography</c>.
/// Uses X25519 + HKDF-SHA256 + ChaCha20-Poly1305 to implement a NaCl-style sealed box.
/// </summary>
public sealed class X25519KeyAgreement : IX25519KeyAgreement
{
    private const int SharedSecretLength = 32;
    private const int AeadKeyLength = 32;
    private const int AeadNonceLength = 12;
    private const int AuthTagLength = 16;

    private static readonly KeyAgreementAlgorithm Kem = KeyAgreementAlgorithm.X25519;

    // HKDF "info" string — domain separates this KEM from any other uses of X25519 in Sunfish.
    private static readonly byte[] HkdfInfo = "sunfish-kernel-security:x25519-box:v1"u8.ToArray();

    /// <inheritdoc />
    public int PublicKeyLength => 32;

    /// <inheritdoc />
    public int PrivateKeyLength => 32;

    /// <inheritdoc />
    public int NonceLength => 24;

    /// <inheritdoc />
    public (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair()
    {
        var creationParams = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport,
        };
        using var key = Key.Create(Kem, creationParams);
        var privateKey = key.Export(KeyBlobFormat.RawPrivateKey);
        var publicKey = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        return (publicKey, privateKey);
    }

    /// <inheritdoc />
    public (byte[] Ciphertext, byte[] Nonce) Box(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> recipientPublicKey,
        ReadOnlySpan<byte> senderPrivateKey)
    {
        if (senderPrivateKey.Length != PrivateKeyLength)
        {
            throw new ArgumentException(
                $"X25519 private key must be {PrivateKeyLength} bytes (was {senderPrivateKey.Length}).",
                nameof(senderPrivateKey));
        }
        if (recipientPublicKey.Length != PublicKeyLength)
        {
            throw new ArgumentException(
                $"X25519 public key must be {PublicKeyLength} bytes (was {recipientPublicKey.Length}).",
                nameof(recipientPublicKey));
        }

        var nonce = new byte[NonceLength];
        RandomNumberGenerator.Fill(nonce);

        var aeadKey = DeriveAeadKey(senderPrivateKey, recipientPublicKey, nonce.AsSpan(0, 12));

        var ciphertext = new byte[plaintext.Length + AuthTagLength];
        using (var chacha = new System.Security.Cryptography.ChaCha20Poly1305(aeadKey))
        {
            chacha.Encrypt(
                nonce: nonce.AsSpan(12, AeadNonceLength),
                plaintext: plaintext,
                ciphertext: ciphertext.AsSpan(0, plaintext.Length),
                tag: ciphertext.AsSpan(plaintext.Length, AuthTagLength));
        }

        CryptographicOperations.ZeroMemory(aeadKey);
        return (ciphertext, nonce);
    }

    /// <inheritdoc />
    public byte[]? OpenBox(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> senderPublicKey,
        ReadOnlySpan<byte> recipientPrivateKey)
    {
        if (recipientPrivateKey.Length != PrivateKeyLength)
        {
            throw new ArgumentException(
                $"X25519 private key must be {PrivateKeyLength} bytes (was {recipientPrivateKey.Length}).",
                nameof(recipientPrivateKey));
        }
        if (senderPublicKey.Length != PublicKeyLength)
        {
            throw new ArgumentException(
                $"X25519 public key must be {PublicKeyLength} bytes (was {senderPublicKey.Length}).",
                nameof(senderPublicKey));
        }
        if (nonce.Length != NonceLength)
        {
            throw new ArgumentException(
                $"Nonce must be {NonceLength} bytes (was {nonce.Length}).",
                nameof(nonce));
        }
        if (ciphertext.Length < AuthTagLength)
        {
            return null;
        }

        byte[]? aeadKey = null;
        try
        {
            aeadKey = DeriveAeadKey(recipientPrivateKey, senderPublicKey, nonce[..12]);

            var plaintextLen = ciphertext.Length - AuthTagLength;
            var plaintext = new byte[plaintextLen];
            using var chacha = new System.Security.Cryptography.ChaCha20Poly1305(aeadKey);
            try
            {
                chacha.Decrypt(
                    nonce: nonce.Slice(12, AeadNonceLength),
                    ciphertext: ciphertext[..plaintextLen],
                    tag: ciphertext.Slice(plaintextLen, AuthTagLength),
                    plaintext: plaintext);
            }
            catch (AuthenticationTagMismatchException)
            {
                return null;
            }

            return plaintext;
        }
        finally
        {
            if (aeadKey is not null)
            {
                CryptographicOperations.ZeroMemory(aeadKey);
            }
        }
    }

    /// <summary>
    /// X25519(<paramref name="privateKey"/>, <paramref name="peerPublicKey"/>) → HKDF-SHA256 →
    /// ChaCha20-Poly1305 key. The first 12 bytes of the sealed-box nonce are mixed into
    /// HKDF salt so the AEAD key is unique per-message even if the X25519 shared secret
    /// is ever re-used at a higher layer.
    /// </summary>
    private static byte[] DeriveAeadKey(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        ReadOnlySpan<byte> nonceSalt)
    {
        using var myKey = Key.Import(Kem, privateKey, KeyBlobFormat.RawPrivateKey);
        var peer = PublicKey.Import(Kem, peerPublicKey, KeyBlobFormat.RawPublicKey);

        var sharedParams = default(SharedSecretCreationParameters);
        using var shared = Kem.Agree(myKey, peer, in sharedParams)
            ?? throw new CryptographicException(
                "X25519 key agreement failed (contributory check rejected the peer key).");

        var aeadKey = new byte[AeadKeyLength];
        KeyDerivationAlgorithm.HkdfSha256.DeriveBytes(shared, nonceSalt, HkdfInfo, aeadKey);
        return aeadKey;
    }
}
