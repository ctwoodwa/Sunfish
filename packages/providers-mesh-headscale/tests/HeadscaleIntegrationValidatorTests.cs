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
/// and fail-closed probe behaviour per ADR 0067 §6.2 + §Trust.
/// </summary>
public sealed class HeadscaleIntegrationValidatorTests
{
    private static readonly Uri BaseUri = new("http://headscale.test/");
    private const string ValidApiKey = "hskey-test-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static IReadOnlyDictionary<string, ReadOnlyMemory<byte>> SensitiveCreds(string apiKey = ValidApiKey)
        => new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["api-key"] = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(apiKey)),
        };

    private static IReadOnlyDictionary<string, JsonNode> NonSensitiveCreds(string? user = null)
    {
        var d = new Dictionary<string, JsonNode>
        {
            ["base-url"] = JsonValue.Create(BaseUri.ToString())!,
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
    public void SchemaProvider_CredentialFields_MatchValidatorKeys()
    {
        // Verifies no schema-drift: the field keys declared in the schema
        // must exactly match what the validator reads from the dictionaries.
        var provider = new HeadscaleIntegrationSchemaProvider();
        var schema = provider.GetSchemas()[0];
        var fieldKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in schema.CredentialFields)
        {
            fieldKeys.Add(field.Key);
        }
        Assert.Contains("base-url", fieldKeys);
        Assert.Contains("api-key", fieldKeys);
        Assert.Contains("user", fieldKeys);
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

        // No HeadscaleMeshAdapter or HeadscaleClient passed — must succeed
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
}
