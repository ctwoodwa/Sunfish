namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// Derivation operation that produced a taxonomy definition (ADR 0056
/// §"Lineage operations").
/// </summary>
public enum TaxonomyLineageOp
{
    /// <summary>The definition was created from scratch (no ancestor).</summary>
    InitialPublication,

    /// <summary>Copy of an ancestor with a new identity; node set initially identical.</summary>
    Clone,

    /// <summary>Derivation that adds new nodes; ancestor nodes are inherited unchanged.</summary>
    Extend,

    /// <summary>Derivation with a revised node set (renames, removals); breaks consumers pinned to the ancestor. Requires explicit reason.</summary>
    Alter
}
