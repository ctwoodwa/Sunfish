namespace Sunfish.Kernel.Ledger;

/// <summary>
/// A committed ledger transaction. Appended to the kernel event log by the
/// posting engine; consumed by projections to materialize balances and
/// statements. Paper §12.1 / §12.2.
/// </summary>
/// <param name="Transaction">The committed <see cref="Transaction"/>. Invariants are enforced at posting time; consumers may treat the transaction as balanced without re-checking.</param>
public sealed record PostingsAppliedEvent(Transaction Transaction);

/// <summary>
/// A compensating entry recorded against a prior transaction. Paper §12.1:
/// "Postings are immutable; corrections use compensating entries." The
/// compensating <see cref="Transaction"/> contains postings whose amounts negate
/// the original, and shares an <see cref="Transaction.IdempotencyKey"/> derived
/// from the original transaction id plus a reason tag.
/// </summary>
/// <param name="OriginalTransactionId">Id of the transaction being compensated.</param>
/// <param name="CompensatingTx">The reversing transaction. Its postings sum to zero like any other balanced transaction.</param>
/// <param name="Reason">Human-readable reason string. Surfaced in statements and audit logs.</param>
public sealed record CompensationAppliedEvent(Guid OriginalTransactionId, Transaction CompensatingTx, string Reason);

/// <summary>
/// A period-close rollup. Paper §12.4: "At period close, the projection engine
/// computes rollup snapshots (account balances, P&amp;L summaries) stored as
/// closing events. Subsequent postings affecting closed periods are directed to
/// adjustment accounts in the next open period."
/// </summary>
/// <param name="PeriodEnd">Inclusive end of the closed period. Subsequent posts with <see cref="Posting.PostedAt"/> ≤ this boundary are redirected to <c>adjustments-yyyyMMdd</c>.</param>
/// <param name="ClosingBalances">Account → closing-balance snapshot captured at close. Used by statement projections as a known-good starting point so subsequent balance queries do not need to replay pre-close postings.</param>
public sealed record PeriodClosedEvent(
    DateTimeOffset PeriodEnd,
    IReadOnlyDictionary<string, decimal> ClosingBalances);
