using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Providers.Mesh.Headscale.Integration;

/// <summary>
/// <see cref="IIntegrationProviderValidator"/> for the Headscale
/// mesh-VPN adapter per ADR 0067 §6.2 / W#48 Phase 3b.
/// Probes <c>GET /api/v1/node</c> — an authenticated control-plane
/// endpoint — to verify both reachability and API-key validity.
/// </summary>
/// <remarks>
/// <para>
/// The validator deliberately does NOT exercise <see cref="HeadscaleMeshAdapter"/>
/// — it issues its own probe so validation logic stays independent of the
/// runtime transport layer.
/// </para>
/// <para>
/// Credential key conventions (matches <see cref="HeadscaleIntegrationSchemaProvider"/>):
/// <list type="bullet">
/// <item><c>api-key</c> — sensitive (UTF-8 encoded); passed as Bearer token.</item>
/// <item><c>base-url</c> — non-sensitive JSON string.</item>
/// <item><c>user</c> — non-sensitive JSON string, optional.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class HeadscaleIntegrationValidator : IIntegrationProviderValidator
{
    private readonly IHttpClientFactory _httpFactory;

    public HeadscaleIntegrationValidator(IHttpClientFactory httpFactory)
    {
        ArgumentNullException.ThrowIfNull(httpFactory);
        _httpFactory = httpFactory;
    }

    public IntegrationCategory SupportedCategory => IntegrationCategory.MeshVpn;
    public string SupportedProvider => "headscale";

    public async Task<IntegrationValidationResult> ValidateAsync(
        IReadOnlyDictionary<string, System.ReadOnlyMemory<byte>> sensitiveCredentials,
        IReadOnlyDictionary<string, JsonNode> nonSensitiveCredentials,
        CancellationToken ct)
    {
        if (!sensitiveCredentials.TryGetValue("api-key", out var apiKeyBytes) || apiKeyBytes.IsEmpty)
        {
            return Fail("missing-api-key", "api-key credential is required.");
        }

        if (!nonSensitiveCredentials.TryGetValue("base-url", out var baseUrlNode) ||
            baseUrlNode?.GetValue<string>() is not { Length: > 0 } baseUrlStr ||
            !Uri.TryCreate(baseUrlStr, UriKind.Absolute, out var baseUri))
        {
            return Fail("missing-base-url", "base-url credential is required and must be a valid absolute URI.");
        }

        var apiKey = Encoding.UTF8.GetString(apiKeyBytes.Span);
        try
        {
            using var http = _httpFactory.CreateClient();
            http.BaseAddress = baseUri;
            http.Timeout = TimeSpan.FromSeconds(5);
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var user = nonSensitiveCredentials.TryGetValue("user", out var userNode)
                ? userNode?.GetValue<string>()
                : null;
            var path = string.IsNullOrEmpty(user)
                ? "api/v1/node"
                : $"api/v1/node?user={Uri.EscapeDataString(user)}";

            using var response = await http.GetAsync(path, ct).ConfigureAwait(false);

            return response.StatusCode switch
            {
                HttpStatusCode.OK => Valid(),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    Fail("headscale-auth-failure", "The Headscale API key was rejected. Verify the key has node-read access."),
                _ => Fail("headscale-probe-failed", $"Headscale returned unexpected status {(int)response.StatusCode}.")
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Unreachable($"Could not reach Headscale at {baseUri}: {ex.Message}");
        }
        catch
        {
            return Unreachable($"Could not reach Headscale at {baseUri}.");
        }
        finally
        {
            // Zero the decrypted key bytes; they were passed as a ReadOnlyMemory slice
            // so we cannot zero the underlying buffer here — responsibility lies with
            // DefaultIntegrationAtlasProvider's finally block per ADR 0067 §Trust.
        }
    }

    private static IntegrationValidationResult Valid() =>
        new(ProviderValidationStatus.Valid, DateTimeOffset.UtcNow, null, null);

    private static IntegrationValidationResult Fail(string code, string message) =>
        new(ProviderValidationStatus.Invalid, DateTimeOffset.UtcNow, code, message);

    private static IntegrationValidationResult Unreachable(string message) =>
        new(ProviderValidationStatus.Unreachable, DateTimeOffset.UtcNow, "headscale-unreachable", message);
}
