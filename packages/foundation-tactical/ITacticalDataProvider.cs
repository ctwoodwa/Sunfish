using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Read-side surface aggregating alerts + incidents + capability
/// flags into a <see cref="TacticalSnapshot"/> for the Tactical UI.
/// Per ADR 0081 §1 + §2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant binding (§8.2):</b> implementations MUST verify that
/// the supplied <c>actor</c>'s tenant matches the supplied
/// <c>tenantId</c> argument before resolving any per-tenant state.
/// Cross-tenant calls MUST throw
/// <see cref="TacticalUnauthorizedException"/>.
/// </para>
/// <para>
/// <b>Permission pre-resolution:</b> the implementation MUST
/// resolve every snapshot capability flag
/// (<see cref="TacticalSnapshot.CanAccessFireControl"/>,
/// <see cref="TacticalSnapshot.CanAcknowledgeAlerts"/>) at snapshot
/// time so the UI never re-resolves permissions; permission cache
/// keys MUST include <c>TenantId</c> per §8.2 anti-spoofing.
/// </para>
/// </remarks>
public interface ITacticalDataProvider
{
    /// <summary>Assemble + return one <see cref="TacticalSnapshot"/>.</summary>
    ValueTask<TacticalSnapshot> GetSnapshotAsync(
        TenantId tenantId,
        Principal actor,
        CancellationToken ct = default);

    /// <summary>
    /// Read alerts for the tenant + actor, optionally filtered to a
    /// specific routing policy. Used by the Tactical UI to query
    /// either the Sonar (record-only) or the Lookout
    /// (operator-visible) view.
    /// </summary>
    ValueTask<IReadOnlyList<TacticalAlert>> GetAlertsAsync(
        TenantId tenantId,
        Principal actor,
        AlertRoutingPolicy? filterPolicy = null,
        CancellationToken ct = default);

    /// <summary>Read currently-open incidents for the tenant.</summary>
    ValueTask<IReadOnlyList<IncidentRecord>> GetActiveIncidentsAsync(
        TenantId tenantId,
        Principal actor,
        CancellationToken ct = default);

    /// <summary>Stream <see cref="TacticalSnapshot"/> values per the heartbeat / state-change cadence in <see cref="TacticalOptions"/>.</summary>
    IAsyncEnumerable<TacticalSnapshot> SubscribeSnapshotAsync(
        TenantId tenantId,
        Principal actor,
        CancellationToken ct = default);
}
