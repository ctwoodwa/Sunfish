using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Sunfish.Bridge.Proxy;

public sealed class ERPNextHttpClient : IERPNextClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly ERPNextOptions _options;

    public ERPNextHttpClient(HttpClient http, IOptions<ERPNextOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<JsonElement> GetResourceListAsync(
        string doctype,
        string company,
        IDictionary<string, object>? extraFilters = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        var companyFilter = $"[[\"company\",\"=\",\"{EscapeJson(company)}\"]]";
        var url = $"/api/resource/{Uri.EscapeDataString(doctype)}" +
                  $"?filters={Uri.EscapeDataString(companyFilter)}" +
                  $"&limit_page_length={limit}";

        using var req = BuildRequest(HttpMethod.Get, url);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await ParseJsonAsync(resp, ct).ConfigureAwait(false);
    }

    public async Task<JsonElement> GetResourceAsync(
        string doctype,
        string name,
        string company,
        CancellationToken ct = default)
    {
        var url = $"/api/resource/{Uri.EscapeDataString(doctype)}/{Uri.EscapeDataString(name)}";
        using var req = BuildRequest(HttpMethod.Get, url);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var doc = await ParseJsonAsync(resp, ct).ConfigureAwait(false);

        // Defense: validate company matches to prevent cross-company data leakage.
        if (doc.TryGetProperty("data", out var data) &&
            data.TryGetProperty("company", out var recordCompany) &&
            !string.Equals(recordCompany.GetString(), company, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ERPNext record company '{recordCompany.GetString()}' does not match " +
                $"requested company '{company}'.");
        }

        return doc;
    }

    public async Task<JsonElement> PostAsync(
        string endpoint,
        object payload,
        string company,
        CancellationToken ct = default)
    {
        // Inject company into the payload by round-tripping through a dictionary.
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(payload, _jsonOptions), _jsonOptions)
            ?? new Dictionary<string, object>();
        dict["company"] = company;

        var json = JsonSerializer.Serialize(dict, _jsonOptions);
        using var req = BuildRequest(HttpMethod.Post, endpoint);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await ParseJsonAsync(resp, ct).ConfigureAwait(false);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("token", $"{_options.ApiKey}:{_options.ApiSecret}");
        // Frappe multi-site routing requires Host = site name (no port).
        req.Headers.TryAddWithoutValidation("Host", _options.SiteName);
        return req;
    }

    private static async Task<JsonElement> ParseJsonAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        return doc.RootElement.Clone();
    }

    private static string EscapeJson(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
}

public static class ERPNextServiceExtensions
{
    public static IServiceCollection AddERPNextClient(this IServiceCollection services)
    {
        services.AddHttpClient<IERPNextClient, ERPNextHttpClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ERPNextOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });
        return services;
    }
}
