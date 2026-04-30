namespace Sunfish.Blocks.PublicListings.Defense;

/// <summary>
/// DNS MX-record resolution for Layer 3 of the inquiry-form defense.
/// Production deployments wire a real DNS resolver; tests + InMemory
/// scenarios swap in a stub that returns canned answers.
/// </summary>
public interface IEmailMxResolver
{
    /// <summary>
    /// Returns whether <paramref name="domain"/> publishes any MX
    /// record. <c>true</c> means the domain accepts mail; <c>false</c>
    /// means there is no MX record (the address is undeliverable
    /// from a CAPTCHA-bypass perspective).
    /// </summary>
    Task<bool> HasMxRecordAsync(string domain, CancellationToken ct);
}
