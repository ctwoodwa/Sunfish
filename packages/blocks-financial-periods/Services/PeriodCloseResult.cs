using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Structured failure modes for <see cref="IPeriodCloseService"/>.
/// </summary>
public enum PeriodCloseError
{
    /// <summary>Success sentinel.</summary>
    None,

    /// <summary>The supplied <c>FiscalPeriodId</c> does not exist.</summary>
    PeriodNotFound,

    /// <summary>The period is already in <see cref="FiscalPeriodStatus.SoftClosed"/>.</summary>
    PeriodAlreadySoftClosed,

    /// <summary>The period is in <see cref="FiscalPeriodStatus.Locked"/> — soft-close or reopen-soft is not the right path.</summary>
    PeriodLocked,

    /// <summary>The owning <see cref="FiscalYear"/> is <see cref="FiscalYearStatus.Closed"/>; reopen is rejected until the year is reopened.</summary>
    FiscalYearAlreadyClosed,

    /// <summary>Audit memo is required for the reopen path; caller passed a null / whitespace memo.</summary>
    AuditMemoRequired,
}

/// <summary>
/// Outcome of a <see cref="IPeriodCloseService"/> call. On success
/// <see cref="Period"/> contains the updated row and <see cref="Error"/>
/// is <see cref="PeriodCloseError.None"/>.
/// </summary>
/// <param name="Period">Updated row on success; the unmodified row (if any) on validation failure.</param>
/// <param name="Error">Failure mode, or <see cref="PeriodCloseError.None"/> on success.</param>
/// <param name="Detail">Free-form detail string (e.g., the rejected period id) on failure; null on success.</param>
public readonly record struct PeriodCloseResult(
    FiscalPeriod? Period,
    PeriodCloseError Error,
    string? Detail)
{
    /// <summary>True when <see cref="Error"/> is <see cref="PeriodCloseError.None"/>.</summary>
    public bool IsSuccess => Error == PeriodCloseError.None;
}
