namespace Sunfish.Kernel.Ledger;

/// <summary>
/// An immutable posting event — the atomic unit of ledger motion. Paper §12.1:
/// "Financial and CP-class value records are modeled as a double-entry ledger —
/// not as mutable balance fields. Every financial change is represented as
/// immutable posting events."
/// </summary>
/// <remarks>
/// <para>
/// <b>Sign convention:</b> a positive <see cref="Amount"/> is a debit, negative
/// is a credit. The sum of all <see cref="Amount"/> values within a single
/// <see cref="TransactionId"/> is always exactly zero (paper §12.1 invariant).
/// The balance-check uses <see cref="decimal"/> throughout; no floating-point
/// tolerance is required — see
/// <see cref="Transaction.IsBalanced"/>.
/// </para>
/// <para>
/// <b>Currency:</b> we do not attempt multi-currency conversion inside a single
/// <see cref="Transaction"/>; callers that cross currencies must record the
/// conversion as a separate set of postings in the appropriate currency code.
/// </para>
/// </remarks>
/// <param name="PostingId">Globally unique id for this posting. Minted by the caller.</param>
/// <param name="TransactionId">Group id that ties postings together. Every posting that shares a <see cref="TransactionId"/> is part of the same accounting transaction and must sum to zero.</param>
/// <param name="AccountId">Stable account identifier (caller-chosen — the ledger does not enforce a namespace).</param>
/// <param name="Amount">Signed decimal amount. Positive = debit, negative = credit. Per-transaction sum must equal <c>0m</c>.</param>
/// <param name="Currency">ISO-4217 three-letter currency code (e.g. <c>USD</c>, <c>EUR</c>). Not validated by the ledger.</param>
/// <param name="PostedAt">Wall-clock time at which the economic event occurred. Used by <see cref="Sunfish.Kernel.Ledger.CQRS.IBalanceProjection"/> for as-of queries and by <see cref="Sunfish.Kernel.Ledger.Periods.IPeriodCloser"/> for period-closing.</param>
/// <param name="Description">Free-form memo. Surfaced in statements.</param>
/// <param name="Metadata">Arbitrary caller-supplied tags (source-event id, channel, etc.). The ledger does not interpret any keys here.</param>
public sealed record Posting(
    Guid PostingId,
    Guid TransactionId,
    string AccountId,
    decimal Amount,
    string Currency,
    DateTimeOffset PostedAt,
    string Description,
    IReadOnlyDictionary<string, string> Metadata);
