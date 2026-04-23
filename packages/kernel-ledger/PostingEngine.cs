using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Events;
using Sunfish.Kernel.Ledger.Exceptions;
using Sunfish.Kernel.Ledger.Periods;

using LeaseNs = Sunfish.Kernel.Lease;

namespace Sunfish.Kernel.Ledger;

/// <summary>
/// Default <see cref="IPostingEngine"/> implementation. Paper §12.2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotency.</b> The engine keeps an in-memory index mapping
/// <see cref="Transaction.IdempotencyKey"/> → committed <see cref="Transaction.TransactionId"/>.
/// On construction the index is empty; it is populated as the engine replays
/// the ledger event stream (if one was supplied for rehydration). Calls to
/// <see cref="PostAsync"/> that hit an existing key short-circuit and return
/// the prior transaction id without appending a new event.
/// </para>
/// <para>
/// <b>Lease scope.</b> A single <see cref="Transaction"/> may touch multiple
/// accounts. The engine acquires one lease <i>per affected account</i>
/// (resource id <c>ledger:account:{accountId}</c>), holds them for the
/// duration of the append, then releases them. Per-account (rather than
/// per-transaction-global) scope lets concurrent transactions that touch
/// disjoint accounts proceed in parallel — matching paper §6.3's CP-class
/// serialization requirement without over-serializing. Account ids are sorted
/// deterministically before acquisition to avoid lease-ordering deadlocks
/// between two transactions that touch the same pair of accounts.
/// </para>
/// <para>
/// <b>Closed periods.</b> When the injected <see cref="IPeriodCloseState"/>
/// indicates a period is closed, postings whose <see cref="Posting.PostedAt"/>
/// falls at or before <c>LastClosedPeriodEnd</c> are rewritten to an
/// <c>adjustments-yyyyMMdd</c> account in the next open period before the
/// transaction is committed (paper §12.4).
/// </para>
/// </remarks>
public sealed class PostingEngine : IPostingEngine, ILedgerEventStream
{
    /// <summary>Default lease duration used for account serialization.</summary>
    internal static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromSeconds(30);

    private readonly LeaseNs.ILeaseCoordinator _leases;
    private readonly IEventLog _eventLog;
    private readonly IPeriodCloseState _periodState;
    private readonly ILogger<PostingEngine> _logger;

    // TransactionId → PostingsAppliedEvent (used by CompensateAsync for lookups).
    private readonly ConcurrentDictionary<Guid, PostingsAppliedEvent> _byTransactionId = new();

    // IdempotencyKey → committed TransactionId (paper §12.2 dedupe).
    private readonly ConcurrentDictionary<string, Guid> _byIdempotencyKey = new();

    // Append-ordered list of committed events for projection replay.
    private readonly List<object> _events = new();
    private readonly object _eventsGate = new();

    // Live subscribers for projections.
    private readonly List<Action<object>> _subscribers = new();
    private readonly object _subscribersGate = new();

    /// <summary>Constructs a new posting engine.</summary>
    public PostingEngine(
        LeaseNs.ILeaseCoordinator leases,
        IEventLog eventLog,
        IPeriodCloseState periodState,
        ILogger<PostingEngine>? logger = null)
    {
        _leases = leases ?? throw new ArgumentNullException(nameof(leases));
        _eventLog = eventLog ?? throw new ArgumentNullException(nameof(eventLog));
        _periodState = periodState ?? throw new ArgumentNullException(nameof(periodState));
        _logger = logger ?? NullLogger<PostingEngine>.Instance;
    }

    /// <inheritdoc />
    public async Task<PostingResult> PostAsync(Transaction tx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tx);
        ct.ThrowIfCancellationRequested();

        // Balance validation — paper §12.1.
        if (!tx.IsBalanced)
        {
            _logger.LogWarning("Rejecting unbalanced transaction {TxId} (sum={Sum})", tx.TransactionId, tx.Sum);
            return new PostingResult(false, tx.TransactionId, "UNBALANCED", null);
        }

        // Idempotency dedupe — paper §12.2.
        if (_byIdempotencyKey.TryGetValue(tx.IdempotencyKey, out var existingId))
        {
            _logger.LogDebug(
                "Idempotency key {Key} already committed as {ExistingId}; returning prior result",
                tx.IdempotencyKey, existingId);
            return new PostingResult(true, existingId, null, null);
        }

        // Closed-period rewrite — paper §12.4.
        var effectiveTx = RewriteForClosedPeriod(tx);

        // Acquire per-account leases in a deterministic order.
        var accountIds = effectiveTx.Postings
            .Select(p => p.AccountId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToArray();

        var heldLeases = new List<LeaseNs.Lease>(accountIds.Length);
        try
        {
            foreach (var accountId in accountIds)
            {
                var lease = await _leases.AcquireAsync(
                    $"ledger:account:{accountId}",
                    DefaultLeaseDuration,
                    ct).ConfigureAwait(false);
                if (lease is null)
                {
                    _logger.LogWarning(
                        "Lease unavailable for account {AccountId} while posting {TxId}",
                        accountId, effectiveTx.TransactionId);
                    return new PostingResult(false, effectiveTx.TransactionId, "QUORUM_UNAVAILABLE", null);
                }
                heldLeases.Add(lease);
            }

            // Append to kernel event log (durability receipt) and commit to local stream.
            var envelope = BuildKernelEventEnvelope(effectiveTx);
            var logSeq = await _eventLog.AppendAsync(envelope, ct).ConfigureAwait(false);

            var applied = new PostingsAppliedEvent(effectiveTx);
            CommitEvent(applied);
            _byTransactionId[effectiveTx.TransactionId] = applied;
            _byIdempotencyKey[effectiveTx.IdempotencyKey] = effectiveTx.TransactionId;

            return new PostingResult(true, effectiveTx.TransactionId, null, logSeq);
        }
        finally
        {
            foreach (var lease in heldLeases)
            {
                try
                {
                    await _leases.ReleaseAsync(lease, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Swallowed error releasing lease {LeaseId}", lease.LeaseId);
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<PostingResult> CompensateAsync(Guid originalTransactionId, string reason, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(reason);
        ct.ThrowIfCancellationRequested();

        if (!_byTransactionId.TryGetValue(originalTransactionId, out var original))
        {
            _logger.LogWarning("Compensate failed: transaction {TxId} not found", originalTransactionId);
            throw new InvalidOperationException(
                $"Cannot compensate transaction {originalTransactionId}: not found in ledger.");
        }

        var compensatingTxId = Guid.NewGuid();
        var compensatingPostings = original.Transaction.Postings.Select(p => new Posting(
            PostingId: Guid.NewGuid(),
            TransactionId: compensatingTxId,
            AccountId: p.AccountId,
            Amount: -p.Amount,
            Currency: p.Currency,
            PostedAt: DateTimeOffset.UtcNow,
            Description: $"Compensation of {p.Description}: {reason}",
            Metadata: p.Metadata)).ToList();

        var compensating = new Transaction(
            TransactionId: compensatingTxId,
            IdempotencyKey: $"compensation:{originalTransactionId}:{reason}",
            Postings: compensatingPostings,
            CreatedAt: DateTimeOffset.UtcNow);

        // PostAsync handles lease acquisition, balance check, idempotency.
        var result = await PostAsync(compensating, ct).ConfigureAwait(false);
        if (result.Success)
        {
            var compensationEvent = new CompensationAppliedEvent(originalTransactionId, compensating, reason);
            CommitEvent(compensationEvent);
        }
        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<object> ReplayAll()
    {
        lock (_eventsGate)
        {
            return _events.ToArray();
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<object> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_subscribersGate)
        {
            _subscribers.Add(handler);
        }
        return new Unsubscriber(this, handler);
    }

    /// <summary>
    /// Internal hook for <see cref="IPeriodCloser"/> to publish a
    /// <see cref="PeriodClosedEvent"/> through the same stream subscribers see.
    /// </summary>
    internal void PublishPeriodClosed(PeriodClosedEvent evt) => CommitEvent(evt);

    private void CommitEvent(object evt)
    {
        lock (_eventsGate)
        {
            _events.Add(evt);
        }

        Action<object>[] snapshot;
        lock (_subscribersGate)
        {
            snapshot = _subscribers.ToArray();
        }
        foreach (var s in snapshot)
        {
            try
            {
                s(evt);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Subscriber threw while handling ledger event");
            }
        }
    }

    private Transaction RewriteForClosedPeriod(Transaction tx)
    {
        var lastClosed = _periodState.LastClosedPeriodEnd;
        if (lastClosed is null)
        {
            return tx;
        }

        var closedEnd = lastClosed.Value;
        if (tx.Postings.All(p => p.PostedAt > closedEnd))
        {
            return tx;
        }

        var adjustmentSuffix = closedEnd.UtcDateTime.ToString("yyyyMMdd");
        var rewritten = tx.Postings.Select(p => p.PostedAt <= closedEnd
            ? p with
            {
                AccountId = $"adjustments-{adjustmentSuffix}",
                PostedAt = closedEnd.AddTicks(1),
                Description = $"[adj {adjustmentSuffix}] {p.Description}",
            }
            : p).ToList();

        return tx with { Postings = rewritten };
    }

    private static KernelEvent BuildKernelEventEnvelope(Transaction tx)
    {
        // Project the transaction into a payload dictionary for log durability.
        // The authoritative typed event lives on ILedgerEventStream; this is the
        // receipt written to IEventLog per the spec.
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["transactionId"] = tx.TransactionId,
            ["idempotencyKey"] = tx.IdempotencyKey,
            ["createdAt"] = tx.CreatedAt,
            ["postingCount"] = tx.Postings.Count,
            ["accounts"] = tx.Postings.Select(p => p.AccountId).Distinct(StringComparer.Ordinal).ToArray(),
        };

        return new KernelEvent(
            Id: new Sunfish.Kernel.Events.EventId(tx.TransactionId),
            EntityId: new EntityId("ledger", "txn", tx.TransactionId.ToString("N")),
            Kind: "ledger.postings-applied",
            OccurredAt: tx.CreatedAt,
            Payload: payload);
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly PostingEngine _engine;
        private readonly Action<object> _handler;
        private int _disposed;

        public Unsubscriber(PostingEngine engine, Action<object> handler)
        {
            _engine = engine;
            _handler = handler;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            lock (_engine._subscribersGate)
            {
                _engine._subscribers.Remove(_handler);
            }
        }
    }
}
