using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// A fiscal accounting period. Local placeholder in PR 4 — full entity
/// (with chart FK, journal-entry rollups, close-event auditing) ships in
/// <c>blocks-financial-periods</c>. PR 4 only needs the
/// <see cref="Status"/> + <see cref="StartDate"/> / <see cref="EndDate"/>
/// to gate <see cref="Services.JournalPostingService"/>.
/// </summary>
/// <param name="Id">Unique period identifier.</param>
/// <param name="ChartId">FK to the chart this period belongs to.</param>
/// <param name="StartDate">Inclusive period start (UTC date-only).</param>
/// <param name="EndDate">Inclusive period end (UTC date-only).</param>
/// <param name="Status">Current lifecycle state.</param>
public sealed record FiscalPeriod(
    FiscalPeriodId Id,
    ChartOfAccountsId ChartId,
    DateOnly StartDate,
    DateOnly EndDate,
    FiscalPeriodStatus Status);
