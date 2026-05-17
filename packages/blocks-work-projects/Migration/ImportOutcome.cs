namespace Sunfish.Blocks.WorkProjects.Migration;

/// <summary>
/// Outcome of a single record's import attempt — used by the ERPNext
/// importer + future Wave / Frappe importers. Mirrors
/// <c>blocks-work-orders</c>'s sibling importer convention.
/// </summary>
public enum ImportOutcomeKind
{
    Inserted,
    Updated,
    Skipped,
    Failed,
}

/// <summary>Bundled outcome + entity payload from an importer.</summary>
public sealed record ImportOutcome<T>(ImportOutcomeKind Kind, T? Entity, string? Reason);
