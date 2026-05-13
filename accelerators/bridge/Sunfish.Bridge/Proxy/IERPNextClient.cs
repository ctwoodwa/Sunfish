using System.Text.Json;

namespace Sunfish.Bridge.Proxy;

public interface IERPNextClient
{
    Task<JsonElement> GetResourceListAsync(
        string doctype,
        string company,
        IDictionary<string, object>? extraFilters = null,
        int limit = 20,
        CancellationToken ct = default);

    Task<JsonElement> GetResourceAsync(
        string doctype,
        string name,
        string company,
        CancellationToken ct = default);

    Task<JsonElement> PostAsync(
        string endpoint,
        object payload,
        string company,
        CancellationToken ct = default);
}
