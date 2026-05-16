using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// In-memory <see cref="IBalanceComputer"/> for tests: returns a
/// caller-seeded balance per <see cref="GLAccountId"/>; ignores the
/// as-of date. Sufficient for the year-end-close happy + edge paths;
/// the SQLite-backed production implementation (lands with the
/// persistence wiring) does the real sum-query.
/// </summary>
public sealed class InMemoryBalanceComputer : IBalanceComputer
{
    private readonly ConcurrentDictionary<GLAccountId, decimal> _balances = new();

    /// <summary>
    /// Seed or replace the balance for the supplied account. Returned
    /// to every <see cref="ComputeAsOfAsync"/> call regardless of date.
    /// </summary>
    public void Seed(GLAccountId accountId, decimal balance)
        => _balances[accountId] = balance;

    /// <inheritdoc />
    public Task<decimal> ComputeAsOfAsync(
        GLAccountId accountId,
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_balances.TryGetValue(accountId, out var b) ? b : 0m);
}
