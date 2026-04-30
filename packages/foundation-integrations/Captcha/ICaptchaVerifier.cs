using System.Net;

namespace Sunfish.Foundation.Integrations.Captcha;

/// <summary>
/// Egress contract for CAPTCHA verification. W#28 Phase 3 introduces this
/// substrate per ADR 0059 §"Inquiry-form abuse posture (ADR 0043 T2
/// boundary)" + §"Capability promotion" — the public-listings
/// inquiry-form 5-layer defense calls
/// <see cref="VerifyAsync(string, IPAddress, CancellationToken)"/> as
/// layer 1.
/// </summary>
/// <remarks>
/// Provider-neutral: this contract names no Google / hCaptcha / Cloudflare
/// vendor surface. The first adapter (<c>providers-recaptcha</c>) ships
/// in Phase 3.1; additional providers slot in without changing this
/// interface. Per ADR 0013, vendor SDK imports are quarantined to the
/// adapter package via <c>BannedSymbols.txt</c>.
/// </remarks>
public interface ICaptchaVerifier
{
    /// <summary>
    /// Verifies a CAPTCHA token issued by the consumer's browser-side
    /// challenge widget. Returns a result indicating pass/fail + a score
    /// (per reCAPTCHA v3 / hCaptcha 9 risk-score conventions; 0.0–1.0).
    /// </summary>
    /// <param name="token">The opaque token returned by the challenge widget.</param>
    /// <param name="clientIp">The end-user's source IP — passed through to the provider for risk-scoring.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CaptchaVerifyResult> VerifyAsync(string token, IPAddress clientIp, CancellationToken ct);
}

/// <summary>
/// Result of a CAPTCHA verify call.
/// </summary>
/// <param name="Passed">Whether the token verified successfully AND scored above the configured minimum.</param>
/// <param name="Score">Provider-returned risk score in <c>[0.0, 1.0]</c>; closer to 1.0 = lower risk. Not all providers return a score (older v2-style yes/no providers may always return <c>1.0</c> when <see cref="Passed"/> is true).</param>
/// <param name="Provider">Provider name (e.g., <c>"recaptcha-v3"</c>, <c>"hcaptcha"</c>) for audit trail attribution.</param>
public sealed record CaptchaVerifyResult(bool Passed, double Score, string Provider);

/// <summary>
/// Provider configuration (each adapter implements its own concrete
/// config record + DI-binds). The minimum-passing-score threshold is
/// the gate the substrate uses to decide pass/fail when a score is
/// returned.
/// </summary>
public interface ICaptchaProviderConfig
{
    /// <summary>Public site key for the browser-side challenge widget.</summary>
    string SiteKey { get; }

    /// <summary>Secret key used by the server-side verify call.</summary>
    string SecretKey { get; }

    /// <summary>
    /// Minimum acceptable risk score; verify results below this score
    /// are treated as failed even if the provider says the token is
    /// valid. Default per reCAPTCHA recommendations is 0.3.
    /// </summary>
    double MinPassingScore { get; }
}
