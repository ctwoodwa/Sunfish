using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Filter + paging for the
/// <see cref="IEngineRoomDataProvider.GetCrdtGrowthMetricsAsync(TenantId, CrdtGrowthQuery, System.Threading.CancellationToken)"/>
/// overload per ADR 0079 §1.
/// </summary>
/// <param name="TenantId">Owning tenant; the provider scopes results to this tenant.</param>
/// <param name="CompactionEligibleOnly">When true, restrict to documents with <see cref="CrdtGrowthMetrics.CompactionEligible"/> = true.</param>
/// <param name="PageSize">Max results per page; null defers to provider default.</param>
/// <param name="ContinuationToken">Opaque continuation from a prior page; null for the first page. Implementations MUST treat invalid/stale tokens as first-page (no throw); cross-tenant tokens MUST be rejected as <see cref="System.ArgumentException"/>.</param>
public sealed record CrdtGrowthQuery(
    TenantId TenantId,
    bool? CompactionEligibleOnly = null,
    int? PageSize = null,
    string? ContinuationToken = null);
