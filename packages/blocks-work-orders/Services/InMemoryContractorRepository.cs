using System.Collections.Concurrent;
using Sunfish.Blocks.WorkOrders.Models;

namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// In-memory <see cref="IContractorReadModel"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Also surfaces an
/// <see cref="Upsert"/> write API for the writer-side tests + the
/// kitchen-sink demo seed path. Production hosts wire a SQLite-backed
/// implementation in a follow-on persistence hand-off.
/// </summary>
public sealed class InMemoryContractorRepository : IContractorReadModel
{
    private readonly ConcurrentDictionary<ContractorId, Contractor> _byId = new();

    /// <summary>Insert or replace a contractor.</summary>
    public void Upsert(Contractor contractor)
    {
        ArgumentNullException.ThrowIfNull(contractor);
        _byId[contractor.Id] = contractor;
    }

    /// <inheritdoc />
    public Task<Contractor?> GetByIdAsync(ContractorId id, CancellationToken cancellationToken = default)
        => Task.FromResult(_byId.TryGetValue(id, out var c) ? c : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<Contractor>> FindByTradeAsync(
        TradeCategory trade,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Contractor> rows = _byId.Values
            .Where(c => c.Status == ContractorStatus.Active
                        && c.DeletedAt is null
                        && c.Trades.Contains(trade))
            .ToList();
        return Task.FromResult(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Contractor>> GetPreferredContractorsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Contractor> rows = _byId.Values
            .Where(c => c.Status == ContractorStatus.Active
                        && c.DeletedAt is null
                        && c.PreferredFlag)
            .OrderByDescending(c => c.Rating ?? decimal.MinValue)
            .ToList();
        return Task.FromResult(rows);
    }
}
