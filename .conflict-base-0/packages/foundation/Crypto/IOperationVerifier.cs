namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Verifies the Ed25519 signature on a <see cref="SignedOperation{T}"/> envelope. This only checks
/// cryptographic integrity — authorization and policy are enforced by higher-level layers.
/// </summary>
public interface IOperationVerifier
{
    /// <summary>Returns <c>true</c> iff the signature is a valid Ed25519 signature by <c>IssuerId</c>
    /// over the canonical-JSON form of the envelope.</summary>
    bool Verify<T>(SignedOperation<T> op);
}
