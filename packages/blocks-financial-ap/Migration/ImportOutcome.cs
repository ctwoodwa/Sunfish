namespace Sunfish.Blocks.FinancialAp.Migration;

/// <summary>
/// Outcome of a single ERPNext source-record import attempt. Mirrors
/// the convention used by blocks-financial-ar /
/// blocks-people-foundation / blocks-work-projects.
/// </summary>
public enum ImportOutcomeKind
{
    /// <summary>Source produced a freshly-created local record.</summary>
    Inserted,

    /// <summary>An existing record was reconciled to a newer source version.</summary>
    Updated,

    /// <summary>A prior import already covers this source (Modified key matched).</summary>
    Skipped,

    /// <summary>Source could not be imported.</summary>
    Failed,
}

/// <summary>Bundled outcome + entity payload from an importer call.</summary>
public sealed record ImportOutcome<T>(ImportOutcomeKind Kind, T? Entity, string? Reason);
