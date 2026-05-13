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

    /// <summary>
    /// GET /api/resource/{doctype} with explicit field selection.
    /// Needed for accounting endpoints where ERPNext's default minimal field set is insufficient.
    /// </summary>
    Task<JsonElement> GetListWithFieldsAsync(
        string doctype,
        string company,
        IReadOnlyList<string> fields,
        int limit = 100,
        CancellationToken ct = default);
}
