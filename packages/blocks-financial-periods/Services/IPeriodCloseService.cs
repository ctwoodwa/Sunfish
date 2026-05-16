using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Period-state transition service per Stage 02 §6.5(a). PR 2 covers
/// soft-close + reopen-soft; hard-close + year-end rollover land in
/// PR 3 via <c>IFiscalYearCloseService</c> (not yet defined).
/// </summary>
/// <remarks>
/// <para>
/// Implementations follow CRDT Pattern A — Designated authority per
/// <c>_shared/engineering/crdt-friendly-schema-conventions.md</c> §7:
/// the period-close action is performed by one designated replica;
/// observer replicas surface the propagated status change and do not
/// advance state locally.
/// </para>
/// <para>
/// Role-gating (e.g., admin-only reopen) is the caller's responsibility
/// — this service intentionally does not consult <c>IUserContext</c>
/// directly, so caller layers can choose their own authorization model.
/// </para>
/// </remarks>
public interface IPeriodCloseService
{
    /// <summary>
    /// Soft-close the period: postings remain rejected for regular users
    /// while admins (FinancialAdmin role, gated by the caller) may still
    /// post. Reversals remain allowed per Stage 02 §6.1 Phase 4.
    /// </summary>
    Task<PeriodCloseResult> SoftCloseAsync(
        FiscalPeriodId periodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopen a soft-closed period (admin-only, gated by the caller).
    /// Emits <c>Financial.PeriodOpened</c> with the audit memo as the
    /// reopen reason. Locked → SoftClosed transitions (unlock-with-audit)
    /// are PR 3's path; this overload only handles SoftClosed → Open.
    /// </summary>
    Task<PeriodCloseResult> ReopenAsync(
        FiscalPeriodId periodId,
        string auditMemo,
        CancellationToken cancellationToken = default);
}
