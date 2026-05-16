using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Financial;

/// <summary>
/// Event payload emitted when a <see cref="FiscalYear"/> transitions
/// to <see cref="FiscalYearStatus.Closed"/> via
/// <c>IFiscalYearCloseService.CloseFiscalYearAsync</c> (Stage 02
/// §6.5(b)).
/// </summary>
/// <remarks>
/// Canonical event-type name: <c>Financial.YearClosed</c> per the
/// cross-cluster event-bus catalog (§3.1).
/// </remarks>
/// <param name="FiscalYearId">FK to the closed fiscal year.</param>
/// <param name="ChartId">FK to the owning chart of accounts.</param>
/// <param name="ClosingJournalEntryId">FK to the closing JE posted by the close routine; null when the year had zero activity and no JE was synthesized.</param>
public sealed record YearClosed(
    FiscalYearId FiscalYearId,
    ChartOfAccountsId ChartId,
    JournalEntryId? ClosingJournalEntryId);
