namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// A single department KPI card surfaced on the Quarterdeck per
/// ADR 0080 §2.3 rule 9. Sourced from any registered
/// <see cref="IDepartmentKpiSource"/>; emitted to the snapshot after
/// permission filtering — denied actors see the card with
/// <see cref="DepartmentStatus.Denied"/> + null-coerced
/// <see cref="Value"/> rather than the card being hidden.
/// </summary>
/// <param name="SourceName">
/// Registered name of the originating <see cref="IDepartmentKpiSource"/>.
/// Used for source attribution + uniqueness validation at startup
/// (registered-prefix <c>"sunfish.*"</c> reserved for first-party
/// sources).
/// </param>
/// <param name="Label">
/// Localized human-readable KPI label (e.g., <c>"CRDT growth /
/// hour"</c>). The data provider does not localize here.
/// </param>
/// <param name="Value">
/// Pre-formatted KPI value (e.g., <c>"42.7"</c>, <c>"OK"</c>,
/// <c>"—"</c> when denied). The Quarterdeck UI renders this string
/// as-is.
/// </param>
/// <param name="Unit">
/// Optional unit suffix (e.g., <c>"MB/h"</c>, <c>"%"</c>). Null when
/// no unit applies.
/// </param>
/// <param name="Status">
/// Per-card access decision. <see cref="DepartmentStatus.Accessible"/>
/// renders the Value normally; <see cref="DepartmentStatus.Denied"/>
/// renders a denied affordance + denial reason; the source supplies a
/// neutral Value when status is denied (data provider does not mutate).
/// </param>
public sealed record DepartmentKpi(
    string SourceName,
    string Label,
    string Value,
    string? Unit,
    DepartmentStatus Status);
