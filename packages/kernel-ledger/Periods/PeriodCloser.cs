using Sunfish.Kernel.Ledger.Exceptions;

namespace Sunfish.Kernel.Ledger.Periods;

/// <summary>
/// Default <see cref="IPeriodCloser"/> implementation. Replays the ledger
/// event stream to compute balances as-of <c>periodEnd</c>, persists a
/// <see cref="PeriodClosedEvent"/>, and mutates the shared
/// <see cref="PeriodCloseState"/> so subsequent postings see the new boundary.
/// </summary>
public sealed class PeriodCloser : IPeriodCloser
{
    private readonly ILedgerEventStream _stream;
    private readonly PeriodCloseState _state;
    private readonly PostingEngine _engine;

    /// <summary>Constructs a new period closer.</summary>
    public PeriodCloser(ILedgerEventStream stream, PeriodCloseState state, PostingEngine engine)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <inheritdoc />
    public Task<PeriodCloseResult> CloseAsync(DateTimeOffset periodEnd, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var lastClosed = _state.LastClosedPeriodEnd;
        if (lastClosed is not null && periodEnd <= lastClosed.Value)
        {
            throw new ClosedPeriodException(periodEnd, lastClosed.Value);
        }

        var balances = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var evt in _stream.ReplayAll())
        {
            if (evt is not PostingsAppliedEvent applied)
            {
                continue;
            }
            foreach (var p in applied.Transaction.Postings)
            {
                if (p.PostedAt > periodEnd)
                {
                    continue;
                }
                balances[p.AccountId] = balances.TryGetValue(p.AccountId, out var existing)
                    ? existing + p.Amount
                    : p.Amount;
            }
        }

        var snapshot = new Dictionary<string, decimal>(balances, StringComparer.Ordinal);
        var closedEvent = new PeriodClosedEvent(periodEnd, snapshot);

        _state.SetLastClosed(periodEnd);
        _engine.PublishPeriodClosed(closedEvent);

        return Task.FromResult(new PeriodCloseResult(
            periodEnd,
            (ulong)snapshot.Count,
            snapshot));
    }

    /// <inheritdoc />
    public Task<DateTimeOffset?> LastClosedPeriodEndAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_state.LastClosedPeriodEnd);
    }
}

/// <summary>
/// Shared, mutable container for the most-recent closed period boundary.
/// Paper §12.4. Used by both <see cref="PeriodCloser"/> (writer) and
/// <see cref="Sunfish.Kernel.Ledger.PostingEngine"/> (reader — through
/// <see cref="IPeriodCloseState"/>).
/// </summary>
public sealed class PeriodCloseState : IPeriodCloseState
{
    private readonly object _gate = new();
    private DateTimeOffset? _lastClosed;

    /// <inheritdoc />
    public DateTimeOffset? LastClosedPeriodEnd
    {
        get { lock (_gate) { return _lastClosed; } }
    }

    /// <summary>Internal API — set by <see cref="PeriodCloser"/>.</summary>
    internal void SetLastClosed(DateTimeOffset periodEnd)
    {
        lock (_gate)
        {
            _lastClosed = periodEnd;
        }
    }
}
