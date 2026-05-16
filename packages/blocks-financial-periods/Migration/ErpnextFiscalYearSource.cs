namespace Sunfish.Blocks.FinancialPeriods.Migration;

/// <summary>
/// Source-record shape for an ERPNext <c>Fiscal Year</c> doctype row
/// per Stage 02 §10.1. The importer consumes this shape directly;
/// callers shape ERPNext JSON responses into instances of this record.
/// </summary>
/// <param name="Name">
/// ERPNext <c>name</c> field — the stable doctype id. Persisted on the
/// resulting <c>FiscalYear</c>'s external-reference for trace +
/// idempotency.
/// </param>
/// <param name="Modified">
/// ERPNext <c>modified</c> field (ISO timestamp string). Used as the
/// version key for "should this re-import update the local row?"
/// decisions: lexicographic ordering matches the ISO timestamp
/// ordering.
/// </param>
/// <param name="YearStartDate">Inclusive FY start date (calendar).</param>
/// <param name="YearEndDate">Inclusive FY end date (calendar).</param>
/// <param name="CompanyShortName">
/// Optional ERPNext company short-name; if present, used to derive the
/// local <c>FiscalYear.Label</c>. Null falls back to a label derived
/// from <see cref="YearStartDate"/>.
/// </param>
/// <param name="IsShortYear">
/// ERPNext <c>is_short_year</c> flag; informational for the synthesized
/// period set in the period importer (a short FY's monthly periods
/// truncate at <see cref="YearEndDate"/> instead of spanning a full
/// month).
/// </param>
public sealed record ErpnextFiscalYearSource(
    string Name,
    string Modified,
    DateOnly YearStartDate,
    DateOnly YearEndDate,
    string? CompanyShortName,
    bool IsShortYear);
