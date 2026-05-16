using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialPeriods.Models;

/// <summary>
/// A fiscal-year container per Stage 02 §3.15. Owns a set of
/// <see cref="FiscalPeriod"/>s (typically 12 monthly, 4 quarterly, or
/// 1 annual) that together cover [<see cref="StartDate"/>,
/// <see cref="EndDate"/>] (inclusive). Belongs to a single
/// <see cref="ChartOfAccountsId"/>.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="ChartId">FK to the chart this fiscal year belongs to.</param>
/// <param name="Label">Display label, e.g. <c>"2026"</c>, <c>"FY26"</c>, <c>"FY26 (Apr2026-Mar2027)"</c>.</param>
/// <param name="StartDate">Inclusive period start (UTC date-only).</param>
/// <param name="EndDate">Inclusive period end (UTC date-only).</param>
/// <param name="Status">Current lifecycle state.</param>
/// <param name="ClosedAtUtc">Wall-clock instant the year transitioned to <see cref="FiscalYearStatus.Closed"/>; null while Open.</param>
/// <param name="ClosingJournalEntryId">FK to the closing journal entry posted at year-end; null while Open or before the closing JE has been synthesized.</param>
/// <param name="CreatedAtUtc">Creation timestamp.</param>
/// <param name="Version">Optimistic-concurrency token bumped on every mutation by <c>IFiscalYearCloseService</c> (PR 3b); the repository uses it as the compare-and-swap predicate to surface concurrent-update races between admin sessions.</param>
/// <param name="ExternalRef">Optional external-system reference (e.g., ERPNext <c>Fiscal Year.name</c>) carried for round-trip provenance + idempotent import. Null when the FY was created locally. Set by the ERPNext importer in PR 4; queried via <c>IFiscalYearRepository.GetByExternalRefAsync</c>.</param>
/// <param name="ExternalModifiedAtUtc">External-system version timestamp paired with <see cref="ExternalRef"/> — e.g., ERPNext <c>modified</c>. Persisted on the row so a re-import after process restart correctly decides Skipped-vs-Updated without relying on in-process caches. Null when no external version is known.</param>
public sealed record FiscalYear(
    FiscalYearId Id,
    ChartOfAccountsId ChartId,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalYearStatus Status,
    Instant? ClosedAtUtc,
    JournalEntryId? ClosingJournalEntryId,
    Instant CreatedAtUtc,
    int Version = 0,
    string? ExternalRef = null,
    Instant? ExternalModifiedAtUtc = null)
{
    /// <summary>
    /// Build a fresh <see cref="FiscalYear"/> in the
    /// <see cref="FiscalYearStatus.Open"/> state with null close fields
    /// and <see cref="Version"/> = 0.
    /// </summary>
    public static FiscalYear CreateOpen(
        FiscalYearId id,
        ChartOfAccountsId chartId,
        string label,
        DateOnly startDate,
        DateOnly endDate,
        Instant? createdAtUtc = null,
        string? externalRef = null,
        Instant? externalModifiedAtUtc = null)
        => new(
            Id:                    id,
            ChartId:               chartId,
            Label:                 label,
            StartDate:             startDate,
            EndDate:               endDate,
            Status:                FiscalYearStatus.Open,
            ClosedAtUtc:           null,
            ClosingJournalEntryId: null,
            CreatedAtUtc:          createdAtUtc ?? Instant.Now,
            Version:               0,
            ExternalRef:           externalRef,
            ExternalModifiedAtUtc: externalModifiedAtUtc);

    /// <summary>
    /// Per Stage 02 §3.15 — run the shape invariants over this fiscal
    /// year. Returns an empty error list when valid.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (StartDate > EndDate)
            errors.Add($"StartDate {StartDate:O} must be <= EndDate {EndDate:O}.");
        if (string.IsNullOrWhiteSpace(Label))
            errors.Add("Label must not be empty.");
        if (Status == FiscalYearStatus.Closed && ClosedAtUtc is null)
            errors.Add("Closed FiscalYear must have a non-null ClosedAtUtc.");
        if (Status == FiscalYearStatus.Open && ClosedAtUtc is not null)
            errors.Add("Open FiscalYear must have a null ClosedAtUtc.");
        if (Status == FiscalYearStatus.Open && ClosingJournalEntryId is not null)
            errors.Add("Open FiscalYear must have a null ClosingJournalEntryId.");
        return errors;
    }
}
