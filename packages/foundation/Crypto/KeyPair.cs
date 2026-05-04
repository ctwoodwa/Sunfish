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

    /// <summary>
    /// Signs the supplied byte sequence with this keypair's Ed25519 private key.
    /// Used by wire-protocol layers (e.g. crew-comms HELLO/HEARTBEAT) where the
    /// signed bytes are NOT a canonical-JSON <see cref="SignedOperation{T}"/>
    /// envelope but a protocol-specific concatenation defined by the caller.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Ed25519Signer"/> for ledger / audit-trail / cross-process
    /// envelopes that need <see cref="CanonicalJson"/> signing discipline.
    /// <see cref="Sign"/> exists exclusively for protocol-level signables that
    /// already define their own canonical byte stream.
    /// </remarks>
    public Signature Sign(ReadOnlySpan<byte> data)
    {
        Span<byte> signature = stackalloc byte[Signature.LengthInBytes];
        SignatureAlgorithm.Ed25519.Sign(_key, data, signature);
        return Signature.FromBytes(signature);
    }

    /// <summary>
    /// Verifies an Ed25519 signature over <paramref name="data"/> using a raw
    /// 32-byte public key. Returns <c>false</c> for malformed keys or invalid
    /// signatures. Symmetric companion to <see cref="Sign"/> for wire-protocol
    /// callers that have a public-key blob (not a <c>SignedOperation</c>).
    /// </summary>
    public static bool VerifyRaw(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        if (publicKey.Length != PrincipalId.LengthInBytes) return false;
        if (signature.Length != Signature.LengthInBytes) return false;
        try
        {
            var pk = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKey, KeyBlobFormat.RawPublicKey);
            return SignatureAlgorithm.Ed25519.Verify(pk, data, signature);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _key.Dispose();
}
