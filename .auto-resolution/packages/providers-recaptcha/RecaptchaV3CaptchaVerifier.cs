using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Sunfish.Foundation.Integrations.Captcha;

namespace Sunfish.Providers.Recaptcha;

/// <summary>
/// Google reCAPTCHA v3 adapter for <see cref="ICaptchaVerifier"/>. Calls
/// the documented verify endpoint via <see cref="HttpClient"/> (no Google
/// SDK dependency); maps the response score against the configured
/// <see cref="RecaptchaV3Config.MinPassingScore"/>.
/// </summary>
/// <remarks>
/// First <c>providers-*</c> package in the codebase — establishes the
/// <c>providers-{vendor}</c> naming convention per ADR 0013. Vendor SDK
/// imports stay out of this package because the verify call is a simple
/// HTTPS POST; future providers (hcaptcha, cloudflare-turnstile) follow
/// the same pattern.
/// </remarks>
public sealed class RecaptchaV3CaptchaVerifier : ICaptchaVerifier
{
    /// <summary>Provider identifier emitted on every <see cref="CaptchaVerifyResult"/>.</summary>
    public const string ProviderName = "recaptcha-v3";

    private readonly HttpClient _httpClient;
    private readonly RecaptchaV3Config _config;

    /// <summary>Creates the verifier with an injected <see cref="HttpClient"/> and provider config.</summary>
    public RecaptchaV3CaptchaVerifier(HttpClient httpClient, RecaptchaV3Config config)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrEmpty(config.SecretKey))
        {
            throw new ArgumentException("RecaptchaV3Config.SecretKey is required.", nameof(config));
        }
        _httpClient = httpClient;
        _config = config;
    }

    /// <inheritdoc />
    public async Task<CaptchaVerifyResult> VerifyAsync(string token, IPAddress clientIp, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentNullException.ThrowIfNull(clientIp);

        var form = new Dictionary<string, string>
        {
            ["secret"] = _config.SecretKey,
            ["response"] = token,
            ["remoteip"] = clientIp.ToString(),
        };

        using var content = new FormUrlEncodedContent(form);
        using var response = await _httpClient.PostAsync(_config.VerifyEndpoint, content, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new CaptchaVerifyResult(Passed: false, Score: 0.0, Provider: ProviderName);
        }

        var body = await response.Content.ReadFromJsonAsync<RecaptchaV3Response>(cancellationToken: ct).ConfigureAwait(false);
        if (body is null || !body.Success)
        {
            return new CaptchaVerifyResult(Passed: false, Score: body?.Score ?? 0.0, Provider: ProviderName);
        }

        var passed = body.Score >= _config.MinPassingScore;
        return new CaptchaVerifyResult(Passed: passed, Score: body.Score, Provider: ProviderName);
    }

    /// <summary>
    /// Wire-shape of Google's reCAPTCHA v3 verify response. Per Google's
    /// docs: <c>success</c> (bool), <c>score</c> (0.0–1.0), <c>action</c>
    /// (string), <c>challenge_ts</c> (ISO timestamp), <c>hostname</c>
    /// (string), <c>error-codes</c> (string[]).
    /// </summary>
    internal sealed record RecaptchaV3Response
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("score")]
        public double Score { get; init; }

        [JsonPropertyName("action")]
        public string? Action { get; init; }

        [JsonPropertyName("challenge_ts")]
        public string? ChallengeTimestamp { get; init; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; init; }

        [JsonPropertyName("error-codes")]
        public IReadOnlyList<string>? ErrorCodes { get; init; }
    }
}
