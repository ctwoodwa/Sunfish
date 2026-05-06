using System.Collections.Generic;
using System.Threading;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.EngineRoom;

namespace Sunfish.Blocks.EngineRoom;

/// <summary>
/// Optional host-side hook exposing per-document CRDT growth metrics to
/// <see cref="DefaultEngineRoomDataProvider"/>. Hosts that run a real
/// CRDT document store implement this; hosts that don't leave the
/// registration unfilled and the data provider streams an empty result
/// set per W#50 Phase 2 fallback contract.
/// </summary>
public interface ICrdtDocumentRegistry
{
    /// <summary>
    /// Streams growth metrics for the supplied tenant. Implementations
    /// MUST scope results to <paramref name="tenantId"/>; cross-tenant
    /// leakage is a §Trust violation. The optional
    /// <paramref name="query"/> overload constrains by
    /// <see cref="CrdtGrowthQuery.CompactionEligibleOnly"/> and / or
    /// <see cref="CrdtGrowthQuery.PageSize"/>.
    /// </summary>
    IAsyncEnumerable<CrdtGrowthMetrics> StreamMetricsAsync(
        TenantId tenantId,
        CrdtGrowthQuery? query = null,
        CancellationToken ct = default);
}
