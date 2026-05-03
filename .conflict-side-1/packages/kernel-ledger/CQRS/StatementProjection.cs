namespace Sunfish.Kernel.Ledger.CQRS;

/// <summary>
/// Default <see cref="IStatementProjection"/> implementation. Computes opening
/// and closing balances by scanning the authoritative event stream, so results
/// are always consistent with <see cref="IBalanceProjection"/> without needing
/// a separate persistent table.
/// </summary>
public sealed class StatementProjection : IStatementProjection
{
    private readonly ILedgerEventStream _stream;

    /// <summary>Constructs a new statement projection.</summary>
    public StatementProjection(ILedgerEventStream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <inheritdoc />
    public Task<Statement> GetStatementAsync(
        string accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(accountId);
        ct.ThrowIfCancellationRequested();

        var opening = 0m;
        var closing = 0m;
        var inPeriod = new List<Posting>();

        foreach (var evt in _stream.ReplayAll())
        {
            if (evt is not PostingsAppliedEvent applied)
            {
                continue;
            }
            foreach (var p in applied.Transaction.Postings)
            {
                if (!string.Equals(p.AccountId, accountId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (p.PostedAt < periodStart)
                {
                    opening += p.Amount;
                }
                if (p.PostedAt <= periodEnd)
                {
                    closing += p.Amount;
                }
                if (p.PostedAt >= periodStart && p.PostedAt <= periodEnd)
                {
                    inPeriod.Add(p);
                }
            }
        }

        inPeriod.Sort((a, b) => a.PostedAt.CompareTo(b.PostedAt));
        return Task.FromResult(new Statement(
            accountId,
            periodStart,
            periodEnd,
            opening,
            closing,
            inPeriod));
    }
}
