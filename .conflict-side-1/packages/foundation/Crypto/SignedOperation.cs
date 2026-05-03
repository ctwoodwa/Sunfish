namespace Sunfish.Foundation.Crypto;

/// <summary>
/// A cryptographically signed operation envelope. The <see cref="Signature"/> covers the canonical-JSON
/// form of <c>{issuedAt, issuerId, nonce, payload}</c> — see <see cref="CanonicalJson.SerializeSignable"/>.
/// </summary>
/// <remarks>
/// Because this is a <c>record</c>, callers can produce tampered variants via <c>with</c>-expressions
/// for testing (e.g. <c>op with { Payload = tampered }</c>) — those tampered variants will fail
/// <see cref="IOperationVerifier.Verify"/>.
/// </remarks>
/// <typeparam name="T">The payload type.</typeparam>
/// <param name="Payload">The domain payload being authorized.</param>
/// <param name="IssuerId">The principal that signed the envelope.</param>
/// <param name="IssuedAt">The wall-clock time at which the envelope was issued.</param>
/// <param name="Nonce">A unique per-issuance nonce, used to prevent replay at higher layers.</param>
/// <param name="Signature">The Ed25519 signature over the canonical signable bytes.</param>
public sealed record SignedOperation<T>(
    T Payload,
    PrincipalId IssuerId,
    DateTimeOffset IssuedAt,
    Guid Nonce,
    Signature Signature);
