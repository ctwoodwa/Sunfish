using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Providers.Recaptcha.Integration;

/// <summary>
/// <see cref="IIntegrationProviderValidator"/> for the Google reCAPTCHA v3
/// captcha adapter per ADR 0067 §6.2 / W#48 Phase 3b.
/// </summary>
/// <remarks>
/// <para>
/// Validation strategy: POST an intentionally-invalid token to the verify
/// endpoint and inspect the error-codes array.
/// <list type="bullet">
/// <item>
///   <c>invalid-input-response</c> (but NOT <c>invalid-input-secret</c>) means
///   the secret key is structurally valid — Google accepted it but rejected the
///   dummy token, which is the expected outcome. Returns
///   <see cref="ProviderValidationStatus.Valid"/>.
/// </item>
/// <item>
///   <c>invalid-input-secret</c> means the secret key itself is malformed or
///   revoked. Returns <see cref="ProviderValidationStatus.Invalid"/>.
/// </item>
/// <item>
///   Network/transport errors return
///   <see cref="ProviderValidationStatus.Unreachable"/>.
/// </item>
/// </list>
/// </para>
/// <para>
/// Credential key conventions (matches <see cref="RecaptchaV3IntegrationSchemaProvider"/>):
/// <list type="bullet">
/// <item><c>secret-key</c> — sensitive (UTF-8 encoded).</item>
/// <item><c>site-key</c> — non-sensitive JSON string.</item>
/// <item><c>verify-endpoint</c> — non-sensitive JSON string, optional.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class RecaptchaV3IntegrationValidator : IIntegrationProviderValidator
{
    private readonly IHttpClientFactory _httpFactory;

    public RecaptchaV3IntegrationValidator(IHttpClientFactory httpFactory)
    {
        ArgumentNullException.ThrowIfNull(httpFactory);
        _httpFactory = httpFactory;
    }

    public IntegrationCategory SupportedCategory => IntegrationCategory.Captcha;
    public string SupportedProvider => "recaptcha-v3";

    public async Task<IntegrationValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, System.ReadOnlyMemory<byte>> sensitiveCredentials,
        IReadOnlyDictionary<string, JsonNode> nonSensitiveCredentials,
        CancellationToken ct)
    {
        if (!sensitiveCredentials.TryGetValue("secret-key", out var secretKeyBytes) || secretKeyBytes.IsEmpty)
        {
            return Fail("missing-secret-key", "secret-key credential is required.");
        }

        var secretKey = Encoding.UTF8.GetString(secretKeyBytes.Span);
        var verifyEndpoint = nonSensitiveCredentials.TryGetValue("verify-endpoint", out var epNode)
            ? epNode?.GetValue<string>()
            : null;
        if (string.IsNullOrWhiteSpace(verifyEndpoint))
        {
            verifyEndpoint = RecaptchaV3Config.DefaultVerifyEndpoint;
        }

        try
        {
            using var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(5);

            // POST with a deliberately invalid token — Google's documented
            // probe pattern for key validation. A valid secret key will
            // produce error-codes: ["invalid-input-response"]; an invalid
            // secret key produces error-codes: ["invalid-input-secret"].
            var form = new Dictionary<string, string>
            {
                ["secret"] = secretKey,
                ["response"] = "integration-validator-probe",
            };
            using var content = new FormUrlEncodedContent(form);
            using var response = await http.PostAsync(verifyEndpoint, content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Unreachable($"reCAPTCHA verify endpoint returned {(int)response.StatusCode}.");
            }

            var body = await response.Content
                .ReadFromJsonAsync<RecaptchaProbeResponse>(cancellationToken: ct)
                .ConfigureAwait(false);

            if (body?.ErrorCodes is { } codes)
            {
                foreach (var code in codes)
                {
                    if (string.Equals(code, "invalid-input-secret", StringComparison.OrdinalIgnoreCase))
                    {
                        return Fail("recaptcha-invalid-secret", "The reCAPTCHA v3 secret key is invalid or revoked.");
                    }
                }
                // "invalid-input-response" without "invalid-input-secret" → key is valid
                return Valid();
            }

            // No error-codes and success=true is unexpected for a dummy token, but treat as valid
            return Valid();
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Unreachable($"Could not reach reCAPTCHA verify endpoint: {ex.Message}");
        }
        catch
        {
            return Unreachable("Could not reach reCAPTCHA verify endpoint.");
        }
    }

    private static IntegrationValidationResult Valid() =>
        new(ProviderValidationStatus.Valid, DateTimeOffset.UtcNow, null, null);

    private static IntegrationValidationResult Fail(string code, string message) =>
        new(ProviderValidationStatus.Invalid, DateTimeOffset.UtcNow, code, message);

    private static IntegrationValidationResult Unreachable(string message) =>
        new(ProviderValidationStatus.Unreachable, DateTimeOffset.UtcNow, "recaptcha-unreachable", message);

    /// <summary>Wire shape of Google's reCAPTCHA verify response (probe subset).</summary>
    private sealed record RecaptchaProbeResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error-codes")]
        public IReadOnlyList<string>? ErrorCodes { get; init; }
    }
}
