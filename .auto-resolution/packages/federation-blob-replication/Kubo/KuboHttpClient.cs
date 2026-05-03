using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sunfish.Federation.BlobReplication.Kubo;

/// <summary>
/// Default <see cref="IKuboHttpClient"/> — a thin wrapper over a named <see cref="HttpClient"/>
/// ("sunfish-kubo"). Uses <c>/api/v0/*</c> endpoints; all calls are POST (Kubo's RPC is POST-only).
/// </summary>
/// <remarks>
/// <para>
/// This client is intentionally a minimal mapping — no retry, circuit-breaker, or resilience
/// policies are applied here. Resilience is configured at the <see cref="IHttpClientFactory"/>
/// registration site (see <see cref="DependencyInjection.IpfsBlobStoreExtensions"/>) so that
/// applications can replace the policy chain without forking this client.
/// </para>
/// <para>
/// Error handling: all non-2xx responses call <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>
/// except for <see cref="CatAsync"/>, which maps "not found" style responses to <see langword="null"/>
/// so that <see cref="IpfsBlobStore.GetAsync"/> can surface a miss without throwing.
/// </para>
/// </remarks>
public sealed class KuboHttpClient : IKuboHttpClient
{
    /// <summary>Named-client identifier for <see cref="IHttpClientFactory"/>.</summary>
    public const string HttpClientName = "sunfish-kubo";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    /// <summary>Creates a client that issues requests through <paramref name="http"/>. The base
    /// address on <paramref name="http"/> must point at the Kubo RPC endpoint root (for example
    /// <c>http://localhost:5001/</c>). Individual methods append <c>api/v0/*</c> paths.</summary>
    public KuboHttpClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <inheritdoc />
    public async Task<KuboAddResponse> AddAsync(ReadOnlyMemory<byte> content, bool pin, CancellationToken ct)
    {
        var pinFlag = pin ? "true" : "false";
        var url = $"api/v0/add?cid-version=1&raw-leaves=true&pin={pinFlag}";

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        // Kubo requires a Content-Disposition with form-data; name="file"; filename="..."
        form.Add(fileContent, name: "file", fileName: "blob");

        using var response = await _http.PostAsync(url, form, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Kubo may stream NDJSON if multiple files were added; for single-file adds we take the
        // first JSON object.
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var firstLine = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        var parsed = JsonSerializer.Deserialize<KuboAddResponse>(firstLine, JsonOptions)
            ?? throw new InvalidOperationException($"Kubo /api/v0/add returned empty body: '{body}'.");
        return parsed;
    }

    /// <inheritdoc />
    public async Task<byte[]?> CatAsync(string cid, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cid);

        var url = $"api/v0/cat?arg={Uri.EscapeDataString(cid)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        // Kubo returns 500 with a JSON {"Message":"...","Code":0,"Type":"error"} body when the
        // block isn't available. It also uses 404 on some versions. Map either to null.
        if (response.StatusCode == HttpStatusCode.NotFound
            || response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (LooksLikeNotFound(errorBody))
            {
                return null;
            }
            // Otherwise surface the error.
            throw new HttpRequestException(
                $"Kubo /api/v0/cat returned {(int)response.StatusCode}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<KuboPinResponse> PinAddAsync(string cid, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cid);
        var url = $"api/v0/pin/add?arg={Uri.EscapeDataString(cid)}";
        return await PostEmptyAsync<KuboPinResponse>(url, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<KuboPinResponse> PinRmAsync(string cid, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cid);
        var url = $"api/v0/pin/rm?arg={Uri.EscapeDataString(cid)}";
        return await PostEmptyAsync<KuboPinResponse>(url, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<KuboPinListResponse> PinListAsync(string? cid, CancellationToken ct)
    {
        var url = cid is null
            ? "api/v0/pin/ls"
            : $"api/v0/pin/ls?arg={Uri.EscapeDataString(cid)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

        // When the CID is not pinned Kubo returns 500 with a {"Message":"...","Code":0,...} body
        // rather than an empty Keys map. Treat that as "no pinned keys".
        if (cid is not null && response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (LooksLikeNotFound(errorBody) || errorBody.Contains("not pinned", StringComparison.OrdinalIgnoreCase))
            {
                return new KuboPinListResponse(new Dictionary<string, KuboPinListEntry>());
            }
            throw new HttpRequestException(
                $"Kubo /api/v0/pin/ls returned {(int)response.StatusCode}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
        var parsed = await response.Content.ReadFromJsonAsync<KuboPinListResponse>(JsonOptions, ct)
            .ConfigureAwait(false);
        return parsed ?? new KuboPinListResponse(new Dictionary<string, KuboPinListEntry>());
    }

    /// <inheritdoc />
    public Task<KuboIdResponse> IdAsync(CancellationToken ct)
        => PostEmptyAsync<KuboIdResponse>("api/v0/id", ct);

    /// <inheritdoc />
    public Task<KuboConfigResponse> GetConfigAsync(CancellationToken ct)
        => PostEmptyAsync<KuboConfigResponse>("api/v0/config/show", ct);

    /// <inheritdoc />
    public Task<KuboSwarmPeersResponse> SwarmPeersAsync(CancellationToken ct)
        => PostEmptyAsync<KuboSwarmPeersResponse>("api/v0/swarm/peers", ct);

    private async Task<T> PostEmptyAsync<T>(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var parsed = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
        return parsed ?? throw new InvalidOperationException(
            $"Kubo {url} returned an empty or null JSON body.");
    }

    private static bool LooksLikeNotFound(string body)
        => body.Contains("not found", StringComparison.OrdinalIgnoreCase)
        || body.Contains("no such", StringComparison.OrdinalIgnoreCase)
        || body.Contains("dag: not found", StringComparison.OrdinalIgnoreCase);
}
