using Sunfish.Blocks.FinancialTax.Models;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// CRUD surface for <see cref="TaxJurisdiction"/> rows. PR 1 ships the
/// in-memory implementation only; the SQLite-backed implementation
/// lands in a later persistence-layer hand-off.
/// </summary>
public interface ITaxJurisdictionStore
{
    /// <summary>Look up by id. Returns null when missing or soft-deleted.</summary>
    Task<TaxJurisdiction?> GetAsync(TaxJurisdictionId id, CancellationToken cancellationToken = default);

    /// <summary>All active rows at the given level. Soft-deleted rows are excluded.</summary>
    Task<IReadOnlyList<TaxJurisdiction>> GetByLevelAsync(JurisdictionLevel level, CancellationToken cancellationToken = default);

    /// <summary>Direct children of a parent. Soft-deleted rows are excluded.</summary>
    Task<IReadOnlyList<TaxJurisdiction>> GetChildrenAsync(TaxJurisdictionId parentId, CancellationToken cancellationToken = default);

    /// <summary>Insert or update by id. Implementations bump <c>UpdatedAtUtc</c>.</summary>
    Task UpsertAsync(TaxJurisdiction jurisdiction, CancellationToken cancellationToken = default);
}
