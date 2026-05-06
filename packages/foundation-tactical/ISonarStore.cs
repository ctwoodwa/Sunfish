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

    /// <summary>Snapshot of all currently-active Sonar alerts for the tenant. Phase 2 enforces tenant-binding.</summary>
    IReadOnlyList<TacticalAlert> GetActiveAlerts(TenantId tenantId);
}
