using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Recently-issued Standing Order summary surfaced on the Quarterdeck
/// per ADR 0080 §2.3 rule 3. The data provider reads from
/// <c>IStandingOrderRepository.EnumerateAsync</c> and projects the
/// last 5 entries (newest first) into this lightweight DTO; the UI
/// links to the full Standing Order via <see cref="Id"/>.
/// </summary>
/// <param name="Id">Stable Standing Order identifier.</param>
/// <param name="Path">
/// Wayfinder path the order targets (e.g.,
/// <c>"/system/identity/keys/rotation"</c>). Rendered as the order's
/// breadcrumb on the Quarterdeck.
/// </param>
/// <param name="IssuedAt">Wall-clock timestamp when the order was issued.</param>
/// <param name="IssuedByDisplayName">
/// Display name of the actor who issued the order; not the actor's
/// principal id (the Quarterdeck never surfaces principal ids).
/// </param>
public sealed record StandingOrderSummary(
    StandingOrderId Id,
    string Path,
    DateTimeOffset IssuedAt,
    string IssuedByDisplayName);
