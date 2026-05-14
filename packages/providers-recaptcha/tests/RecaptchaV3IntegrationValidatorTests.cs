using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Providers.Recaptcha.Integration;
using Sunfish.UICore.Wayfinder.Integrations;
using Xunit;

namespace Sunfish.Providers.Recaptcha.Tests;

/// <summary>
/// W#48 Phase 3b — RecaptchaV3IntegrationValidator unit tests.
/// Verifies schema drift-protection, marker-credential leak containment,
/// scheme-validation, and probe-based credential validation per ADR 0067 §6.2 + §Trust.
/// </summary>
public sealed class RecaptchaV3IntegrationValidatorTests
{
    private const string ValidSecretKey = "6LeIxAcTAAAAAGG-vFI1TnRWxMZNFuojJ4WifJWe";

    private static IReadOnlyDictionary<string, ReadOnlyMemory<byte>> SensitiveCreds(string secretKey = ValidSecretKey)
        => new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["secret-key"] = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(secretKey)),
        };

    private static IReadOnlyDictionary<string, JsonNode> NonSensitiveCreds(string? endpoint = null)
    {
        var d = new Dictionary<string, JsonNode>
        {
            ["site-key"] = JsonValue.Create("6LeIxAcTAAAAAJcZVRqyHh71UMIEGNQ_MXjiZKhI")!,
        };
        if (endpoint is not null)
        {
            d["verify-endpoint"] = JsonValue.Create(endpoint)!;
        }
        return d;
    }

    // ── Schema shape ──────────────────────────────────────────────────────

    [Fact]
    public void SchemaProvider_ReturnsOneSchema_ForCaptchaRecaptchaV3()
    {
        var provider = new RecaptchaV3IntegrationSchemaProvider();
        var schemas = provider.GetSchemas();
        Assert.Single(schemas);
        var schema = schemas[0];
        Assert.Equal("recaptcha-v3", schema.ProviderId);
        Assert.Equal(IntegrationCategory.Captcha, schema.Category);
    }

    [Fact]
    public void SchemaProvider_CredentialFields_ExactlyMatchValidatorKeys()
    {
        // M5: set-equality — detects both missing keys and unexpected extras
        var schema = new RecaptchaV3IntegrationSchemaProvider().GetSchemas()[0];
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in schema.CredentialFields) keys.Add(f.Key);
        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { "site-key", "secret-key", "verify-endpoint" },
            keys);
    }

    [Fact]
    public void SchemaProvider_SecretKey_IsSecret_RequiredWithCurrentPasswordHint()
    {
        var schema = new RecaptchaV3IntegrationSchemaProvider().GetSchemas()[0];
        var field = Assert.Single(schema.CredentialFields, f => f.Key == "secret-key");
        Assert.Equal(CredentialFieldKind.Secret, field.Kind);
        Assert.True(field.IsRequired);
        Assert.Equal(CredentialAutocompleteHint.CurrentPassword, field.AutocompleteHint);
    }

    [Fact]
    public void SchemaProvider_SiteKey_IsText_Required()
    {
        var schema = new RecaptchaV3IntegrationSchemaProvider().GetSchemas()[0];
        var field = Assert.Single(schema.CredentialFields, f => f.Key == "site-key");
        Assert.Equal(CredentialFieldKind.Text, field.Kind);
        Assert.True(field.IsRequired);
    }

    [Fact]
    public void SchemaProvider_VerifyEndpoint_IsUrl_Optional()
    {
        var schema = new RecaptchaV3IntegrationSchemaProvider().GetSchemas()[0];
        var field = Assert.Single(schema.CredentialFields, f => f.Key == "verify-endpoint");
        Assert.Equal(CredentialFieldKind.Url, field.Kind);
        Assert.False(field.IsRequired);
    }

    // ── Validator metadata ────────────────────────────────────────────────

    [Fact]
    public void Validator_SupportedCategory_IsCaptcha()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var validator = new RecaptchaV3IntegrationValidator(factory);
        Assert.Equal(IntegrationCategory.Captcha, validator.SupportedCategory);
        Assert.Equal("recaptcha-v3", validator.SupportedProvider);
    }

    // ── Probe outcomes ────────────────────────────────────────────────────

    [Fact]
    public async Task Validator_InvalidInputResponse_ReturnsValid()
    {
        // "invalid-input-response" without "invalid-input-secret" = key is valid
        var body = """{"success":false,"error-codes":["invalid-input-response"]}""";
        using var handler = new SuccessHandler(HttpStatusCode.OK, body);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Valid, result.Status);
    }

    [Fact]
    public async Task Validator_InvalidInputSecret_ReturnsInvalid()
    {
        var body = """{"success":false,"error-codes":["invalid-input-secret"]}""";
        using var handler = new SuccessHandler(HttpStatusCode.OK, body);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Invalid, result.Status);
        Assert.Equal("recaptcha-invalid-secret", result.ErrorCode);
    }

    [Fact]
    public async Task Validator_NetworkError_ReturnsUnreachable()
    {
        using var handler = new ThrowingHandler();
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Unreachable, result.Status);
        Assert.Equal("recaptcha-unreachable", result.ErrorCode);
    }

    [Fact]
    public async Task Validator_HttpError_ReturnsUnreachable()
    {
        using var handler = new SuccessHandler(HttpStatusCode.ServiceUnavailable, string.Empty);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Unreachable, result.Status);
    }

    [Fact]
    public async Task Validator_MissingSecretKey_ReturnsInvalid()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var result = await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(
                new Dictionary<string, ReadOnlyMemory<byte>>(),
                NonSensitiveCreds(),
                CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Invalid, result.Status);
        Assert.Equal("missing-secret-key", result.ErrorCode);
    }

    [Fact]
    public async Task Validator_UsesCustomEndpoint_WhenProvided()
    {
        // B2: custom endpoint uses https:// — http:// would be rejected before calling
        const string customEndpoint = "https://recaptcha.test/verify";
        var body = """{"success":false,"error-codes":["invalid-input-response"]}""";
        string? capturedUrl = null;
        using var handler = new CapturingHandler(HttpStatusCode.OK, body, url => capturedUrl = url);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(customEndpoint), CancellationToken.None);

        Assert.Equal(customEndpoint, capturedUrl);
    }

    [Fact]
    public async Task Validator_Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var handler = new CancellingHandler();
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new RecaptchaV3IntegrationValidator(factory)
                .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), cts.Token));
    }

    // ── B2: scheme validation ─────────────────────────────────────────────

    [Fact]
    public async Task Validator_HttpVerifyEndpoint_ReturnsInvalidInsecureScheme()
    {
        // B2: plaintext HTTP endpoint must be rejected before making any network
        // call — prevents secret-key POST to attacker-controlled infrastructure.
        var factory = Substitute.For<IHttpClientFactory>();

        var result = await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(
                SensitiveCreds(),
                NonSensitiveCreds("http://attacker.example/verify"),
                CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Invalid, result.Status);
        Assert.Equal("insecure-verify-endpoint", result.ErrorCode);
        factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task Validator_LoopbackHttpEndpoint_IsAllowed()
    {
        // B2: loopback HTTP is permitted (local dev / integration tests)
        var body = """{"success":false,"error-codes":["invalid-input-response"]}""";
        using var handler = new SuccessHandler(HttpStatusCode.OK, body);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(
                SensitiveCreds(),
                NonSensitiveCreds("http://127.0.0.1:9000/verify"),
                CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Valid, result.Status);
    }

    // ── M3: HttpClient internal timeout → Unreachable ────────────────────

    [Fact]
    public async Task Validator_Timeout_ReturnsUnreachable()
    {
        // M3: HttpClient's internal timeout raises OperationCanceledException whose
        // token differs from the caller's ct — after amendment, this returns Unreachable
        // instead of propagating to the caller.
        using var internalCts = new CancellationTokenSource();
        await internalCts.CancelAsync();
        using var handler = new CancelWithInternalTokenHandler(internalCts.Token);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(
                SensitiveCreds(),
                NonSensitiveCreds(),
                CancellationToken.None); // caller's token is NOT cancelled

        Assert.Equal(ProviderValidationStatus.Unreachable, result.Status);
    }

    // ── B1: transport-exception marker-leak ───────────────────────────────

    [Fact]
    public async Task Validator_TransportError_DoesNotLeakSecretInMessage()
    {
        // B1: ex.Message from TLS errors can embed raw credential bytes.
        // Verify the error message returned by Unreachable() never contains the key.
        const string markerKey = "MARKER_RECAPTCHA_SECRET_IN_TRANSPORT_ERROR_54321";
        using var handler = new ThrowingHandler(markerKey);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(
                new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["secret-key"] = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(markerKey)),
                },
                NonSensitiveCreds(),
                CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Unreachable, result.Status);
        Assert.DoesNotContain(markerKey, result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(markerKey, result.ErrorCode ?? string.Empty, StringComparison.Ordinal);
    }

    // ── Marker-credential leak ────────────────────────────────────────────

    [Fact]
    public async Task Validator_MarkerSecretKey_DoesNotAppearInErrorMessage()
    {
        const string markerKey = "MARKER_RECAPTCHA_SECRET_KEY_SENTINEL_12345";
        var body = """{"success":false,"error-codes":["invalid-input-secret"]}""";
        using var handler = new SuccessHandler(HttpStatusCode.OK, body);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new RecaptchaV3IntegrationValidator(factory)
            .ValidateAsync(
                new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["secret-key"] = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(markerKey)),
                },
                NonSensitiveCreds(),
                CancellationToken.None);

        Assert.DoesNotContain(markerKey, result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(markerKey, result.ErrorCode ?? string.Empty, StringComparison.Ordinal);
    }

    // ── Validator isolation — does not resolve ICaptchaVerifier ──────────

    [Fact]
    public async Task Validator_DoesNotRequire_ICaptchaVerifier()
    {
        // The validator MUST NOT depend on ICaptchaVerifier or RecaptchaV3CaptchaVerifier.
        var body = """{"success":false,"error-codes":["invalid-input-response"]}""";
        using var handler = new SuccessHandler(HttpStatusCode.OK, body);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var validator = new RecaptchaV3IntegrationValidator(factory);
        var result = await validator.ValidateAsync(
            SensitiveCreds(), NonSensitiveCreds(), CancellationToken.None);
        Assert.Equal(ProviderValidationStatus.Valid, result.Status);
    }

    // ── Test helpers ──────────────────────────────────────────────────────

    private sealed class SuccessHandler(HttpStatusCode code, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
    }

    private sealed class CapturingHandler(
        HttpStatusCode code, string body, Action<string> capture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            capture(request.RequestUri?.ToString() ?? string.Empty);
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ThrowingHandler(string? messageToInclude = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException($"simulated network failure {messageToInclude}"));
    }

    private sealed class CancellingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromCanceled<HttpResponseMessage>(ct);
    }

    /// <summary>
    /// Simulates HttpClient internal timeout: cancels with an internal token
    /// distinct from the caller's ct, so ct.IsCancellationRequested remains false.
    /// </summary>
    private sealed class CancelWithInternalTokenHandler(CancellationToken internalToken) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromCanceled<HttpResponseMessage>(internalToken);
    }
}
