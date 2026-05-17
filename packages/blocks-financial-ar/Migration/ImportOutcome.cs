namespace Sunfish.Blocks.FinancialAr.Migration;

/// <summary>
/// Outcome of a single ERPNext source-record import attempt. Mirrors the
/// sibling-importer convention in blocks-people-foundation /
/// blocks-work-projects / blocks-work-orders. Local copy rather than a
/// shared <c>foundation-migration</c> dep — three-state enum + 1 entity
/// ref isn't worth a cross-cluster coupling yet.
/// </summary>
public enum ImportOutcomeKind
{
    /// <summary>Source produced a freshly-created local record.</summary>
    Inserted,

    /// <summary>An existing record was reconciled to a newer source version.</summary>
    Updated,

    /// <summary>A prior import already covers this source (Modified key matched).</summary>
    Skipped,

    /// <summary>Source could not be imported (validation, structural, or upstream constraint).</summary>
    Failed,
}

/// <summary>Bundled outcome + entity payload from an importer call.</summary>
public sealed record ImportOutcome<T>(ImportOutcomeKind Kind, T? Entity, string? Reason);
