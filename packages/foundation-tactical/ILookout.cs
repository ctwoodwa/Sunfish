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

    /// <summary>
    /// Snapshot of currently-active Lookout alerts for the tenant.
    /// </summary>
    /// <remarks>
    /// <b>Tenant binding (§8.2) [normative]:</b> implementations MUST
    /// resolve the ambient
    /// <c>Sunfish.Foundation.MultiTenancy.ITenantContext.TenantId</c>
    /// and verify it equals the supplied <paramref name="tenantId"/>
    /// before reading any per-tenant state. On mismatch, throw
    /// <see cref="TacticalUnauthorizedException"/> and emit
    /// <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAuthorizationDenied"/>
    /// with <c>denialReason="tenant-mismatch"</c>. This method MUST NOT
    /// be invoked from a DI scope without an
    /// <c>ITenantContext</c> registered — Phase 2 startup wiring MUST
    /// fail fast on missing scope.
    /// </remarks>
    IReadOnlyList<TacticalAlert> GetActiveLookoutAlerts(TenantId tenantId);

    /// <summary>
    /// Stream Lookout-alert lists for the tenant. Yields when:
    /// (a) a new alert is written; (b) an alert expires or is
    /// superseded; (c) on heartbeat
    /// (<see cref="TacticalOptions.HeartbeatInterval"/>).
    /// Acknowledgement-status changes do NOT yield immediately —
    /// they yield on the next heartbeat per ADR 0081 §2.
    /// </summary>
    /// <remarks>
    /// <b>A11y live-region invariant (Phase 3a):</b> the
    /// near-real-time path of this stream feeds the assertive Lookout
    /// live region. Acknowledgement-status changes MUST NOT feed the
    /// assertive region — Phase 3a renderers route status-change
    /// updates through a separate polite channel per ADR 0081 §2 +
    /// §7.5. Backpressure: Phase 2 implementations use
    /// <c>Channel(capacity: 1, BoundedChannelFullMode.DropOldest)</c>
    /// per the cohort pattern; downstream consumers MUST be tolerant
    /// of dropped intermediate snapshots.
    /// </remarks>
    IAsyncEnumerable<IReadOnlyList<TacticalAlert>> SubscribeLookoutAsync(
        TenantId tenantId,
        CancellationToken ct = default);
}
