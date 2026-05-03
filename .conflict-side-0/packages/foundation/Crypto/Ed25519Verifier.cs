using NSec.Cryptography;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Verifies Ed25519 signatures on <see cref="SignedOperation{T}"/> envelopes. Stateless — one
/// instance may be shared across verify calls.
/// </summary>
public sealed class Ed25519Verifier : IOperationVerifier
{
    /// <inheritdoc />
    public bool Verify<T>(SignedOperation<T> op)
    {
        ArgumentNullException.ThrowIfNull(op);

        var alg = SignatureAlgorithm.Ed25519;
        PublicKey publicKey;
        try
        {
            publicKey = PublicKey.Import(alg, op.IssuerId.AsSpan(), KeyBlobFormat.RawPublicKey);
        }
        catch (FormatException)
        {
            // Malformed public key bytes — treat as failed verification rather than leak exception.
            return false;
        }

        var signable = CanonicalJson.SerializeSignable(op.Payload, op.IssuerId, op.IssuedAt, op.Nonce);
        return alg.Verify(publicKey, signable, op.Signature.AsSpan());
    }
}
