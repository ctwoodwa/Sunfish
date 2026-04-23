namespace Sunfish.Kernel.Ledger.Periods;

/// <summary>
/// Read-side state snapshot consulted by <see cref="Sunfish.Kernel.Ledger.PostingEngine"/>
/// to decide whether an incoming posting falls inside a closed period. Kept
/// deliberately small so the posting hot-path never needs to replay the log.
/// </summary>
public interface IPeriodCloseState
{
    /// <summary>
    /// The most recent closed period end (inclusive). Null if no periods are
    /// closed yet. A posting with <see cref="Sunfish.Kernel.Ledger.Posting.PostedAt"/>
    /// ≤ this value is redirected to an <c>adjustments-yyyyMMdd</c> account.
    /// </summary>
    DateTimeOffset? LastClosedPeriodEnd { get; }
}
