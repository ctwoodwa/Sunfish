namespace Sunfish.Blocks.FinancialLedger.Migration;

/// <summary>
/// Outcome of a single ERPNext-source-record upsert. Carries the
/// resolved local record + the <see cref="ImportAction"/> + an
/// optional human-readable detail string (for audit / dashboards).
/// </summary>
public sealed record ImportOutcome<T>(
    T Record,
    ImportAction Action,
    string? Detail);
