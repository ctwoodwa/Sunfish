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
        };

        await _periods.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
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
        };

        await _periods.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        await _events.PublishAsync(
            new PeriodOpened(
                PeriodId: updated.Id,
                ChartId:  updated.ChartId,
                Reason:   $"Reopened by admin: {auditMemo}"),
            cancellationToken).ConfigureAwait(false);

        return new PeriodCloseResult(updated, PeriodCloseError.None, null);
    }
}
