using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// Resolves a <see cref="GLAccount"/> by id. Implementations include the
/// in-memory test fake (<see cref="InMemoryAccountResolver"/>) and the
/// SQLite-backed production resolver that ships with the persistence
/// hand-off.
/// </summary>
public interface IAccountResolver
{
    /// <summary>
    /// Look up the account by id. Returns <c>null</c> when no such
    /// account exists in the resolver's backing store.
    /// </summary>
    Task<GLAccount?> GetAsync(GLAccountId id, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory <see cref="IAccountResolver"/> backed by a
/// <see cref="Dictionary{TKey,TValue}"/>. Test setup seeds the dictionary;
/// production uses a SQLite-backed implementation in a later persistence
/// hand-off.
/// </summary>
public sealed class InMemoryAccountResolver : IAccountResolver
{
    private readonly Dictionary<GLAccountId, GLAccount> _accounts;

    public InMemoryAccountResolver(IEnumerable<GLAccount>? seed = null)
    {
        _accounts = (seed ?? Array.Empty<GLAccount>())
            .ToDictionary(a => a.Id);
    }

    /// <summary>Seed or replace an account in the backing dictionary.</summary>
    public void Upsert(GLAccount account) => _accounts[account.Id] = account;

    /// <inheritdoc />
    public Task<GLAccount?> GetAsync(GLAccountId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_accounts.TryGetValue(id, out var a) ? a : null);
}
