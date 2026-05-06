using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// Lightweight DTO for a recently-issued Standing Order surfaced
/// on the Helm <see cref="HelmSlot.ActivityFeed"/> by
/// <c>RecentStandingOrdersWidget</c>. Cycle-safe: lives in ui-core
/// and references only <c>foundation/Assets/Common</c> types
/// (<see cref="StandingOrderId"/> is in
/// <c>Sunfish.Foundation.Assets.Common</c> per W#48 P1.5 cycle-break).
/// Phase 3 will wire a <c>foundation-wayfinder</c>-backed
/// <see cref="IRecentStandingOrdersSource"/> that projects
/// <c>StandingOrder</c> records into this DTO at the boundary.
/// </summary>
/// <param name="StandingOrderId">Stable identifier of the order.</param>
/// <param name="Path">
/// Wayfinder path the order targets (e.g.,
/// <c>"system.network.offline"</c>). Rendered as the activity-feed
/// row's primary identifier.
/// </param>
/// <param name="IssuedAt">Wall-clock timestamp the order was issued.</param>
/// <param name="IssuedByDisplayName">
/// Display name of the issuing actor; not the principal id (the
/// activity feed never surfaces principal ids directly).
/// </param>
public sealed record RecentStandingOrderEntry(
    StandingOrderId StandingOrderId,
    string Path,
    DateTimeOffset IssuedAt,
    string IssuedByDisplayName);
