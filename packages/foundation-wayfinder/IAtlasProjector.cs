using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Projects per-tenant Standing Order logs into a queryable settings catalog.
/// Per ADR 0065 §5.
/// </summary>
/// <remarks>
/// Phase 3a ships the in-process <see cref="DefaultAtlasProjector"/>; Phase 3b
/// adds a Roslyn analyzer (<c>SUNFISH_WAYFINDER001</c>) that warns when an
/// <c>AddSunfish*()</c>-registering project doesn't declare an
/// <see cref="AtlasSchemaDescriptor"/> for at least one settable path.
/// </remarks>
public interface IAtlasProjector
{
    /// <summary>
    /// Compute the current LWW-resolved view of a tenant's Standing Order log,
    /// optionally restricted to a single <see cref="StandingOrderScope"/>.
    /// </summary>
    /// <param name="tenantId">Tenant to project.</param>
    /// <param name="scopeFilter">When non-null, only orders matching the scope contribute to the projection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The projected view; <see cref="AtlasView.SettingsByPath"/> may be empty.</returns>
    ValueTask<AtlasView> ProjectAsync(
        TenantId tenantId,
        StandingOrderScope? scopeFilter,
        CancellationToken ct);

    /// <summary>
    /// Stream search hits matching <paramref name="query"/> against a tenant's
    /// projected catalog. Hits are streamed in descending
    /// <see cref="AtlasSearchHit.Score"/> order.
    /// </summary>
    /// <param name="tenantId">Tenant to search within.</param>
    /// <param name="query">Free-text search query; matched against path + display name.</param>
    /// <param name="limit">Maximum number of hits to return.</param>
    /// <param name="ct">Cancellation token; cancelling ends enumeration cleanly.</param>
    IAsyncEnumerable<AtlasSearchHit> SearchAsync(
        TenantId tenantId,
        string query,
        int limit,
        CancellationToken ct);
}
