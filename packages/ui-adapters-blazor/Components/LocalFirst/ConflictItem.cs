namespace Sunfish.UIAdapters.Blazor.Components.LocalFirst;

/// <summary>
/// A single pending conflict surfaced by <see cref="SunfishConflictList"/>
/// (paper §5.2 conflict list).
/// </summary>
/// <param name="Id">Stable identifier for the conflict row (used as <c>@key</c>).</param>
/// <param name="RecordId">Identifier of the record the conflict applies to.</param>
/// <param name="Kind">
/// Conflict category — one of <c>"merge"</c>, <c>"lease"</c>, <c>"schema-epoch"</c>.
/// The set is open-ended so downstream layers can add their own categories;
/// the component does not switch on it.
/// </param>
/// <param name="DetectedAt">When the replication layer detected the conflict.</param>
/// <param name="Description">Short human-readable description shown in the row.</param>
public sealed record ConflictItem(
    string Id,
    string RecordId,
    string Kind,
    DateTimeOffset DetectedAt,
    string Description);
