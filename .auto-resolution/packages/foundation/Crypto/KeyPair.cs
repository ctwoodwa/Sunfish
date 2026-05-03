using NSec.Cryptography;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// An Ed25519 keypair wrapping an <see cref="NSec.Cryptography.Key"/>. Generated keypairs must be
/// disposed to release unmanaged secret-key material (NSec zeroes it on dispose).
/// </summary>
public sealed class KeyPair : IDisposable
{
    private readonly Key _key;
    private readonly PrincipalId _principalId;

    private KeyPair(Key key)
    {
        _key = key;

        // Export the public key as raw 32 bytes and wrap it as PrincipalId.
        var publicBlob = _key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        _principalId = PrincipalId.FromBytes(publicBlob);
    }

    /// <summary>Generates a fresh Ed25519 keypair.</summary>
    /// <remarks>The underlying key is created with <see cref="KeyExportPolicies.AllowPlaintextExport"/>
    /// so the public-key material can be re-exported. The secret key is not exported by this class.</remarks>
    public static KeyPair Generate()
    {
        var creationParameters = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport,
        };
        var key = Key.Create(SignatureAlgorithm.Ed25519, creationParameters);
        return new KeyPair(key);
    }

    /// <summary>The public-key identifier of this keypair.</summary>
    public PrincipalId PrincipalId => _principalId;

    /// <summary>Exposed to <see cref="Ed25519Signer"/> so it can call NSec's sign primitive directly.</summary>
    internal Key NSecKey => _key;

    /// <inheritdoc />
    public void Dispose() => _key.Dispose();
}
