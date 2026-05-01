using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Providers.Mesh.Headscale;

/// <summary>
/// Thin HTTP client over the Headscale v0.x REST API. Pure REST —
/// no <c>Headscale-Sharp</c> / <c>Tailscale.Net</c> SDK dependency,
/// per ADR 0013 provider neutrality + the existing
/// <c>providers-recaptcha</c> precedent.
/// </summary>
/// <remarks>
/// Endpoints covered:
/// <list type="bullet">
///   <item><c>GET /health</c> — control-plane liveness probe.</item>
///   <item><c>GET /api/v1/node</c> — node list (read for resolution + status).</item>
///   <item><c>POST /api/v1/node/register</c> — register a pre-authed Sunfish peer device.</item>
/// </list>
/// Auth is <c>Authorization: Bearer &lt;ApiKey&gt;</c> on every request.
/// </remarks>
public class HeadscaleClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly HeadscaleOptions _options;

    public HeadscaleClient(HttpClient http, HeadscaleOptions options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options;
        _http.BaseAddress ??= options.BaseUrl;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        if (_http.Timeout == TimeSpan.FromSeconds(100) /* HttpClient default */)
        {
            _http.Timeout = options.RequestTimeout;
        }
    }

    /// <summary>
    /// Probes <c>GET /health</c>. Returns true on a 2xx; any other
    /// status (including transport exceptions) is false.
    /// </summary>
    public virtual async Task<bool> HealthCheckAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync("health", ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Lists nodes registered under the configured Headscale user.</summary>
    public virtual async Task<IReadOnlyList<HeadscaleNode>> ListNodesAsync(CancellationToken ct)
    {
        var path = string.IsNullOrEmpty(_options.User)
            ? "api/v1/node"
            : $"api/v1/node?user={Uri.EscapeDataString(_options.User)}";
        var response = await _http.GetFromJsonAsync<HeadscaleNodeList>(path, JsonOptions, ct).ConfigureAwait(false);
        return response?.Nodes ?? Array.Empty<HeadscaleNode>();
    }

    /// <summary>
    /// Registers a Sunfish peer with Headscale. The Sunfish
    /// <c>PeerId</c> is encoded into a <c>tag:sunfish-peer-…</c>
    /// ACL tag so subsequent <see cref="ListNodesAsync"/> calls can
    /// match.
    /// </summary>
    public virtual async Task<HeadscaleNode> RegisterNodeAsync(HeadscaleRegisterRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var resp = await _http.PostAsJsonAsync("api/v1/node/register", request, JsonOptions, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var registered = await resp.Content.ReadFromJsonAsync<HeadscaleNode>(JsonOptions, ct).ConfigureAwait(false)
            ?? throw new HttpRequestException("Headscale POST /api/v1/node/register returned an empty body.");
        return registered;
    }
}

/// <summary>Wire shape for <c>GET /api/v1/node</c>.</summary>
public sealed record HeadscaleNodeList
{
    [JsonPropertyName("nodes")]
    public IReadOnlyList<HeadscaleNode> Nodes { get; init; } = Array.Empty<HeadscaleNode>();
}

/// <summary>Wire shape for one node in the Headscale v0.x REST API.</summary>
public sealed record HeadscaleNode
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("ip_addresses")]
    public IReadOnlyList<string> IpAddresses { get; init; } = Array.Empty<string>();

    [JsonPropertyName("online")]
    public bool Online { get; init; }

    [JsonPropertyName("last_seen")]
    public DateTimeOffset? LastSeen { get; init; }

    [JsonPropertyName("forced_tags")]
    public IReadOnlyList<string> ForcedTags { get; init; } = Array.Empty<string>();
}

/// <summary>Wire shape for <c>POST /api/v1/node/register</c>.</summary>
public sealed record HeadscaleRegisterRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("user")]
    public string? User { get; init; }

    [JsonPropertyName("forced_tags")]
    public required IReadOnlyList<string> ForcedTags { get; init; }
}
