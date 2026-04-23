namespace Sunfish.Kernel.Ledger.CQRS;

/// <summary>
/// CQRS read-side — period statements. Paper §12.3.
/// </summary>
public interface IStatementProjection
{
    /// <summary>
    /// Produce a <see cref="Statement"/> for <paramref name="accountId"/> covering
    /// <paramref name="periodStart"/> (inclusive) through <paramref name="periodEnd"/> (inclusive).
    /// </summary>
    Task<Statement> GetStatementAsync(
        string accountId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken ct);
}

/// <summary>
/// A period statement for a single account.
/// </summary>
/// <param name="AccountId">The account the statement is for.</param>
/// <param name="PeriodStart">Inclusive start of the statement period.</param>
/// <param name="PeriodEnd">Inclusive end of the statement period.</param>
/// <param name="OpeningBalance">Sum of every posting with <see cref="Posting.PostedAt"/> &lt; <paramref name="PeriodStart"/>.</param>
/// <param name="ClosingBalance">Sum of every posting with <see cref="Posting.PostedAt"/> ≤ <paramref name="PeriodEnd"/>.</param>
/// <param name="Postings">Postings within the period, oldest first.</param>
public sealed record Statement(
    string AccountId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<Posting> Postings);
