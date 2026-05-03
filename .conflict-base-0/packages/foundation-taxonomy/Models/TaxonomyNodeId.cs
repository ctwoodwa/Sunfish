namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// Composite key for a taxonomy node — the definition id (which taxonomy)
/// plus the node's stable code (which entry within it).
/// </summary>
/// <param name="Definition">Identity of the parent <see cref="TaxonomyDefinition"/>.</param>
/// <param name="Code">Stable, lowercase, dash-separated code (e.g., <c>lease-execution</c>).</param>
public readonly record struct TaxonomyNodeId(TaxonomyDefinitionId Definition, string Code)
{
    /// <summary>Identity rendered as <c>Vendor.Domain.TaxonomyName/code</c>.</summary>
    public override string ToString() => $"{Definition}/{Code}";
}
