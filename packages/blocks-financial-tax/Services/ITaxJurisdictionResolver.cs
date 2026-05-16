using Sunfish.Blocks.FinancialTax.Models;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// Walks the jurisdiction tree to produce the set of jurisdictions
/// that apply to a given location or property. Consumed by the PR 3
/// calculation engine when it iterates rates per-jurisdiction.
/// </summary>
public interface ITaxJurisdictionResolver
{
    /// <summary>
    /// Resolve the applicable jurisdictions for a given address or
    /// property location. Returns from-most-local to most-senior order
    /// (City → County → State → Federal / Country); callers that want
    /// outermost-first ordering (e.g. compound-tax application per
    /// Stage 02 §6.4) re-sort via
    /// <see cref="JurisdictionLevelExtensions.OrderIndex"/>.
    /// </summary>
    /// <param name="context">Where the transaction occurred / which property it ties to.</param>
    /// <param name="cancellationToken">Standard.</param>
    /// <returns>
    /// Ordered list of applicable jurisdictions (most-local first).
    /// Empty when the location is unknown to the store.
    /// </returns>
    Task<IReadOnlyList<TaxJurisdiction>> ResolveAsync(
        TaxLocationContext context,
        CancellationToken cancellationToken = default);
}
