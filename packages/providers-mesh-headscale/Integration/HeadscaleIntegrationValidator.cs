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
/// The validator deliberately does NOT exercise <see cref="HeadscaleMeshAdapter"/>
/// — it issues its own probe so validation logic stays independent of the
/// runtime transport layer.
///
/// Credential key conventions (matches <see cref="HeadscaleIntegrationSchemaProvider"/>):
/// <list type="bullet">
/// <item><c>api-key</c> — sensitive (UTF-8 encoded); passed as Bearer token.</item>
/// <item><c>base-url</c> — non-sensitive JSON string; must be HTTPS or loopback.</item>
/// <item><c>user</c> — non-sensitive JSON string, optional.</item>
/// </list>
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

        // M1: GetString() avoids InvalidOperationException on wrong-typed JSON nodes
        var baseUrlStr = GetString(nonSensitiveCredentials.TryGetValue("base-url", out var baseUrlNode)
            ? baseUrlNode : null);
        if (string.IsNullOrEmpty(baseUrlStr) || !Uri.TryCreate(baseUrlStr, UriKind.Absolute, out var baseUri))
        {
            return Fail("missing-base-url", "base-url is required and must be a valid absolute URI.");
        }

        // B2: reject plaintext HTTP endpoints — prevents API-key leak over unencrypted transport
        if (!IsAllowedScheme(baseUri))
        {
            return Fail("insecure-base-url",
                "base-url must use HTTPS (HTTP is only permitted for loopback addresses).");
        }

        var apiKey = Encoding.UTF8.GetString(apiKeyBytes.Span);
        try
        {
            using var http = _httpFactory.CreateClient();
            http.BaseAddress = baseUri;
            http.Timeout = TimeSpan.FromSeconds(5);
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            // M1: GetString() is safe on wrong-typed or missing nodes
            var user = GetString(nonSensitiveCredentials.TryGetValue("user", out var userNode)
                ? userNode : null);
            var path = string.IsNullOrEmpty(user)
                ? "api/v1/node"
                : $"api/v1/node?user={Uri.EscapeDataString(user)}";

            using var response = await http.GetAsync(path, ct).ConfigureAwait(false);

            // m1: 5xx / 429 are server-side unavailability → Unreachable, not Invalid
            var statusInt = (int)response.StatusCode;
            return statusInt switch
            {
                200 => Valid(),
                401 or 403 => Fail("headscale-auth-failure",
                    "The Headscale API key was rejected. Verify the key has node-read access."),
                429 or >= 500 => Unreachable($"Headscale returned HTTP {statusInt}."),
                _ => Fail("headscale-probe-failed",
                    $"Headscale returned unexpected status {statusInt}."),
            };
        }
        // M3: distinguish HttpClient internal timeout from caller's CancellationToken;
        //     both throw OperationCanceledException but with different tokens.
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        {
            return Unreachable("Headscale probe timed out.");
        }
        // M2: narrow catch — only transport failures; programmer errors propagate
        catch (HttpRequestException)
        {
            // B2 Note: do NOT include ex.Message — it can embed raw bytes from the error
            return Unreachable($"Could not reach Headscale at {baseUri.Host}.");
        }
    }

    /// <summary>Safe JSON string extraction — returns null for non-string nodes (M1).</summary>
    private static string? GetString(JsonNode? node) =>
        node is JsonValue jv && jv.TryGetValue<string>(out var s) ? s : null;

    /// <summary>B2: HTTPS always allowed; HTTP only for loopback addresses.</summary>
    private static bool IsAllowedScheme(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps ||
        (uri.Scheme == Uri.UriSchemeHttp && IsLoopback(uri.Host));

    private static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        host == "127.0.0.1" ||
        host == "::1";

    private static IntegrationValidationResult Valid() =>
        new(ProviderValidationStatus.Valid, DateTimeOffset.UtcNow, null, null);

    private static IntegrationValidationResult Fail(string code, string message) =>
        new(ProviderValidationStatus.Invalid, DateTimeOffset.UtcNow, code, message);

    private static IntegrationValidationResult Unreachable(string message) =>
        new(ProviderValidationStatus.Unreachable, DateTimeOffset.UtcNow, "headscale-unreachable", message);
}
