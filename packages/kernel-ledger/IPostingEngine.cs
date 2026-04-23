namespace Sunfish.Kernel.Ledger;

/// <summary>
/// Paper §12.2 posting engine. Converts balanced <see cref="Transaction"/>s
/// into committed ledger postings under distributed lease coordination, with
/// idempotency at the <see cref="Transaction.IdempotencyKey"/> level.
/// </summary>
/// <remarks>
/// <para>
/// The posting engine is the single write-side authority for the ledger. All
/// balance-mutating events must flow through <see cref="PostAsync"/> — direct
/// writes to the event log would bypass balance validation, idempotency, and
/// lease coordination.
/// </para>
/// <para>
/// Corrections are made via <see cref="CompensateAsync"/>, which emits a
/// reversing transaction. The original transaction remains in the log unchanged
/// (paper §12.1: "Postings are immutable; corrections use compensating entries").
/// </para>
/// </remarks>
public interface IPostingEngine
{
    /// <summary>
    /// Apply a transaction. Idempotent by <see cref="Transaction.IdempotencyKey"/> —
    /// processing the same key twice yields at most one transaction in the ledger.
    /// Uses kernel-lease for CP-class serialization against the affected accounts.
    /// </summary>
    /// <param name="tx">The balanced transaction to commit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PostingResult"/>. <c>Success = false</c> with a non-null
    /// <see cref="PostingResult.RejectionReason"/> indicates a validation or
    /// quorum failure; <c>Success = true</c> returns the committed transaction
    /// id and the event-log sequence number of the <see cref="PostingsAppliedEvent"/>.
    /// </returns>
    Task<PostingResult> PostAsync(Transaction tx, CancellationToken ct);

    /// <summary>
    /// Record a compensating entry. Paper §12.1: "corrections use compensating
    /// entries." The engine constructs a reversing transaction whose postings
    /// negate the original; the sum is zero and the original's postings are
    /// preserved unchanged in the log.
    /// </summary>
    /// <param name="originalTransactionId">Id of the transaction to compensate.</param>
    /// <param name="reason">Human-readable reason string.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PostingResult> CompensateAsync(Guid originalTransactionId, string reason, CancellationToken ct);
}

/// <summary>
/// Outcome of an <see cref="IPostingEngine.PostAsync"/> or
/// <see cref="IPostingEngine.CompensateAsync"/> call.
/// </summary>
/// <param name="Success">True iff the transaction was committed (or was a duplicate of a previously-committed transaction).</param>
/// <param name="TransactionId">The committed transaction's id. For a duplicate <see cref="Transaction.IdempotencyKey"/>, this is the id of the <i>previously</i>-committed transaction — never a new id.</param>
/// <param name="RejectionReason">Non-null iff <see cref="Success"/> is false. Canonical values: <c>UNBALANCED</c>, <c>QUORUM_UNAVAILABLE</c>, <c>CLOSED_PERIOD_NOT_REDIRECTABLE</c>, <c>NOT_FOUND</c>.</param>
/// <param name="LogSequence">Sequence number of the <see cref="PostingsAppliedEvent"/> in the kernel event log. Null on duplicate (no new log entry) or rejection.</param>
public sealed record PostingResult(
    bool Success,
    Guid TransactionId,
    string? RejectionReason,
    ulong? LogSequence);
