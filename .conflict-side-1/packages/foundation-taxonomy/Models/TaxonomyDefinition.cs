using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// Top-level taxonomy product (a versioned, governed bundle of nodes).
/// </summary>
public sealed record TaxonomyDefinition
{
    /// <summary>Three-part identity (Vendor.Domain.TaxonomyName).</summary>
    public required TaxonomyDefinitionId Id { get; init; }

    /// <summary>Semver version of this definition.</summary>
    public required TaxonomyVersion Version { get; init; }

    /// <summary>Governance posture (Civilian / Enterprise / Authoritative).</summary>
    public required TaxonomyGovernanceRegime Governance { get; init; }

    /// <summary>Free-text description of the taxonomy's purpose.</summary>
    public required string Description { get; init; }

    /// <summary>Owning actor. Authoritative-regime definitions must be owned by <see cref="ActorId.Sunfish"/>; Civilian/Enterprise are tenant-scoped actors.</summary>
    public required ActorId Owner { get; init; }

    /// <summary>Wall-clock time the version was published.</summary>
    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>Wall-clock time the version was retired; null while the version is current.</summary>
    public DateTimeOffset? RetiredAt { get; init; }

    /// <summary>Lineage record describing how this definition was derived; null for <see cref="TaxonomyLineageOp.InitialPublication"/> definitions.</summary>
    public TaxonomyLineage? DerivedFrom { get; init; }
}
