namespace Sunfish.Foundation.Assets.Postgres.Hierarchy;

/// <summary>
/// EF Core row mapped to the <c>hierarchy_closure</c> table.
/// </summary>
/// <remarks>
/// One row per (ancestor, descendant, depth) over a given validity window. The composite
/// key is surrogate (<see cref="ClosureId"/>) so two temporally-disjoint rows with the same
/// (ancestor, descendant, depth) can coexist (re-parent → close → re-open).
/// </remarks>
public sealed class ClosureRow
{
    /// <summary>Surrogate identifier.</summary>
    public long ClosureId { get; set; }

    /// <summary>Ancestor scheme.</summary>
    public required string AncestorScheme { get; set; }

    /// <summary>Ancestor authority.</summary>
    public required string AncestorAuthority { get; set; }

    /// <summary>Ancestor local-part.</summary>
    public required string AncestorLocalPart { get; set; }

    /// <summary>Descendant scheme.</summary>
    public required string DescendantScheme { get; set; }

    /// <summary>Descendant authority.</summary>
    public required string DescendantAuthority { get; set; }

    /// <summary>Descendant local-part.</summary>
    public required string DescendantLocalPart { get; set; }

    /// <summary>Depth in the closure; 0 = self.</summary>
    public int Depth { get; set; }

    /// <summary>When this closure row becomes valid.</summary>
    public DateTimeOffset ValidFrom { get; set; }

    /// <summary>When this closure row's validity closes (null = still open).</summary>
    public DateTimeOffset? ValidTo { get; set; }
}
