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

    /// <summary>
    /// Snapshot of all accounts currently in the resolver — used by the
    /// W#60 P4 PR 6 ERPNext importers to look up by
    /// <see cref="GLAccount.ExternalRef"/>. Production resolvers will
    /// expose a dedicated GetByExternalRef API; this snapshot suffices
    /// for the in-memory path.
    /// </summary>
    public IReadOnlyList<GLAccount> SeededAccounts => _accounts.Values.ToList();

    /// <inheritdoc />
    public Task<GLAccount?> GetAsync(GLAccountId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_accounts.TryGetValue(id, out var a) ? a : null);
}
