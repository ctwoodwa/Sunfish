using NSec.Cryptography;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Produces Ed25519 signatures over <see cref="SignedOperation{T}"/> envelopes using the key
/// material held by the given <see cref="KeyPair"/>.
/// </summary>
public sealed class Ed25519Signer(KeyPair keyPair) : IOperationSigner
{
    private readonly KeyPair _keyPair = keyPair ?? throw new ArgumentNullException(nameof(keyPair));

    /// <inheritdoc />
    public PrincipalId IssuerId => _keyPair.PrincipalId;

    /// <inheritdoc />
    public ValueTask<SignedOperation<T>> SignAsync<T>(
        T payload,
        DateTimeOffset issuedAt,
        Guid nonce,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var alg = SignatureAlgorithm.Ed25519;
        var signable = CanonicalJson.SerializeSignable(payload, IssuerId, issuedAt, nonce);

        Span<byte> signature = stackalloc byte[Signature.LengthInBytes];
        alg.Sign(_keyPair.NSecKey, signable, signature);

        var envelope = new SignedOperation<T>(
            Payload: payload,
            IssuerId: IssuerId,
            IssuedAt: issuedAt,
            Nonce: nonce,
            Signature: Signature.FromBytes(signature));

        return ValueTask.FromResult(envelope);
    }
}
