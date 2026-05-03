using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// Audit trail of how a taxonomy definition was derived from an ancestor
/// (ADR 0056 §"Lineage"). Immutable once attached to a definition.
/// </summary>
public sealed record TaxonomyLineage
{
    /// <summary>Operation that produced this definition.</summary>
    public required TaxonomyLineageOp Operation { get; init; }

    /// <summary>Identity of the ancestor definition; not set for <see cref="TaxonomyLineageOp.InitialPublication"/>.</summary>
    public required TaxonomyDefinitionId AncestorDefinition { get; init; }

    /// <summary>Version of the ancestor that this definition was derived from.</summary>
    public required TaxonomyVersion AncestorVersion { get; init; }

    /// <summary>Actor that performed the derivation.</summary>
    public required ActorId DerivedBy { get; init; }

    /// <summary>Wall-clock time of the derivation.</summary>
    public required DateTimeOffset DerivedAt { get; init; }

    /// <summary>Free-text rationale; required for <see cref="TaxonomyLineageOp.Alter"/>, optional otherwise.</summary>
    public required string Reason { get; init; }
}
