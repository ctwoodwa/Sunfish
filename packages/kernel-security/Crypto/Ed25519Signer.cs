using NSec.Cryptography;

namespace Sunfish.Kernel.Security.Crypto;

/// <summary>
/// Default <see cref="IEd25519Signer"/> backed by <c>NSec.Cryptography</c>.
/// Stateless — one instance can be shared across the process.
/// </summary>
public sealed class Ed25519Signer : IEd25519Signer
{
    private static readonly SignatureAlgorithm Alg = SignatureAlgorithm.Ed25519;

    /// <inheritdoc />
    public int PublicKeyLength => 32;

    /// <inheritdoc />
    public int PrivateKeyLength => 32;

    /// <inheritdoc />
    public int SignatureLength => 64;

    /// <inheritdoc />
    public (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair()
    {
        var creationParams = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport,
        };
        using var key = Key.Create(Alg, creationParams);

        var privateKey = key.Export(KeyBlobFormat.RawPrivateKey);
        var publicKey = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        return (publicKey, privateKey);
    }

    /// <inheritdoc />
    public (byte[] PublicKey, byte[] PrivateKey) GenerateFromSeed(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != PrivateKeyLength)
        {
            throw new ArgumentException(
                $"Ed25519 seed must be {PrivateKeyLength} bytes (was {seed.Length}).",
                nameof(seed));
        }

        var creationParams = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport,
        };
        using var key = Key.Import(Alg, seed, KeyBlobFormat.RawPrivateKey, creationParams);

        var privateKey = key.Export(KeyBlobFormat.RawPrivateKey);
        var publicKey = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        return (publicKey, privateKey);
    }

    /// <inheritdoc />
    public byte[] Sign(ReadOnlySpan<byte> message, ReadOnlySpan<byte> privateKey)
    {
        if (privateKey.Length != PrivateKeyLength)
        {
            throw new ArgumentException(
                $"Ed25519 private key must be {PrivateKeyLength} bytes (was {privateKey.Length}).",
                nameof(privateKey));
        }

        using var key = Key.Import(Alg, privateKey, KeyBlobFormat.RawPrivateKey);
        return Alg.Sign(key, message);
    }

    /// <inheritdoc />
    public bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey)
    {
        if (publicKey.Length != PublicKeyLength || signature.Length != SignatureLength)
        {
            return false;
        }

        PublicKey nsecPub;
        try
        {
            nsecPub = PublicKey.Import(Alg, publicKey, KeyBlobFormat.RawPublicKey);
        }
        catch (FormatException)
        {
            return false;
        }

        return Alg.Verify(nsecPub, message, signature);
    }
}
