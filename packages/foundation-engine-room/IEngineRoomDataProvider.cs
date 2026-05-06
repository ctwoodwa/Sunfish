using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Read-side data provider for the Engine Room observability surface per
/// ADR 0079 §2. Implementations live in <c>blocks-engine-room</c> (Phase 2);
/// this contract is the foundation-tier seam.
/// </summary>
/// <remarks>
/// <para>
/// <b>CALLER CONTRACT (per ADR 0079 §2):</b> callers MUST verify
/// <c>ShipAction.ViewEngineRoom</c> via <c>IPermissionResolver</c>
/// before invoking any method on this interface.
/// <c>ShipAction.ViewDamageControl</c> is required separately for
/// rendering the Damage Control panel (Phase 3b). The provider does not
/// re-verify role — it is the caller's (UI block's) responsibility to
/// gate access.
/// </para>
/// <para>
/// <b>Subscription heartbeat (per §2):</b>
/// <see cref="SubscribeHealthAsync"/> emits one
/// <see cref="EngineRoomHealthSummary"/> immediately on subscribe, then
/// on each status change, then every <c>HeartbeatInterval</c> (default
/// 30s — configured via <c>EngineRoomOptions</c> shipping with Phase 2
/// <c>blocks-engine-room</c>).
/// </para>
/// </remarks>
public interface IEngineRoomDataProvider
{
    /// <summary>
    /// Materialize the current Engine Room health summary for the supplied
    /// tenant. Pre-condition: caller has verified
    /// <c>ShipAction.ViewEngineRoom</c>.
    /// </summary>
    ValueTask<EngineRoomHealthSummary> GetHealthSummaryAsync(
        TenantId tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Materialize the current sync-daemon health snapshot for the
    /// supplied tenant. Pre-condition: caller has verified
    /// <c>ShipAction.ViewEngineRoom</c>.
    /// </summary>
    ValueTask<SyncDaemonHealth> GetSyncDaemonHealthAsync(
        TenantId tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Stream per-document CRDT growth metrics for the supplied tenant.
    /// Pre-condition: caller has verified
    /// <c>ShipAction.ViewEngineRoom</c>.
    /// </summary>
    IAsyncEnumerable<CrdtGrowthMetrics> GetCrdtGrowthMetricsAsync(
        TenantId tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Stream per-document CRDT growth metrics filtered by
    /// <paramref name="query"/>. Pre-condition: caller has verified
    /// <c>ShipAction.ViewEngineRoom</c> (and
    /// <c>ShipAction.ViewDamageControl</c> when the query restricts to
    /// <see cref="CrdtGrowthQuery.CompactionEligibleOnly"/>=true and the
    /// caller intends to surface Damage Control affordances).
    /// </summary>
    IAsyncEnumerable<CrdtGrowthMetrics> GetCrdtGrowthMetricsAsync(
        TenantId tenantId,
        CrdtGrowthQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribe to <see cref="EngineRoomHealthSummary"/> updates per the
    /// §2 heartbeat contract: one immediately on subscribe, one per
    /// status change, one per heartbeat interval.
    /// </summary>
    IAsyncEnumerable<EngineRoomHealthSummary> SubscribeHealthAsync(
        TenantId tenantId,
        CancellationToken ct = default);
}
