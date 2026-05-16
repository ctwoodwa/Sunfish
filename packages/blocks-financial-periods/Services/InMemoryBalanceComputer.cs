using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// <b>TESTS ONLY.</b> In-memory <see cref="IBalanceComputer"/>:
/// returns a caller-seeded balance per <see cref="GLAccountId"/>;
/// ignores the as-of date entirely. Sufficient for the year-end-close
/// happy + edge paths; kitchen-sink demos + production hosts MUST
/// wire the SQLite-backed implementation (lands with the persistence
/// wiring) so as-of filtering + prior-year roll-over isolation work
/// correctly. The InMemory fake intentionally collapses every date
/// to the same seeded value — if a demo accidentally uses this, the
/// year-end close will sweep lifetime balances into the current FY's
/// retained earnings (addendum M3).
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
