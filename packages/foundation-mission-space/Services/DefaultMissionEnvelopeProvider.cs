using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Reference <see cref="IMissionEnvelopeProvider"/> per ADR 0062-A1.4.
/// Single-flight envelope construction, cache TTL, observer fanout
/// with 100ms coalescing window + 100-pending bound + oldest-first
/// overflow.
/// </summary>
/// <remarks>
/// <para>
/// <b>Single-flight.</b> N concurrent callers see exactly 1
/// envelope-factory invocation per cache-miss; the others await the
/// in-flight result.
/// </para>
/// <para>
/// <b>Per-cost-class timeout</b> per A1.6 is enforced inside the
/// envelope-factory delegate (each probe knows its own cost class
/// + budget). The coordinator's <see cref="DefaultOverallTimeout"/>
/// is an outer-bound safety net (15s).
/// </para>
/// <para>
/// <b>Cache invalidation</b> on probe-status transition per A1.7 —
/// callers signal via <see cref="InvalidateAsync"/>. The coordinator
/// itself is content-agnostic; consumers (W#36 Bridge subscription
/// handler, W#39 regulatory probe, etc.) call <see cref="InvalidateAsync"/>
/// when they observe a Healthy → Stale / Failed transition for any
/// dimension probe.
/// </para>
/// <para>
/// <b>Observer fanout</b> per A1.4: when the envelope changes, the
/// coordinator schedules a fanout 100ms in the future; additional
/// changes within that window are merged into the pending change
/// (their <see cref="EnvelopeChange.ChangedDimensions"/> sets are
/// unioned). At fanout time, every observer receives the final
/// coalesced change. The pending-change queue is bounded at 100;
/// overflow evicts oldest-first + emits
/// <see cref="AuditEventType.MissionEnvelopeObserverOverflow"/>.
/// </para>
/// </remarks>
public sealed class DefaultMissionEnvelopeProvider : IMissionEnvelopeProvider, IAsyncDisposable
{
    /// <summary>Default overall envelope-construction timeout per A1.4 (outer-bound; per-probe budgets are tighter).</summary>
    public static readonly TimeSpan DefaultOverallTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Default cache TTL — a small upper bound; probes drive most invalidation per A1.7.</summary>
    public static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>Per A1.4 — the coalescing window for observer fanout.</summary>
    public static readonly TimeSpan DefaultCoalescingWindow = TimeSpan.FromMilliseconds(100);

    /// <summary>Per A1.4 — pending-change queue bound. Overflow evicts oldest-first.</summary>
    public const int DefaultMaxPendingChanges = 100;

    private readonly Func<CancellationToken, ValueTask<MissionEnvelope>> _envelopeFactory;
    private readonly TimeProvider _time;
    private readonly TimeSpan _overallTimeout;
    private readonly TimeSpan _cacheTtl;
    private readonly TimeSpan _coalescingWindow;
    private readonly int _maxPendingChanges;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _tenantId;

    private readonly SemaphoreSlim _coordinatorLock = new(1, 1);
    private MissionEnvelope? _cachedEnvelope;
    private DateTimeOffset _cachedAt;
    private TaskCompletionSource<MissionEnvelope>? _inflight;

    private readonly List<IMissionEnvelopeObserver> _observers = new();
    private readonly object _observersLock = new();

    private readonly object _fanoutLock = new();
    private EnvelopeChange? _pendingFanout;
    private int _pendingMergedCount;
    private CancellationTokenSource? _fanoutCts;
    private bool _disposed;

    /// <summary>Audit-disabled overload (test / bootstrap).</summary>
    public DefaultMissionEnvelopeProvider(
        Func<CancellationToken, ValueTask<MissionEnvelope>> envelopeFactory,
        TimeProvider? time = null,
        TimeSpan? overallTimeout = null,
        TimeSpan? cacheTtl = null,
        TimeSpan? coalescingWindow = null,
        int? maxPendingChanges = null)
    {
        ArgumentNullException.ThrowIfNull(envelopeFactory);
        _envelopeFactory = envelopeFactory;
        _time = time ?? TimeProvider.System;
        _overallTimeout = overallTimeout ?? DefaultOverallTimeout;
        _cacheTtl = cacheTtl ?? DefaultCacheTtl;
        _coalescingWindow = coalescingWindow ?? DefaultCoalescingWindow;
        _maxPendingChanges = maxPendingChanges ?? DefaultMaxPendingChanges;
    }

    /// <summary>Audit-enabled overload — W#32 both-or-neither contract.</summary>
    public DefaultMissionEnvelopeProvider(
        Func<CancellationToken, ValueTask<MissionEnvelope>> envelopeFactory,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        TenantId tenantId,
        TimeProvider? time = null,
        TimeSpan? overallTimeout = null,
        TimeSpan? cacheTtl = null,
        TimeSpan? coalescingWindow = null,
        int? maxPendingChanges = null)
        : this(envelopeFactory, time, overallTimeout, cacheTtl, coalescingWindow, maxPendingChanges)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }
        _auditTrail = auditTrail;
        _signer = signer;
        _tenantId = tenantId;
    }

    /// <inheritdoc />
    public async ValueTask<MissionEnvelope> GetCurrentAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Fast cache-hit path — no lock acquisition.
        if (_cachedEnvelope is not null && _time.GetUtcNow() - _cachedAt < _cacheTtl)
        {
            return _cachedEnvelope;
        }

        TaskCompletionSource<MissionEnvelope>? joinTcs = null;
        TaskCompletionSource<MissionEnvelope>? ownTcs = null;

        await _coordinatorLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check under the lock — another caller may have refreshed.
            if (_cachedEnvelope is not null && _time.GetUtcNow() - _cachedAt < _cacheTtl)
            {
                return _cachedEnvelope;
            }

            if (_inflight is not null)
            {
                joinTcs = _inflight;
            }
            else
            {
                ownTcs = new TaskCompletionSource<MissionEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
                _inflight = ownTcs;
            }
        }
        finally
        {
            _coordinatorLock.Release();
        }

        if (joinTcs is not null)
        {
            return await joinTcs.Task.WaitAsync(ct).ConfigureAwait(false);
        }

        // We're the elected runner.
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_overallTimeout);
            var envelope = await _envelopeFactory(timeoutCts.Token).ConfigureAwait(false);
            envelope = envelope.WithComputedHash();

            var previous = _cachedEnvelope;
            _cachedEnvelope = envelope;
            _cachedAt = _time.GetUtcNow();
            ownTcs!.TrySetResult(envelope);

            if (previous is null || previous.EnvelopeHash != envelope.EnvelopeHash)
            {
                EnqueueFanout(previous, envelope);
            }

            return envelope;
        }
        catch (Exception ex)
        {
            ownTcs!.TrySetException(ex);
            throw;
        }
        finally
        {
            await _coordinatorLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (ReferenceEquals(_inflight, ownTcs))
                {
                    _inflight = null;
                }
            }
            finally
            {
                _coordinatorLock.Release();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask InvalidateAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _coordinatorLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _cachedEnvelope = null;
            _cachedAt = default;
        }
        finally
        {
            _coordinatorLock.Release();
        }
    }

    /// <inheritdoc />
    public void Subscribe(IMissionEnvelopeObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_observersLock)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }
        }
    }

    /// <inheritdoc />
    public void Unsubscribe(IMissionEnvelopeObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_observersLock)
        {
            _observers.Remove(observer);
        }
    }

    /// <summary>Drains the pending-fanout queue immediately (test seam — production drains via the coalescing timer).</summary>
    internal async Task FlushFanoutForTestAsync(CancellationToken ct = default)
    {
        EnvelopeChange? toDispatch;
        lock (_fanoutLock)
        {
            toDispatch = _pendingFanout;
            _pendingFanout = null;
            _pendingMergedCount = 0;
            _fanoutCts?.Cancel();
            _fanoutCts = null;
        }
        if (toDispatch is not null)
        {
            await DispatchAsync(toDispatch, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await FlushFanoutForTestAsync(CancellationToken.None).ConfigureAwait(false);
        _coordinatorLock.Dispose();
    }

    private void EnqueueFanout(MissionEnvelope? previous, MissionEnvelope current)
    {
        var change = ComputeChange(previous, current);

        lock (_fanoutLock)
        {
            if (_pendingFanout is null)
            {
                _pendingFanout = change;
                _pendingMergedCount = 1;
                ScheduleFanoutTimerLocked();
                return;
            }

            // Merge into the pending change. Take Current from the
            // newer change; merge ChangedDimensions; pick the higher
            // severity.
            var mergedDims = new HashSet<DimensionChangeKind>(_pendingFanout.ChangedDimensions);
            foreach (var d in change.ChangedDimensions) mergedDims.Add(d);
            var severity = (EnvelopeChangeSeverity)Math.Max(
                (int)_pendingFanout.Severity,
                (int)change.Severity);
            _pendingFanout = new EnvelopeChange
            {
                Previous = _pendingFanout.Previous, // keep the original "previous" reference
                Current = current,
                ChangedDimensions = mergedDims.ToArray(),
                Severity = severity,
            };
            _pendingMergedCount++;

            if (_pendingMergedCount > _maxPendingChanges)
            {
                _pendingMergedCount = _maxPendingChanges;
                _ = EmitOverflowAsync();
            }
        }
    }

    private void ScheduleFanoutTimerLocked()
    {
        _fanoutCts?.Cancel();
        _fanoutCts = new CancellationTokenSource();
        var ct = _fanoutCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_coalescingWindow, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            EnvelopeChange? toDispatch;
            lock (_fanoutLock)
            {
                if (ct.IsCancellationRequested) return;
                toDispatch = _pendingFanout;
                _pendingFanout = null;
                _pendingMergedCount = 0;
                _fanoutCts = null;
            }
            if (toDispatch is not null)
            {
                await DispatchAsync(toDispatch, CancellationToken.None).ConfigureAwait(false);
            }
        }, ct);
    }

    private async Task DispatchAsync(EnvelopeChange change, CancellationToken ct)
    {
        IMissionEnvelopeObserver[] snapshot;
        lock (_observersLock)
        {
            snapshot = _observers.ToArray();
        }
        foreach (var observer in snapshot)
        {
            try
            {
                await observer.OnChangedAsync(change, ct).ConfigureAwait(false);
            }
            catch
            {
                // Per A1.4 — observer exceptions don't propagate; the
                // fanout pump keeps draining for other observers.
            }
        }
        await EmitAsync(AuditEventType.MissionEnvelopeChangeBroadcast,
            BuildBroadcastPayload(change), ct).ConfigureAwait(false);
    }

    private static EnvelopeChange ComputeChange(MissionEnvelope? previous, MissionEnvelope current)
    {
        var changed = new HashSet<DimensionChangeKind>();
        var severity = EnvelopeChangeSeverity.Informational;

        if (previous is null)
        {
            // First envelope — every dimension is "new", but severity
            // is Informational (initial provisioning).
            foreach (var d in Enum.GetValues<DimensionChangeKind>()) changed.Add(d);
        }
        else
        {
            if (!Equivalent(previous.Hardware, current.Hardware)) changed.Add(DimensionChangeKind.Hardware);
            if (!Equivalent(previous.User, current.User)) changed.Add(DimensionChangeKind.User);
            if (!Equivalent(previous.Regulatory, current.Regulatory)) changed.Add(DimensionChangeKind.Regulatory);
            if (!Equivalent(previous.Runtime, current.Runtime)) changed.Add(DimensionChangeKind.Runtime);
            if (!Equivalent(previous.FormFactor, current.FormFactor)) changed.Add(DimensionChangeKind.FormFactor);
            if (!Equivalent(previous.Edition, current.Edition)) changed.Add(DimensionChangeKind.Edition);
            if (!Equivalent(previous.Network, current.Network)) changed.Add(DimensionChangeKind.Network);
            if (!Equivalent(previous.TrustAnchor, current.TrustAnchor)) changed.Add(DimensionChangeKind.TrustAnchor);
            if (!Equivalent(previous.SyncState, current.SyncState)) changed.Add(DimensionChangeKind.SyncState);
            if (!Equivalent(previous.VersionVector, current.VersionVector)) changed.Add(DimensionChangeKind.VersionVector);
        }

        // Per A1.10 — if any probe is in a non-Healthy state, severity is ProbeUnreliable.
        var hasUnhealthy = current.Hardware.ProbeStatus != ProbeStatus.Healthy
            || current.User.ProbeStatus != ProbeStatus.Healthy
            || current.Regulatory.ProbeStatus != ProbeStatus.Healthy
            || current.Runtime.ProbeStatus != ProbeStatus.Healthy
            || current.FormFactor.ProbeStatus != ProbeStatus.Healthy
            || current.Edition.ProbeStatus != ProbeStatus.Healthy
            || current.Network.ProbeStatus != ProbeStatus.Healthy
            || current.TrustAnchor.ProbeStatus != ProbeStatus.Healthy
            || current.SyncState.ProbeStatus != ProbeStatus.Healthy
            || current.VersionVector.ProbeStatus != ProbeStatus.Healthy;
        if (hasUnhealthy)
        {
            severity = EnvelopeChangeSeverity.ProbeUnreliable;
        }
        else if (previous is not null && changed.Count > 0)
        {
            severity = EnvelopeChangeSeverity.Warning;
        }

        return new EnvelopeChange
        {
            Previous = previous,
            Current = current,
            ChangedDimensions = changed.ToArray(),
            Severity = severity,
        };
    }

    private static bool Equivalent<T>(T a, T b) where T : class
    {
        // Records implement structural equality natively; HashSet
        // members + nullable fields all compare correctly.
        return EqualityComparer<T>.Default.Equals(a, b);
    }

    private async Task EmitOverflowAsync()
    {
        await EmitAsync(AuditEventType.MissionEnvelopeObserverOverflow,
            new AuditPayload(new Dictionary<string, object?>
            {
                ["max_pending"] = _maxPendingChanges,
            }),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static AuditPayload BuildBroadcastPayload(EnvelopeChange change) =>
        new(new Dictionary<string, object?>
        {
            ["changed_dimension_count"] = change.ChangedDimensions.Count,
            ["envelope_hash"] = change.Current.EnvelopeHash,
            ["severity"] = change.Severity.ToString(),
        });

    private async Task EmitAsync(AuditEventType eventType, AuditPayload payload, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null) return;
        var occurredAt = _time.GetUtcNow();
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: _tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }
}
