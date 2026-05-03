namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Signs operations on behalf of a principal, producing <see cref="SignedOperation{T}"/> envelopes.
/// </summary>
public interface IOperationSigner
{
    /// <summary>The principal that this signer will attribute signed operations to.</summary>
    PrincipalId IssuerId { get; }

    /// <summary>
    /// Produces a signed envelope containing the payload, issuer, issuedAt timestamp, and nonce.
    /// The signature covers the canonical-JSON form of all four fields together.
    /// </summary>
    ValueTask<SignedOperation<T>> SignAsync<T>(
        T payload,
        DateTimeOffset issuedAt,
        Guid nonce,
        CancellationToken ct = default);
}
