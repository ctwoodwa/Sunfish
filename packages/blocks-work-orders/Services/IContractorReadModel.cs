using Sunfish.Blocks.WorkOrders.Models;

namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// Read-side projection over <see cref="Contractor"/>. Backs the
/// contractor-picker UI in work-order dispatch + the
/// preferred-contractor surface in maintenance scheduling.
/// </summary>
public interface IContractorReadModel
{
    /// <summary>Fetch by id, or <c>null</c> when unknown.</summary>
    Task<Contractor?> GetByIdAsync(ContractorId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// List Active contractors that match the supplied
    /// <paramref name="trade"/>. Excludes Paused / Blacklisted /
    /// Archived rows + soft-deleted rows.
    /// </summary>
    Task<IReadOnlyList<Contractor>> FindByTradeAsync(
        TradeCategory trade,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List Active contractors flagged
    /// <see cref="Contractor.PreferredFlag"/> = true. Ordered by
    /// <see cref="Contractor.Rating"/> descending (nulls last).
    /// </summary>
    Task<IReadOnlyList<Contractor>> GetPreferredContractorsAsync(
        CancellationToken cancellationToken = default);
}
