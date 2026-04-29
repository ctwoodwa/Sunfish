using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Taxonomy.Models;

namespace Sunfish.Foundation.Taxonomy.Services;

/// <summary>
/// In-memory reference implementation of <see cref="ITaxonomyResolver"/>.
/// Reads from the same backing storage as
/// <see cref="InMemoryTaxonomyRegistry"/>; tombstoned nodes still resolve
/// (per ADR 0056 §"Resolver semantics").
/// </summary>
public sealed class InMemoryTaxonomyResolver : ITaxonomyResolver
{
    private readonly InMemoryTaxonomyRegistry _registry;

    /// <summary>Creates the resolver bound to the given registry.</summary>
    public InMemoryTaxonomyResolver(InMemoryTaxonomyRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc />
    public Task<TaxonomyNode?> ResolveAsync(TenantId tenantId, TaxonomyClassification classification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(classification);
        var key = (tenantId, new TaxonomyNodeId(classification.Definition, classification.Code), classification.Version);
        _registry.NodesSnapshot.TryGetValue(key, out var node);
        return Task.FromResult<TaxonomyNode?>(node);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TaxonomyNode?>> ResolveAllAsync(TenantId tenantId, IReadOnlyList<TaxonomyClassification> classifications, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(classifications);
        var snapshot = _registry.NodesSnapshot;
        var results = new TaxonomyNode?[classifications.Count];
        for (var i = 0; i < classifications.Count; i++)
        {
            var c = classifications[i];
            var key = (tenantId, new TaxonomyNodeId(c.Definition, c.Code), c.Version);
            results[i] = snapshot.TryGetValue(key, out var node) ? node : null;
        }
        return Task.FromResult<IReadOnlyList<TaxonomyNode?>>(results);
    }

    /// <inheritdoc />
    public async Task<bool> IsActiveAsync(TenantId tenantId, TaxonomyClassification classification, CancellationToken ct)
    {
        var node = await ResolveAsync(tenantId, classification, ct).ConfigureAwait(false);
        return node is not null && node.Status == TaxonomyNodeStatus.Active;
    }
}
