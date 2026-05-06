using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Materializes <see cref="ShipRoleAssignment"/> records from the durable
/// <c>IStandingOrderRepository</c> (or any equivalent backing store).
/// <see cref="DefaultPermissionResolver"/> caches results per tenant with
/// 60-second TTL per ADR 0077 §2.5 (Phase 1 fallback).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a separate interface:</b> the wire-format that materializes a
/// <see cref="ShipRoleAssignment"/> from a <c>StandingOrder</c>'s triple
/// payload is not specified verbatim in ADR 0077 §1.2 — implementations are
/// free to pick any JSON shape that round-trips the
/// <see cref="ShipRoleAssignment"/> fields. The resolver depends on this
/// interface so the materialization shape stays an implementation detail
/// of the consumer (typically the host's bootstrap layer).
/// </para>
/// <para>
/// <b>Tenant resolution:</b> <see cref="ResolveAssignmentAsync"/> is the
/// resolver's cold-path lookup when the per-tenant cache has no entry for
/// the actor. Implementations may scan every tenant the actor participates
/// in or maintain an
/// <c>(ActorId → TenantId)</c> index — both are valid Phase 1 strategies.
/// </para>
/// </remarks>
public interface IShipRoleAssignmentSource
{
    /// <summary>
    /// Materialize all role assignments for <paramref name="tenantId"/>.
    /// Called by the cache-warm path; results are cached with 60-second TTL.
    /// </summary>
    ValueTask<IReadOnlyList<ShipRoleAssignment>> LoadAssignmentsAsync(
        TenantId tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Cold-path resolution when the cache has no entry for
    /// <paramref name="actor"/>. Implementations resolve the
    /// <c>(ActorId → TenantId)</c> binding and return the matching
    /// assignment, or null when the actor has no assignment in any tenant.
    /// </summary>
    ValueTask<ShipRoleAssignment?> ResolveAssignmentAsync(
        ActorId actor,
        CancellationToken ct = default);
}
