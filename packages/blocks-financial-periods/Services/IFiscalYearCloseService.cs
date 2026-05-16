using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Year-end close + reopen orchestration per Stage 02 §6.5(b). Closes
/// a <see cref="FiscalYear"/> by:
/// <list type="number">
/// <item>Auto-soft-closing any remaining Open periods (delegates to
/// <see cref="IPeriodCloseService.SoftCloseAsync"/>).</item>
/// <item>Building a balanced closing <see cref="JournalEntry"/> that
/// zeroes Revenue + Expense accounts into the chart's
/// <see cref="ChartOfAccounts.RetainedEarningsAccountId"/> destination.</item>
/// <item>Posting that JE via the ledger's
/// <c>IJournalPostingService</c>.</item>
/// <item>Locking all periods + flipping FY status to
/// <see cref="FiscalYearStatus.Closed"/>.</item>
/// <item>Emitting <c>Financial.PeriodLocked</c> (per period) +
/// <c>Financial.YearClosed</c> + <c>Financial.YearEndRolloverCompleted</c>
/// events through the canonical envelope.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// <b>Authorization warning:</b> callers MUST enforce
/// <c>FinancialAdmin</c> role gating before invoking
/// <see cref="CloseFiscalYearAsync"/> / <see cref="ReopenFiscalYearAsync"/>;
/// this service does NOT consult <c>IUserContext</c> directly. Same
/// discipline as <see cref="IPeriodCloseService"/>.
/// </para>
/// <para>
/// <b>Posting-service role requirement:</b> the injected
/// <c>IJournalPostingService</c> MUST be constructed against an
/// <c>IUserContext</c> carrying the <c>FinancialAdmin</c> role (or
/// the equivalent system actor) — the closing JE's
/// <c>entryDate = fy.EndDate</c> falls inside a SoftClosed period by
/// the time it posts (step 1 auto-soft-closes all open periods), and
/// only admin posts bypass the period gate. A host wiring a
/// per-request scoped <c>IUserContext</c> from an HTTP principal
/// will see the closing JE rejected as <c>PeriodSoftClosed</c>.
/// </para>
/// </remarks>
public interface IFiscalYearCloseService
{
    /// <summary>
    /// Close the fiscal year. Returns
    /// <see cref="FiscalYearCloseError.FiscalYearAlreadyClosed"/> if
    /// the year is already Closed,
    /// <see cref="FiscalYearCloseError.RetainedEarningsAccountNotConfigured"/>
    /// if the chart lacks the rollover destination, and
    /// <see cref="FiscalYearCloseError.ClosingJournalEntryFailed"/>
    /// if the synthesized JE fails to post (Detail carries the
    /// underlying <c>PostError</c>).
    /// </summary>
    Task<FiscalYearCloseResult> CloseFiscalYearAsync(
        FiscalYearId fiscalYearId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopen a closed fiscal year (admin-only, gated by the caller).
    /// Posts a reversal of the closing JE (if one exists), unlocks all
    /// periods back to SoftClosed (re-stamping their SoftClosedAtUtc),
    /// and flips FY status to Open. Emits PeriodOpened per unlocked
    /// period (carrying the audit memo as the reason).
    /// </summary>
    Task<FiscalYearCloseResult> ReopenFiscalYearAsync(
        FiscalYearId fiscalYearId,
        string auditMemo,
        CancellationToken cancellationToken = default);
}
