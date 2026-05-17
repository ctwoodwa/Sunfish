namespace Sunfish.Blocks.People.Foundation.Migration;

/// <summary>
/// Outcome of a single ERPNext source-record import attempt. Mirrors the
/// blocks-work-projects / blocks-work-orders convention. Producer-local
/// copy rather than a shared <c>foundation-migration</c> dep, because
/// (a) the enum is three-state and stable, and (b) a shared package
/// would couple every importer's release cadence — premature.
/// </summary>
public enum ImportOutcomeKind
{
    /// <summary>The source produced a freshly-created local record.</summary>
    Inserted,

    /// <summary>An existing local record was reconciled to a newer source version.</summary>
    Updated,

    /// <summary>A prior import already covers this source (Modified key matched).</summary>
    Skipped,

    /// <summary>The source could not be imported (validation, structural, or upstream constraint).</summary>
    Failed,
}

/// <summary>Bundled outcome + entity payload from an importer call.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Entity">The resolved local entity, or null on <see cref="ImportOutcomeKind.Failed"/>.</param>
/// <param name="Reason">Optional human-readable detail (audit log, dashboard surface).</param>
public sealed record ImportOutcome<T>(ImportOutcomeKind Kind, T? Entity, string? Reason);
