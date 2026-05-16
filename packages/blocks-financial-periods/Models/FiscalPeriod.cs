using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialPeriods.Models;

/// <summary>
/// One period (Monthly / Quarterly / Annual / Custom) within a
/// <see cref="FiscalYear"/> per Stage 02 §3.16. Re-uses the
/// <see cref="FiscalPeriodId"/> id-type from
/// <c>blocks-financial-ledger</c> so <c>JournalEntry.PeriodId</c> can
/// FK-reference periods without a cross-cluster type duplication.
/// </summary>
/// <param name="Id">Unique identifier (re-used from blocks-financial-ledger).</param>
/// <param name="ChartId">FK to the chart this period belongs to.</param>
/// <param name="FiscalYearId">FK to the parent fiscal year.</param>
/// <param name="Kind">Subdivision pattern (Monthly / Quarterly / Annual / Custom).</param>
/// <param name="Label">Display label, e.g. <c>"2026-M01"</c>, <c>"Q1-2026"</c>, <c>"FY2026"</c>.</param>
/// <param name="StartDate">Inclusive period start (UTC date-only).</param>
/// <param name="EndDate">Inclusive period end (UTC date-only).</param>
/// <param name="Status">Current lifecycle state.</param>
/// <param name="SoftClosedAtUtc">Wall-clock instant the period transitioned to SoftClosed; null while Open.</param>
/// <param name="LockedAtUtc">Wall-clock instant the period transitioned to Locked; null while Open / SoftClosed.</param>
/// <param name="ClosingJournalEntryId">FK to the period-close journal entry if one was posted; null otherwise.</param>
/// <param name="CreatedAtUtc">Creation timestamp.</param>
/// <param name="Version">Optimistic-concurrency token bumped on every mutation by <c>IPeriodCloseService</c>; the repository uses it as the compare-and-swap predicate to surface <c>PeriodCloseError.ConcurrentUpdate</c> on racing writers (cross-window admin races, batch close jobs).</param>
public sealed record FiscalPeriod(
    FiscalPeriodId Id,
    ChartOfAccountsId ChartId,
    FiscalYearId FiscalYearId,
    FiscalPeriodKind Kind,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalPeriodStatus Status,
    Instant? SoftClosedAtUtc,
    Instant? LockedAtUtc,
    JournalEntryId? ClosingJournalEntryId,
    Instant CreatedAtUtc,
    int Version = 0)
{
    /// <summary>
    /// Build a fresh <see cref="FiscalPeriod"/> in the
    /// <see cref="FiscalPeriodStatus.Open"/> state with null close fields
    /// and <see cref="Version"/> = 0.
    /// </summary>
    public static FiscalPeriod CreateOpen(
        FiscalPeriodId id,
        ChartOfAccountsId chartId,
        FiscalYearId fiscalYearId,
        FiscalPeriodKind kind,
        string label,
        DateOnly startDate,
        DateOnly endDate,
        Instant? createdAtUtc = null)
        => new(
            Id:                    id,
            ChartId:               chartId,
            FiscalYearId:          fiscalYearId,
            Kind:                  kind,
            Label:                 label,
            StartDate:             startDate,
            EndDate:               endDate,
            Status:                FiscalPeriodStatus.Open,
            SoftClosedAtUtc:       null,
            LockedAtUtc:           null,
            ClosingJournalEntryId: null,
            CreatedAtUtc:          createdAtUtc ?? Instant.Now,
            Version:               0);

    /// <summary>
    /// <c>true</c> if <paramref name="date"/> falls within
    /// [<see cref="StartDate"/>, <see cref="EndDate"/>] inclusive.
    /// </summary>
    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    /// <summary>
    /// Per Stage 02 §3.16 — run the per-row invariants over this
    /// period. Returns an empty error list when valid.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (StartDate > EndDate)
            errors.Add($"StartDate {StartDate:O} must be <= EndDate {EndDate:O}.");
        if (string.IsNullOrWhiteSpace(Label))
            errors.Add("Label must not be empty.");
        if (Status == FiscalPeriodStatus.SoftClosed && SoftClosedAtUtc is null)
            errors.Add("SoftClosed period must have a non-null SoftClosedAtUtc.");
        if (Status == FiscalPeriodStatus.Locked && (SoftClosedAtUtc is null || LockedAtUtc is null))
            errors.Add("Locked period must have non-null SoftClosedAtUtc + LockedAtUtc.");
        if (Status == FiscalPeriodStatus.Open && (SoftClosedAtUtc is not null || LockedAtUtc is not null))
            errors.Add("Open period must have null SoftClosedAtUtc + LockedAtUtc.");
        return errors;
    }
}
