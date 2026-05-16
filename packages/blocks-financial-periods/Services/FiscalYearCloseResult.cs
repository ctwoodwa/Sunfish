using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Structured failure modes for <see cref="IFiscalYearCloseService"/>.
/// </summary>
public enum FiscalYearCloseError
{
    /// <summary>Success sentinel.</summary>
    None,

    /// <summary>The supplied <see cref="FiscalYearId"/> does not exist.</summary>
    FiscalYearNotFound,

    /// <summary>The fiscal year is already in <see cref="FiscalYearStatus.Closed"/>.</summary>
    FiscalYearAlreadyClosed,

    /// <summary>Reopen target was already <see cref="FiscalYearStatus.Open"/>.</summary>
    FiscalYearAlreadyOpen,

    /// <summary>The chart of accounts lacks a designated <see cref="ChartOfAccounts.RetainedEarningsAccountId"/> — rollover destination is required.</summary>
    RetainedEarningsAccountNotConfigured,

    /// <summary>The chart referenced by the fiscal year does not exist in the chart repository.</summary>
    ChartNotFound,

    /// <summary>The closing journal entry failed to post (e.g., a mid-year period is unexpectedly Locked or the chart was misconfigured).</summary>
    ClosingJournalEntryFailed,

    /// <summary>The reversal journal entry for fiscal-year reopen failed to post.</summary>
    ReversalEntryFailed,

    /// <summary>Audit memo is required for the reopen path; caller passed a null / whitespace memo.</summary>
    AuditMemoRequired,

    /// <summary>The repository CAS rejected the fiscal-year update — another writer raced ahead.</summary>
    ConcurrentUpdate,
}

/// <summary>
/// Outcome of a <see cref="IFiscalYearCloseService"/> call. On success
/// <see cref="FiscalYear"/> is the updated row; <see cref="ClosingEntryId"/>
/// is the posted closing JE (null when the year had zero activity).
/// </summary>
/// <param name="FiscalYear">Updated fiscal-year row on success; pre-mutation row (if any) on validation failure; null when the lookup failed.</param>
/// <param name="ClosingEntryId">FK to the closing JE posted by the close routine; null when zero-activity year OR on failure paths.</param>
/// <param name="Error">Failure mode, or <see cref="FiscalYearCloseError.None"/> on success.</param>
/// <param name="Detail">Free-form detail string (e.g., the rejected fiscal-year id, the underlying PostError) on failure; null on success.</param>
public readonly record struct FiscalYearCloseResult(
    FiscalYear? FiscalYear,
    JournalEntryId? ClosingEntryId,
    FiscalYearCloseError Error,
    string? Detail)
{
    /// <summary>True when <see cref="Error"/> is <see cref="FiscalYearCloseError.None"/>.</summary>
    public bool IsSuccess => Error == FiscalYearCloseError.None;
}
