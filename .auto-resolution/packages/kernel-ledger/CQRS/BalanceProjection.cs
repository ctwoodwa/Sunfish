using System.Runtime.CompilerServices;

namespace Sunfish.Kernel.Ledger.CQRS;

/// <summary>
/// Default <see cref="IBalanceProjection"/> implementation. In-memory balance
/// table keyed by account id. Subscribes to <see cref="ILedgerEventStream"/>
/// and updates on every <see cref="PostingsAppliedEvent"/>.
/// </summary>
public sealed class BalanceProjection : IBalanceProjection, IDisposable
{
    private readonly ILedgerEventStream _stream;
    private readonly IDisposable _subscription;
    private readonly object _gate = new();

    // accountId → list of (postedAt, amount, posting) in append order.
    private readonly Dictionary<string, List<PostingRecord>> _accounts = new(StringComparer.Ordinal);

    /// <summary>Constructs a new balance projection, subscribing to the stream.</summary>
    public BalanceProjection(ILedgerEventStream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));

        // Prime with any already-committed events, then subscribe for future ones.
        foreach (var evt in _stream.ReplayAll())
        {
            Apply(evt);
        }
        _subscription = _stream.Subscribe(Apply);
    }

    /// <inheritdoc />
    public Task<decimal> GetBalanceAsync(string accountId, DateTimeOffset? asOf, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(accountId);
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_accounts.TryGetValue(accountId, out var list))
            {
                return Task.FromResult(0m);
            }
            if (asOf is null)
            {
                return Task.FromResult(list.Sum(r => r.Amount));
            }
            var cutoff = asOf.Value;
            return Task.FromResult(list.Where(r => r.PostedAt <= cutoff).Sum(r => r.Amount));
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Posting> GetPostingsAsync(
        string accountId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(accountId);
        PostingRecord[] snapshot;
        lock (_gate)
        {
            snapshot = _accounts.TryGetValue(accountId, out var list)
                ? list.ToArray()
                : Array.Empty<PostingRecord>();
        }

        // Newest first.
        for (var i = snapshot.Length - 1; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();
            yield return snapshot[i].Posting;
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task RebuildAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _accounts.Clear();
            foreach (var evt in _stream.ReplayAll())
            {
                Apply(evt);
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose() => _subscription.Dispose();

    private void Apply(object evt)
    {
        if (evt is PostingsAppliedEvent applied)
        {
            lock (_gate)
            {
                foreach (var p in applied.Transaction.Postings)
                {
                    if (!_accounts.TryGetValue(p.AccountId, out var list))
                    {
                        list = new List<PostingRecord>();
                        _accounts[p.AccountId] = list;
                    }
                    list.Add(new PostingRecord(p.PostedAt, p.Amount, p));
                }
            }
        }
        // CompensationAppliedEvent is realized through the Transaction in the
        // PostingsAppliedEvent that the PostingEngine already committed for the
        // compensating transaction, so no additional handling here.
    }

    private readonly record struct PostingRecord(DateTimeOffset PostedAt, decimal Amount, Posting Posting);
}
