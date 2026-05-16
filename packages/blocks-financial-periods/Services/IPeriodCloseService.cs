using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Period-state transition service per Stage 02 §6.5(a) + §8.5 row 3.
/// PR 2 shipped soft-close + reopen-soft; PR 3a adds lock + unlock;
/// year-end rollover lands in PR 3b via <c>IFiscalYearCloseService</c>.
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
/// <b>Authorization warning:</b> callers MUST enforce <c>FinancialAdmin</c>
/// role gating before invoking <see cref="SoftCloseAsync"/> /
/// <see cref="ReopenAsync"/> / <see cref="LockAsync"/> /
/// <see cref="UnlockAsync"/>; this service intentionally does NOT consult
/// <c>IUserContext</c> directly so caller layers can choose their own
/// authorization model (UI middleware, MediatR pipeline, attribute, etc.).
/// Wiring at the Anchor / Bridge UI surface must gate these methods.
/// </para>
/// </remarks>
public interface IPeriodCloseService
{
    /// <summary>
    /// Soft-close the period: postings remain rejected for regular users
    /// while admins (FinancialAdmin role, gated by the caller) may still
    /// post. Reversals remain allowed per Stage 02 §6.1 Phase 4.
    /// </summary>
    /// <param name="periodId">Period to soft-close.</param>
    /// <param name="closedByPrincipalId">
    /// Identifier of the principal performing the close; flows into the
    /// emitted <c>Financial.PeriodSoftClosed</c> event for audit-trail
    /// reconstruction. Pass <c>null</c> only for non-interactive callers
    /// (background close jobs, migration replays).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PeriodCloseResult> SoftCloseAsync(
        FiscalPeriodId periodId,
        string? closedByPrincipalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopen a soft-closed period (admin-only, gated by the caller).
    /// Emits <c>Financial.PeriodOpened</c> with the audit memo as the
    /// reopen reason. This overload only handles SoftClosed → Open;
    /// Locked → SoftClosed is <see cref="UnlockAsync"/>.
    /// </summary>
    Task<PeriodCloseResult> ReopenAsync(
        FiscalPeriodId periodId,
        string auditMemo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lock the period per Stage 02 §8.5 row 3. SoftClosed → Locked is
    /// the canonical path; Open → Locked is allowed for convenience
    /// (auto-soft-closes inline and emits
    /// <c>Financial.PeriodSoftClosed</c> followed by
    /// <c>Financial.PeriodLocked</c>) so the PR 3b year-end batch can
    /// lock periods without two service calls. Already-Locked input
    /// returns <see cref="PeriodCloseError.PeriodAlreadyLocked"/>.
    /// </summary>
    Task<PeriodCloseResult> LockAsync(
        FiscalPeriodId periodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlock a locked period back to <see cref="FiscalPeriodStatus.SoftClosed"/>
    /// with an audit memo (admin-only, gated by the caller). Re-stamps
    /// <c>SoftClosedAtUtc</c> to the wall-clock instant of the unlock
    /// so the timestamp reflects the new soft-close start. Emits
    /// <c>Financial.PeriodOpened</c> with
    /// <c>Reason = "Unlocked by admin: …"</c> (reusing the existing
    /// event type per the catalog convention; consumers distinguish
    /// via Reason prefix). To re-open the period for non-admin posts
    /// after unlock the caller follows up with <see cref="ReopenAsync"/>.
    /// Rejects when the owning <see cref="FiscalYear"/> is
    /// <see cref="FiscalYearStatus.Closed"/>; year-level reopen is
    /// <c>IFiscalYearCloseService.ReopenFiscalYearAsync</c> (PR 3b).
    /// </summary>
    Task<PeriodCloseResult> UnlockAsync(
        FiscalPeriodId periodId,
        string auditMemo,
        CancellationToken cancellationToken = default);
}
