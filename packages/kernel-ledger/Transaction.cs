namespace Sunfish.Kernel.Ledger;

/// <summary>
/// A set of <see cref="Posting"/>s that collectively move value between accounts.
/// Paper §12.1 invariants:
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description>Each transaction produces at least two postings.</description></item>
///   <item><description>The sum of all posting amounts per transaction is always zero.</description></item>
///   <item><description>Postings are immutable; corrections use compensating entries.</description></item>
/// </list>
/// <para>
/// <see cref="IsBalanced"/> uses <see cref="decimal"/> equality (<c>Sum == 0m</c>)
/// rather than a floating-point tolerance — decimal preserves exact base-10
/// arithmetic, so balancing entries that sum to 0 numerically really do compare
/// equal to <c>0m</c>.
/// </para>
/// </remarks>
/// <param name="TransactionId">Shared group id across the postings.</param>
/// <param name="IdempotencyKey">Stable, unique identifier supplied by the posting source (typically the upstream domain event id). The posting engine dedupes on this key so re-processing the same domain event yields at most one transaction in the ledger (paper §12.2).</param>
/// <param name="Postings">The postings that make up this transaction. Must contain at least two entries that sum to zero.</param>
/// <param name="CreatedAt">Wall-clock time at which the transaction was assembled (before posting). The ledger preserves the value without interpreting it.</param>
public sealed record Transaction(
    Guid TransactionId,
    string IdempotencyKey,
    IReadOnlyList<Posting> Postings,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// Signed sum of every <see cref="Posting.Amount"/> in <see cref="Postings"/>.
    /// Should be exactly <c>0m</c> for a valid double-entry transaction.
    /// </summary>
    public decimal Sum => Postings.Sum(p => p.Amount);

    /// <summary>
    /// True when <see cref="Sum"/> is exactly zero (decimal equality — no
    /// floating-point tolerance). Paper §12.1: "The sum of all posting amounts
    /// per transaction is always zero."
    /// </summary>
    public bool IsBalanced => Sum == 0m;
}
