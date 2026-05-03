using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Taxonomy.Models;

namespace Sunfish.Foundation.Taxonomy.Services;

/// <summary>
/// Read-side contract for resolving <see cref="TaxonomyClassification"/>
/// references back to the underlying <see cref="TaxonomyNode"/> records.
/// Tombstoned nodes still resolve (returning <see cref="TaxonomyNodeStatus.Tombstoned"/>);
/// consumers decide UX.
/// </summary>
public interface ITaxonomyResolver
{
    /// <summary>Resolves a single classification; returns null when the definition+version+code triple is unknown.</summary>
    Task<TaxonomyNode?> ResolveAsync(TenantId tenantId, TaxonomyClassification classification, CancellationToken ct);

    /// <summary>Resolves a batch of classifications; preserves input order. Unknown entries are returned as nulls.</summary>
    Task<IReadOnlyList<TaxonomyNode?>> ResolveAllAsync(TenantId tenantId, IReadOnlyList<TaxonomyClassification> classifications, CancellationToken ct);

    /// <summary>Returns true iff the classification resolves to an <see cref="TaxonomyNodeStatus.Active"/> node. Tombstoned and unknown both yield false.</summary>
    Task<bool> IsActiveAsync(TenantId tenantId, TaxonomyClassification classification, CancellationToken ct);
}
