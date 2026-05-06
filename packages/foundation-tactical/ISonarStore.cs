using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Record-only alert store per ADR 0081 §2. Receives alerts with
/// <see cref="AlertRoutingPolicy.InformationalSonar"/> routing;
/// queryable by tenant. The Sonar surface does not raise operator
/// notifications — alerts here are background-rate observability
/// data.
/// </summary>
public interface ISonarStore
{
    /// <summary>Persist the alert. Implementations MUST be idempotent on <see cref="TacticalAlert.AlertId"/>; duplicate writes overwrite.</summary>
    ValueTask WriteAsync(TacticalAlert alert, CancellationToken ct = default);

    /// <summary>
    /// Snapshot of all currently-active Sonar alerts for the tenant.
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
    IReadOnlyList<TacticalAlert> GetActiveAlerts(TenantId tenantId);
}
