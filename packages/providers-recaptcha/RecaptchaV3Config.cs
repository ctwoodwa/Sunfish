using Sunfish.Foundation.Integrations.Captcha;

namespace Sunfish.Providers.Recaptcha;

/// <summary>
/// Configuration for <see cref="RecaptchaV3CaptchaVerifier"/>. Materializes
/// <see cref="ICaptchaProviderConfig"/> with reCAPTCHA v3 specifics:
/// <see cref="VerifyEndpoint"/> defaults to Google's documented URL and
/// can be overridden for tests + on-prem proxies.
/// </summary>
public sealed record RecaptchaV3Config : ICaptchaProviderConfig
{
    /// <summary>Google's documented verify endpoint.</summary>
    public const string DefaultVerifyEndpoint = "https://www.google.com/recaptcha/api/siteverify";

    /// <summary>Default minimum passing score per Google's recommendation for reCAPTCHA v3.</summary>
    public const double DefaultMinPassingScore = 0.3;

    /// <inheritdoc />
    public required string SiteKey { get; init; }

    /// <inheritdoc />
    public required string SecretKey { get; init; }

    /// <inheritdoc />
    public double MinPassingScore { get; init; } = DefaultMinPassingScore;

    /// <summary>Verify endpoint URL; override for tests + on-prem proxies.</summary>
    public string VerifyEndpoint { get; init; } = DefaultVerifyEndpoint;
}
