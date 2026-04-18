namespace Sunfish.Foundation.Macaroons;

/// <summary>
/// Verifies a macaroon against the issuer's root key and a
/// <see cref="MacaroonContext"/> supplied by the relying party.
/// </summary>
public interface IMacaroonVerifier
{
    /// <summary>
    /// Verifies <paramref name="macaroon"/> against <paramref name="context"/>. Returns a
    /// structured <see cref="MacaroonVerificationResult"/> rather than throwing; callers
    /// should treat every non-<c>IsValid</c> outcome as access-denied.
    /// </summary>
    ValueTask<MacaroonVerificationResult> VerifyAsync(
        Macaroon macaroon,
        MacaroonContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Outcome of a macaroon verification.
/// </summary>
/// <param name="IsValid">True iff the signature chain is authentic AND every caveat
/// evaluates to true against the supplied context.</param>
/// <param name="Reason">When <see cref="IsValid"/> is false, a short diagnostic string
/// suitable for logging but not for leaking to untrusted callers verbatim.</param>
public sealed record MacaroonVerificationResult(bool IsValid, string? Reason = null);
