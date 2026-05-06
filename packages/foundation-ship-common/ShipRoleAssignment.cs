using System;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Wayfinder;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// A materialized role assignment per ADR 0077 §1.2 + §1.4. Composes on
/// ADR 0065 — a <see cref="ShipRoleAssignment"/> is derived from an
/// <see cref="StandingOrderState.Applied"/> Standing Order whose
/// <c>Triples</c> carry <c>ship.roles.{actorId}</c> path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Time fields use <see cref="DateTimeOffset"/></b> (W#49 cohort precedent
/// 2026-05-05 + W#34/W#35/W#40/W#41). The hand-off cited
/// <c>NodaTime.Instant</c> per the ADR; <c>NodaTime</c> is not yet on
/// <c>Directory.Packages.props</c>, and adding a new dependency is outside
/// COB scope. <see cref="DateTimeOffset"/> is the cohort-canonical wall-clock
/// type and the same convention every other built foundation package uses.
/// Migration to <c>NodaTime.Instant</c> would land via a single follow-up
/// ADR amendment touching every <c>Sunfish.Foundation.*</c> time-bearing
/// record at once.
/// </para>
/// <para>
/// <see cref="Division"/> is populated when <see cref="Role"/> is
/// <see cref="ShipRole.DivisionOfficer"/>, otherwise <c>null</c>.
/// <see cref="RotatesAt"/> is populated for Division Officer rotations per
/// §1.4; null for static role assignments.
/// </para>
/// </remarks>
/// <param name="TenantId">Tenant that owns this assignment.</param>
/// <param name="Holder">Actor holding the role.</param>
/// <param name="Role">Closed-enum role per ADR 0077 §1.</param>
/// <param name="Division">Sub-room assignment when <paramref name="Role"/> is <see cref="ShipRole.DivisionOfficer"/>; otherwise null.</param>
/// <param name="AssignedAt">Wall-clock time the role was assigned.</param>
/// <param name="RotatesAt">Next rotation tick for Division Officers per §1.4; null for static roles.</param>
/// <param name="IssuedBy">Back-reference to the <see cref="StandingOrder"/> that assigned this role.</param>
public sealed record ShipRoleAssignment(
    TenantId TenantId,
    ActorId Holder,
    ShipRole Role,
    DivisionAssignment? Division,
    DateTimeOffset AssignedAt,
    DateTimeOffset? RotatesAt,
    StandingOrderId IssuedBy) : IMustHaveTenant;
