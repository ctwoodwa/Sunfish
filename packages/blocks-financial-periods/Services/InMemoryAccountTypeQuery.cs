using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// In-memory <see cref="IAccountTypeQuery"/> for tests + demos.
/// Production hosts wire a SQLite-backed implementation.
/// </summary>
public sealed class InMemoryAccountTypeQuery : IAccountTypeQuery
{
    private readonly ConcurrentDictionary<GLAccountId, GLAccount> _accounts = new();

    /// <summary>Seed an account into the query store.</summary>
    public void Upsert(GLAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        _accounts[account.Id] = account;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GLAccount>> GetByTypeAsync(
        ChartOfAccountsId chartId,
        GLAccountType type,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GLAccount> rows = _accounts.Values
            .Where(a => a.Type == type
                        && a.ChartId is { } c && c.Equals(chartId)
                        && a.IsPostable)
            .ToList();
        return Task.FromResult(rows);
    }
}
