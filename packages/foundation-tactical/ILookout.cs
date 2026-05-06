using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Operator-visible alert surface per ADR 0081 §2. Receives alerts
/// with <see cref="AlertRoutingPolicy.HighPriorityLookout"/> routing;
/// surfaced to the Tactical UI in near-real-time via
/// <see cref="SubscribeLookoutAsync"/>.
/// </summary>
public interface ILookout
{
    /// <summary>Persist + raise the alert. Implementations MUST be idempotent on <see cref="TacticalAlert.AlertId"/>.</summary>
    ValueTask WriteAsync(TacticalAlert alert, CancellationToken ct = default);

    /// <summary>Snapshot of currently-active Lookout alerts for the tenant. Phase 2 enforces tenant-binding.</summary>
    IReadOnlyList<TacticalAlert> GetActiveLookoutAlerts(TenantId tenantId);

    /// <summary>
    /// Stream Lookout-alert lists for the tenant. Yields when:
    /// (a) a new alert is written; (b) an alert expires or is
    /// superseded; (c) on heartbeat
    /// (<see cref="TacticalOptions.HeartbeatInterval"/>).
    /// Acknowledgement-status changes do NOT yield immediately —
    /// they yield on the next heartbeat per ADR 0081 §2.
    /// </summary>
    IAsyncEnumerable<IReadOnlyList<TacticalAlert>> SubscribeLookoutAsync(
        TenantId tenantId,
        CancellationToken ct = default);
}
