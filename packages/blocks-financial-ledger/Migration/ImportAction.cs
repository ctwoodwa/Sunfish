namespace Sunfish.Blocks.FinancialLedger.Migration;

/// <summary>
/// Per-record outcome marker for the ERPNext importer. Drives the
/// importer's audit log and the post-import reconciliation report.
/// </summary>
public enum ImportAction
{
    /// <summary>The source record had no prior import; a new local record was created.</summary>
    Inserted,

    /// <summary>An existing local record was updated to match a newer source version.</summary>
    Updated,

    /// <summary>The source record was already present at the same or newer version — no change.</summary>
    Skipped,
}
