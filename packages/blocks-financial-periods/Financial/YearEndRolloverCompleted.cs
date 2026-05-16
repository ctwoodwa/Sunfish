using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Financial;

/// <summary>
/// Companion to <see cref="YearClosed"/> emitted after the retained-
/// earnings rollover finishes. Carries the net-income computation
/// outputs so downstream consumers (reports cluster, the year-over-
/// year compare in apps/docs, the dashboard panel) can reconstruct
/// the financial outcome without re-querying the ledger.
/// </summary>
/// <remarks>
/// Canonical event-type name: <c>Financial.YearEndRolloverCompleted</c>.
/// New event added to the cross-cluster event-bus catalog (§3.1) in
/// this PR.
/// </remarks>
/// <param name="FiscalYearId">FK to the closed fiscal year.</param>
/// <param name="ChartId">FK to the owning chart of accounts.</param>
/// <param name="ClosingJournalEntryId">FK to the posted closing JE; null when the year had zero activity.</param>
/// <param name="NetIncome">Income − Expense across the closed year. Positive = profit (credited to retained earnings); negative = loss (debited from retained earnings); zero = no activity.</param>
/// <param name="IncomeAccountsClosed">Count of Revenue accounts zeroed in the closing JE (including zero-balance accounts skipped in the JE).</param>
/// <param name="ExpenseAccountsClosed">Count of Expense accounts zeroed in the closing JE.</param>
public sealed record YearEndRolloverCompleted(
    FiscalYearId FiscalYearId,
    ChartOfAccountsId ChartId,
    JournalEntryId? ClosingJournalEntryId,
    decimal NetIncome,
    int IncomeAccountsClosed,
    int ExpenseAccountsClosed);
