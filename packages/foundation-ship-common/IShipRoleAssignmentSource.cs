using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Materializes <see cref="ShipRoleAssignment"/> records from the durable
/// <c>IStandingOrderRepository</c> (or any equivalent backing store).
/// <see cref="DefaultPermissionResolver"/> caches results per tenant with
/// 60-second TTL per ADR 0077 §2.5 (Phase 1 fallback).
/// </summary>
/// <remarks>
/// <b>Why a separate interface:</b> the wire-format that materializes a
/// <see cref="ShipRoleAssignment"/> from a <c>StandingOrder</c>'s triple
/// payload is not specified verbatim in ADR 0077 §1.2 — implementations are
/// free to pick any JSON shape that round-trips the
/// <see cref="ShipRoleAssignment"/> fields. The resolver depends on this
/// interface so the materialization shape stays an implementation detail
/// of the consumer (typically the host's bootstrap layer).
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
}
