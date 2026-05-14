using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Providers.Mesh.Headscale.Integration;
using Sunfish.UICore.Wayfinder.Integrations;
using Xunit;

namespace Sunfish.Providers.Mesh.Headscale.Tests;

/// <summary>
/// W#48 Phase 3b — HeadscaleIntegrationValidator unit tests.
/// Verifies schema drift-protection, credential-leak containment,
/// fail-closed probe behaviour, and scheme-validation per ADR 0067 §6.2 + §Trust.
/// </summary>
public sealed class HeadscaleIntegrationValidatorTests
{
    // B2: test URIs use HTTPS (HTTP is only allowed for loopback)
    private static readonly Uri BaseUri = new("https://headscale.test/");
    private static readonly Uri LoopbackUri = new("http://127.0.0.1:8080/");
    private const string ValidApiKey = "hskey-test-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static IReadOnlyDictionary<string, ReadOnlyMemory<byte>> SensitiveCreds(string apiKey = ValidApiKey)
        => new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["api-key"] = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(apiKey)),
        };

    private static IReadOnlyDictionary<string, JsonNode> NonSensitiveCreds(Uri? baseUri = null, string? user = null)
    {
        var d = new Dictionary<string, JsonNode>
        {
            ["base-url"] = JsonValue.Create((baseUri ?? BaseUri).ToString())!,
        };
        if (user is not null) d["user"] = JsonValue.Create(user)!;
        return d;
    }

    // ── Schema shape ──────────────────────────────────────────────────────

    [Fact]
    public void SchemaProvider_ReturnsOneSchema_ForMeshVpnHeadscale()
    {
        var provider = new HeadscaleIntegrationSchemaProvider();
        var schemas = provider.GetSchemas();
        Assert.Single(schemas);
        var schema = schemas[0];
        Assert.Equal("headscale", schema.ProviderId);
        Assert.Equal(IntegrationCategory.MeshVpn, schema.Category);
    }

    [Fact]
    public void SchemaProvider_CredentialFields_ExactlyMatchValidatorKeys()
    {
        // M5: set-equality — detects both missing keys and unexpected extras
        var schema = new HeadscaleIntegrationSchemaProvider().GetSchemas()[0];
        var fieldKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in schema.CredentialFields)
        {
            fieldKeys.Add(field.Key);
        }
        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { "base-url", "api-key", "user" },
            fieldKeys);
    }

    [Fact]
    public void SchemaProvider_ApiKey_IsSecret_RequiredWithCurrentPasswordHint()
    {
        var schema = new HeadscaleIntegrationSchemaProvider().GetSchemas()[0];
        var apiKeyField = Assert.Single(schema.CredentialFields, f => f.Key == "api-key");
        Assert.Equal(CredentialFieldKind.Secret, apiKeyField.Kind);
        Assert.True(apiKeyField.IsRequired);
        Assert.Equal(CredentialAutocompleteHint.CurrentPassword, apiKeyField.AutocompleteHint);
    }

    [Fact]
    public void SchemaProvider_BaseUrl_IsUrl_Required()
    {
        var schema = new HeadscaleIntegrationSchemaProvider().GetSchemas()[0];
        var urlField = Assert.Single(schema.CredentialFields, f => f.Key == "base-url");
        Assert.Equal(CredentialFieldKind.Url, urlField.Kind);
        Assert.True(urlField.IsRequired);
    }

    [Fact]
    public void SchemaProvider_User_IsText_Optional()
    {
        var schema = new HeadscaleIntegrationSchemaProvider().GetSchemas()[0];
        var userField = Assert.Single(schema.CredentialFields, f => f.Key == "user");
        Assert.Equal(CredentialFieldKind.Text, userField.Kind);
        Assert.False(userField.IsRequired);
    }

    // ── Validator metadata ────────────────────────────────────────────────

    [Fact]
    public void Validator_SupportedCategory_IsMeshVpn()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var validator = new HeadscaleIntegrationValidator(factory);
        Assert.Equal(IntegrationCategory.MeshVpn, validator.SupportedCategory);
        Assert.Equal("headscale", validator.SupportedProvider);
    }

    // ── Probe outcomes ────────────────────────────────────────────────────

    [Fact]
    public async Task Validator_200_ReturnsValid()
    {
        using var handler = new SuccessHandler(HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new HeadscaleIntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Valid, result.Status);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task Validator_401_ReturnsInvalidWithAuthCode()
    {
        using var handler = new SuccessHandler(HttpStatusCode.Unauthorized, string.Empty);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new HeadscaleIntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Invalid, result.Status);
        Assert.Equal("headscale-auth-failure", result.ErrorCode);
    }

    [Fact]
    public async Task Validator_NetworkError_ReturnsUnreachable()
    {
        using var handler = new ThrowingHandler();
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new HeadscaleIntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Unreachable, result.Status);
        Assert.Equal("headscale-unreachable", result.ErrorCode);
    }

    [Fact]
    public async Task Validator_MissingApiKey_ReturnsInvalid()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var result = await new HeadscaleIntegrationValidator(factory)
            .ValidateAsync(
                new Dictionary<string, ReadOnlyMemory<byte>>(),
                NonSensitiveCreds(),
                CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Invalid, result.Status);
        Assert.Equal("missing-api-key", result.ErrorCode);
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
            new HeadscaleIntegrationValidator(factory)
                .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), cts.Token));
    }

    // ── m1: 5xx / 429 → Unreachable ─────────────────────────────────────

    [Fact]
    public async Task Validator_503_ReturnsUnreachable()
    {
        using var handler = new SuccessHandler(HttpStatusCode.ServiceUnavailable, string.Empty);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new HeadscaleIntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Unreachable, result.Status);
    }

    [Fact]
    public async Task Validator_429_ReturnsUnreachable()
    {
        using var handler = new SuccessHandler(HttpStatusCode.TooManyRequests, string.Empty);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new HeadscaleIntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(), CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Unreachable, result.Status);
    }

    // ── B2: scheme validation ─────────────────────────────────────────────

    [Fact]
    public async Task Validator_HttpBaseUrl_ReturnsInvalidInsecureScheme()
    {
        // B2: plaintext HTTP to a non-loopback host must be rejected before
        // making any network call — prevents API-key leak to attacker infrastructure.
        var factory = Substitute.For<IHttpClientFactory>();
        var creds = new Dictionary<string, JsonNode>
        {
            ["base-url"] = JsonValue.Create("http://attacker.example/")!,
        };

        var result = await new HeadscaleIntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), creds, CancellationToken.None);

        Assert.Equal(ProviderValidationStatus.Invalid, result.Status);
        Assert.Equal("insecure-base-url", result.ErrorCode);
        // Verify no HTTP call was made
        factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task Validator_LoopbackHttpBaseUrl_IsAllowed()
    {
        // B2: loopback addresses are permitted over HTTP (local dev / integration tests)
        using var handler = new SuccessHandler(HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new HeadscaleIntegrationValidator(factory)
            .ValidateAsync(SensitiveCreds(), NonSensitiveCreds(LoopbackUri), CancellationToken.None);

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

        var result = await new HeadscaleIntegrationValidator(factory)
            .ValidateAsync(
                SensitiveCreds(),
                NonSensitiveCreds(),
                CancellationToken.None); // caller's token is NOT cancelled

        Assert.Equal(ProviderValidationStatus.Unreachable, result.Status);
    }

    // ── Marker-credential leak ────────────────────────────────────────────

    [Fact]
    public async Task Validator_MarkerApiKey_DoesNotAppearInErrorMessage()
    {
        const string markerKey = "MARKER_HEADSCALE_API_KEY_SENTINEL_12345";
        using var handler = new SuccessHandler(HttpStatusCode.Unauthorized, string.Empty);
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var result = await new HeadscaleIntegrationValidator(factory)
            .ValidateAsync(
                new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["api-key"] = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(markerKey)),
                },
                NonSensitiveCreds(),
                CancellationToken.None);

        Assert.DoesNotContain(markerKey, result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(markerKey, result.ErrorCode ?? string.Empty, StringComparison.Ordinal);
    }

    // ── Validator isolation — does not resolve IMeshVpnAdapter ────────────

    [Fact]
    public async Task Validator_DoesNotRequire_IMeshVpnAdapter()
    {
        // The validator builds its own HttpClient through IHttpClientFactory —
        // it MUST NOT depend on IMeshVpnAdapter or HeadscaleMeshAdapter.
        // This test verifies by constructing it with only IHttpClientFactory.
        using var handler = new SuccessHandler(HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient().Returns(client);

        var validator = new HeadscaleIntegrationValidator(factory);
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
                Content = new StringContent(body),
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(
                new HttpRequestException("simulated network failure"));
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
