namespace Sunfish.Kernel.Ledger.CQRS;

/// <summary>
/// CQRS read-side — materialized account balances. Paper §12.3:
/// "Querying balances from the raw posting stream is impractical at scale. The
/// architecture uses a CQRS write/read split: Write side — immutable posting
/// event stream (source of truth). Read side — materialized projections…
/// updated asynchronously from the event stream."
/// </summary>
public interface IBalanceProjection
{
    /// <summary>
    /// Current balance for <paramref name="accountId"/>. If <paramref name="asOf"/>
    /// is non-null, balance is computed by summing every posting whose
    /// <see cref="Posting.PostedAt"/> is ≤ <paramref name="asOf"/>; null means
    /// "right now".
    /// </summary>
    Task<decimal> GetBalanceAsync(string accountId, DateTimeOffset? asOf, CancellationToken ct);

    /// <summary>All posting history for an account, newest first.</summary>
    IAsyncEnumerable<Posting> GetPostingsAsync(string accountId, CancellationToken ct);

    /// <summary>
    /// Rebuild the projection from the event stream (paper §12.3: "Projections
    /// are rebuilt from the event stream if needed").
    /// </summary>
    Task RebuildAsync(CancellationToken ct);
}
