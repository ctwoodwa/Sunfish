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

    /// <summary>Reopen target was already <see cref="FiscalPeriodStatus.Open"/>; reopen is a no-op state transition and rejected to surface the unexpected caller intent (e.g., a stale UI button).</summary>
    PeriodNotSoftClosed,

    /// <summary>The owning <see cref="FiscalYear"/> is <see cref="FiscalYearStatus.Closed"/>; reopen is rejected until the year is reopened.</summary>
    FiscalYearAlreadyClosed,

    /// <summary>Audit memo is required for the reopen / unlock path; caller passed a null / whitespace memo.</summary>
    AuditMemoRequired,

    /// <summary>
    /// The repository rejected the update because the stored
    /// <see cref="FiscalPeriod.Version"/> did not match the version the
    /// service loaded — another writer mutated the row between fetch
    /// and write. Callers should refetch and re-evaluate the
    /// transition.
    /// </summary>
    ConcurrentUpdate,

    /// <summary>
    /// Soft-close / lock target was already in the expected end state
    /// (e.g., <c>LockAsync</c> called on an already-Locked period).
    /// Distinct from <see cref="PeriodAlreadySoftClosed"/> for clarity.
    /// </summary>
    PeriodAlreadyLocked,

    /// <summary>Unlock target was not in <see cref="FiscalPeriodStatus.Locked"/>; surfaces a stale caller intent.</summary>
    PeriodNotLocked,
}

/// <summary>
/// Outcome of a <see cref="IPeriodCloseService"/> call. On success
/// <see cref="Period"/> contains the updated row and <see cref="Error"/>
/// is <see cref="PeriodCloseError.None"/>.
/// </summary>
/// <remarks>
/// <see cref="Period"/> is <c>null</c> when the row could not be loaded
/// (e.g., <see cref="PeriodCloseError.PeriodNotFound"/>) or when the
/// caller violated a pre-condition that runs before fetch
/// (e.g., <see cref="PeriodCloseError.AuditMemoRequired"/>); otherwise
/// it is the row at the time of the call (pre-mutation on validation
/// failure; post-mutation on success).
/// </remarks>
/// <param name="Period">See remarks for null semantics.</param>
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
