using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Financial;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Default <see cref="IPeriodCloseService"/> per Stage 02 §6.5(a). PR 2
/// covers soft-close + reopen-soft; hard-close + year-end rollover are
/// PR 3.
/// </summary>
public sealed class PeriodCloseService : IPeriodCloseService
{
    private readonly IFiscalPeriodRepository _periods;
    private readonly IFiscalYearRepository _years;
    private readonly IDomainEventPublisher _events;
    private readonly TimeProvider _time;

    public PeriodCloseService(
        IFiscalPeriodRepository periods,
        IFiscalYearRepository years,
        IDomainEventPublisher events,
        TimeProvider time)
    {
        _periods = periods ?? throw new ArgumentNullException(nameof(periods));
        _years   = years   ?? throw new ArgumentNullException(nameof(years));
        _events  = events  ?? throw new ArgumentNullException(nameof(events));
        _time    = time    ?? throw new ArgumentNullException(nameof(time));
    }

    /// <inheritdoc />
    public async Task<PeriodCloseResult> SoftCloseAsync(
        FiscalPeriodId periodId,
        string? closedByPrincipalId = null,
        CancellationToken cancellationToken = default)
    {
        var period = await _periods.GetAsync(periodId, cancellationToken).ConfigureAwait(false);
        if (period is null)
            return new PeriodCloseResult(null, PeriodCloseError.PeriodNotFound, periodId.Value);
        if (period.Status == FiscalPeriodStatus.SoftClosed)
            return new PeriodCloseResult(period, PeriodCloseError.PeriodAlreadySoftClosed, null);
        if (period.Status == FiscalPeriodStatus.Locked)
            return new PeriodCloseResult(period, PeriodCloseError.PeriodLocked, null);

        var now = new Instant(_time.GetUtcNow());
        var updated = period with
        {
            Status          = FiscalPeriodStatus.SoftClosed,
            SoftClosedAtUtc = now,
            Version         = period.Version + 1,
        };

        if (!await _periods.UpdateAsync(updated, cancellationToken).ConfigureAwait(false))
            return new PeriodCloseResult(period, PeriodCloseError.ConcurrentUpdate, null);

        await _events.PublishAsync(
            new PeriodSoftClosed(
                PeriodId:            updated.Id,
                ChartId:             updated.ChartId,
                ClosedByPrincipalId: closedByPrincipalId),
            cancellationToken).ConfigureAwait(false);

        return new PeriodCloseResult(updated, PeriodCloseError.None, null);
    }

    /// <inheritdoc />
    public async Task<PeriodCloseResult> ReopenAsync(
        FiscalPeriodId periodId,
        string auditMemo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auditMemo))
            return new PeriodCloseResult(null, PeriodCloseError.AuditMemoRequired, null);

        var period = await _periods.GetAsync(periodId, cancellationToken).ConfigureAwait(false);
        if (period is null)
            return new PeriodCloseResult(null, PeriodCloseError.PeriodNotFound, periodId.Value);
        // Distinguish Open (already-reopened — surface caller mistake)
        // from Locked (PR 3's unlock-with-audit owns that path).
        if (period.Status == FiscalPeriodStatus.Open)
            return new PeriodCloseResult(period, PeriodCloseError.PeriodNotSoftClosed, null);
        if (period.Status != FiscalPeriodStatus.SoftClosed)
            return new PeriodCloseResult(period, PeriodCloseError.PeriodLocked, null);

        var fy = await _years.GetAsync(period.FiscalYearId, cancellationToken).ConfigureAwait(false);
        if (fy is { Status: FiscalYearStatus.Closed })
            return new PeriodCloseResult(period, PeriodCloseError.FiscalYearAlreadyClosed, null);

        var updated = period with
        {
            Status          = FiscalPeriodStatus.Open,
            SoftClosedAtUtc = null,
            Version         = period.Version + 1,
        };

        if (!await _periods.UpdateAsync(updated, cancellationToken).ConfigureAwait(false))
            return new PeriodCloseResult(period, PeriodCloseError.ConcurrentUpdate, null);

        await _events.PublishAsync(
            new PeriodOpened(
                PeriodId: updated.Id,
                ChartId:  updated.ChartId,
                Reason:   $"Reopened by admin: {auditMemo}"),
            cancellationToken).ConfigureAwait(false);

        return new PeriodCloseResult(updated, PeriodCloseError.None, null);
    }

    /// <inheritdoc />
    public async Task<PeriodCloseResult> LockAsync(
        FiscalPeriodId periodId,
        CancellationToken cancellationToken = default)
    {
        var period = await _periods.GetAsync(periodId, cancellationToken).ConfigureAwait(false);
        if (period is null)
            return new PeriodCloseResult(null, PeriodCloseError.PeriodNotFound, periodId.Value);
        if (period.Status == FiscalPeriodStatus.Locked)
            return new PeriodCloseResult(period, PeriodCloseError.PeriodAlreadyLocked, null);

        var now = new Instant(_time.GetUtcNow());
        // Lock is canonically valid for SoftClosed periods (Stage 02
        // §8.5 row 3); an Open period auto-soft-closes inline so the
        // PR 3b year-end batch can lock in one call. The auto path is
        // remembered so we can emit PeriodSoftClosed *before*
        // PeriodLocked — downstream consumers (AR aging snapshots,
        // reports cluster) that gate on soft-close must see the
        // intermediate transition even when both state changes
        // collapse into one CAS step.
        var autoSoftClosing = period.Status == FiscalPeriodStatus.Open;
        var softClosedAt = period.SoftClosedAtUtc ?? now;

        var updated = period with
        {
            Status          = FiscalPeriodStatus.Locked,
            SoftClosedAtUtc = softClosedAt,
            LockedAtUtc     = now,
            Version         = period.Version + 1,
        };

        if (!await _periods.UpdateAsync(updated, cancellationToken).ConfigureAwait(false))
            return new PeriodCloseResult(period, PeriodCloseError.ConcurrentUpdate, null);

        if (autoSoftClosing)
        {
            await _events.PublishAsync(
                new PeriodSoftClosed(
                    PeriodId:            updated.Id,
                    ChartId:             updated.ChartId,
                    ClosedByPrincipalId: null),
                cancellationToken).ConfigureAwait(false);
        }
        await _events.PublishAsync(
            new PeriodLocked(PeriodId: updated.Id, ChartId: updated.ChartId),
            cancellationToken).ConfigureAwait(false);

        return new PeriodCloseResult(updated, PeriodCloseError.None, null);
    }

    /// <inheritdoc />
    public async Task<PeriodCloseResult> UnlockAsync(
        FiscalPeriodId periodId,
        string auditMemo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auditMemo))
            return new PeriodCloseResult(null, PeriodCloseError.AuditMemoRequired, null);

        var period = await _periods.GetAsync(periodId, cancellationToken).ConfigureAwait(false);
        if (period is null)
            return new PeriodCloseResult(null, PeriodCloseError.PeriodNotFound, periodId.Value);
        if (period.Status != FiscalPeriodStatus.Locked)
            return new PeriodCloseResult(period, PeriodCloseError.PeriodNotLocked, null);

        var fy = await _years.GetAsync(period.FiscalYearId, cancellationToken).ConfigureAwait(false);
        if (fy is { Status: FiscalYearStatus.Closed })
            return new PeriodCloseResult(period, PeriodCloseError.FiscalYearAlreadyClosed, null);

        // Unlock returns to SoftClosed (not Open) — the admin who
        // unlocks still owes a separate ReopenAsync if they actually
        // want to permit non-admin posts. Stage 02 §8.5 row 3 reverse
        // path. Re-stamp SoftClosedAtUtc to the unlock instant so the
        // audit trail reflects the new soft-close start (the original
        // soft-close instant was preserved through the lock window
        // but is now stale for downstream consumers).
        var unlockedAt = new Instant(_time.GetUtcNow());
        var updated = period with
        {
            Status          = FiscalPeriodStatus.SoftClosed,
            SoftClosedAtUtc = unlockedAt,
            LockedAtUtc     = null,
            Version         = period.Version + 1,
        };

        if (!await _periods.UpdateAsync(updated, cancellationToken).ConfigureAwait(false))
            return new PeriodCloseResult(period, PeriodCloseError.ConcurrentUpdate, null);

        // Emit PeriodOpened with the unlock memo as the reason so
        // observers see the audit string in the cross-cluster bus.
        await _events.PublishAsync(
            new PeriodOpened(
                PeriodId: updated.Id,
                ChartId:  updated.ChartId,
                Reason:   $"Unlocked by admin: {auditMemo}"),
            cancellationToken).ConfigureAwait(false);

        return new PeriodCloseResult(updated, PeriodCloseError.None, null);
    }
}
