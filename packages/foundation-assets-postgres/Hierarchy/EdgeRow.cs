namespace Sunfish.Foundation.Assets.Postgres.Hierarchy;

/// <summary>
/// EF Core row mapped to the <c>hierarchy_edges</c> table.
/// </summary>
/// <remarks>
/// Each edge is temporal: <see cref="ValidFrom"/> is set when it is added, <see cref="ValidTo"/>
/// is null while the edge is still open and is populated when the edge is invalidated.
/// </remarks>
public sealed class EdgeRow
{
    /// <summary>Surrogate identifier (sequence-generated).</summary>
    public long EdgeId { get; set; }

    /// <summary>Source endpoint scheme.</summary>
    public required string FromScheme { get; set; }

    /// <summary>Source endpoint authority.</summary>
    public required string FromAuthority { get; set; }

    /// <summary>Source endpoint local-part.</summary>
    public required string FromLocalPart { get; set; }

    /// <summary>Target endpoint scheme.</summary>
    public required string ToScheme { get; set; }

    /// <summary>Target endpoint authority.</summary>
    public required string ToAuthority { get; set; }

    /// <summary>Target endpoint local-part.</summary>
    public required string ToLocalPart { get; set; }

    /// <summary>Edge kind enum (see <c>Sunfish.Foundation.Assets.Hierarchy.EdgeKind</c>).</summary>
    public int Kind { get; set; }

    /// <summary>When the edge becomes valid.</summary>
    public DateTimeOffset ValidFrom { get; set; }

    /// <summary>When the edge's validity closes (null = still open).</summary>
    public DateTimeOffset? ValidTo { get; set; }

    /// <summary>Optional metadata payload.</summary>
    public string? MetadataJson { get; set; }
}
