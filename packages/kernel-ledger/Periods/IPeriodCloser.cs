namespace Sunfish.Kernel.Ledger.Periods;

/// <summary>
/// Paper §12.4 period closer. "At period close, the projection engine computes
/// rollup snapshots (account balances, P&amp;L summaries) stored as closing
/// events. Subsequent postings affecting closed periods are directed to
/// adjustment accounts in the next open period."
/// </summary>
public interface IPeriodCloser
{
    /// <summary>
    /// Close a period. Computes and stores rollup snapshots as a
    /// <see cref="Sunfish.Kernel.Ledger.PeriodClosedEvent"/>. After close, later
    /// postings whose <see cref="Sunfish.Kernel.Ledger.Posting.PostedAt"/>
    /// falls inside the closed period are redirected to
    /// <c>adjustments-yyyyMMdd</c> accounts in the next open period.
    /// </summary>
    Task<PeriodCloseResult> CloseAsync(DateTimeOffset periodEnd, CancellationToken ct);

    /// <summary>
    /// The most recent closed period end (inclusive). Null if no periods are
    /// closed yet.
    /// </summary>
    Task<DateTimeOffset?> LastClosedPeriodEndAsync(CancellationToken ct);
}

/// <summary>
/// Outcome of a <see cref="IPeriodCloser.CloseAsync"/> call.
/// </summary>
/// <param name="PeriodEnd">The inclusive period end that was closed.</param>
/// <param name="AccountCount">Number of distinct accounts with balances in the snapshot.</param>
/// <param name="ClosingBalances">Account id → closing balance at <paramref name="PeriodEnd"/>.</param>
public sealed record PeriodCloseResult(
    DateTimeOffset PeriodEnd,
    ulong AccountCount,
    IReadOnlyDictionary<string, decimal> ClosingBalances);
