using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Financial;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Default <see cref="IPeriodCloseService"/> per Stage 02 §6.5(a) +
/// §8.5 row 3. PR 2 shipped soft-close + reopen-soft; PR 3a added
/// lock + unlock + the <see cref="FiscalPeriod.Version"/> CAS;
/// PR 3b wraps all event emission in the canonical
/// <see cref="DomainEventEnvelope{TPayload}"/> per
/// <c>xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md</c>.
/// </summary>
public sealed class PeriodCloseService : IPeriodCloseService
{
    private const int PayloadSchemaVersion = 1;

    private readonly IFiscalPeriodRepository _periods;
    private readonly IFiscalYearRepository _years;
    private readonly IDomainEventPublisher _events;
    private readonly TimeProvider _time;
    private readonly TenantId _tenantId;
    private readonly ReplicaId _replicaId;

    /// <summary>
    /// Construct a <see cref="PeriodCloseService"/>. The
    /// <paramref name="tenantId"/> + <paramref name="replicaId"/>
    /// flow into emitted event envelopes; production hosts supply the
    /// active tenant + replica from their context surfaces, tests
    /// default both to the system sentinels.
    /// </summary>
    public PeriodCloseService(
        IFiscalPeriodRepository periods,
        IFiscalYearRepository years,
        IDomainEventPublisher events,
        TimeProvider time,
        TenantId? tenantId = null,
        ReplicaId? replicaId = null)
    {
        _periods   = periods ?? throw new ArgumentNullException(nameof(periods));
        _years     = years   ?? throw new ArgumentNullException(nameof(years));
        _events    = events  ?? throw new ArgumentNullException(nameof(events));
        _time      = time    ?? throw new ArgumentNullException(nameof(time));
        _tenantId  = tenantId  ?? TenantId.System;
        _replicaId = replicaId ?? ReplicaId.System;
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

        await PublishAsync(
            "Financial.PeriodSoftClosed",
            IdempotencyKey("Financial.PeriodSoftClosed", updated.Id, FiscalPeriodStatus.SoftClosed),
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

        await PublishAsync(
            "Financial.PeriodOpened",
            IdempotencyKey("Financial.PeriodOpened", updated.Id, FiscalPeriodStatus.Open),
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
            await PublishAsync(
                "Financial.PeriodSoftClosed",
                IdempotencyKey("Financial.PeriodSoftClosed", updated.Id, FiscalPeriodStatus.SoftClosed),
                new PeriodSoftClosed(
                    PeriodId:            updated.Id,
                    ChartId:             updated.ChartId,
                    ClosedByPrincipalId: null),
                cancellationToken).ConfigureAwait(false);
        }
        await PublishAsync(
            "Financial.PeriodLocked",
            IdempotencyKey("Financial.PeriodLocked", updated.Id, FiscalPeriodStatus.Locked),
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

        // Unlock emits PeriodOpened (reusing the event type per the
        // catalog convention; consumers distinguish via the Reason
        // prefix "Unlocked by admin:" vs "Reopened by admin:"). The
        // idempotency key includes the SoftClosed status target so the
        // unlock event is distinct from a Reopen-to-Open emission for
        // the same period.
        await PublishAsync(
            "Financial.PeriodOpened",
            IdempotencyKey("Financial.PeriodOpened", updated.Id, FiscalPeriodStatus.SoftClosed),
            new PeriodOpened(
                PeriodId: updated.Id,
                ChartId:  updated.ChartId,
                Reason:   $"Unlocked by admin: {auditMemo}"),
            cancellationToken).ConfigureAwait(false);

        return new PeriodCloseResult(updated, PeriodCloseError.None, null);
    }

    private Task PublishAsync<TPayload>(
        string eventType,
        string idempotencyKey,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var envelope = new DomainEventEnvelope<TPayload>
        {
            // EventId is an interim UUIDv7-as-GUID-string (sortable by
            // mint-time, satisfies the §1 eventId sortability intent).
            // foundation-events will mint real ULID Crockford-base32
            // strings; the swap is value-shape-only — consumers must
            // treat EventId as opaque string.
            EventId              = Guid.CreateVersion7().ToString(),
            EventType            = eventType,
            SchemaVersion        = PayloadSchemaVersion,
            OccurredAt           = _time.GetUtcNow(),
            TenantId             = _tenantId,
            OriginatingReplicaId = _replicaId,
            IdempotencyKey       = idempotencyKey,
            Payload              = payload,
        };
        return _events.PublishAsync(envelope, cancellationToken);
    }

    private string IdempotencyKey(string eventType, FiscalPeriodId periodId, FiscalPeriodStatus newStatus)
        // Per xo-ruling-2026-05-16T21-12Z idempotency-key convention
        // for periods. Falls back to TenantId.System.Value (not a bare
        // literal) for default-constructed TenantIds — keeps the
        // canonical sentinel literal in one place.
        => $"{eventType}|{_tenantId.Value ?? TenantId.System.Value}|{periodId.Value}|{newStatus}";
}
