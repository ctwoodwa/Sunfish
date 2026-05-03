namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// One entry within a taxonomy definition version. Nodes form a forest:
/// roots have <see cref="ParentCode"/> = <c>null</c>; children reference
/// a sibling node's <see cref="TaxonomyNodeId.Code"/>.
/// </summary>
public sealed record TaxonomyNode
{
    /// <summary>Composite key (definition + code).</summary>
    public required TaxonomyNodeId Id { get; init; }

    /// <summary>Version of the definition this node belongs to.</summary>
    public required TaxonomyVersion DefinitionVersion { get; init; }

    /// <summary>Current display label.</summary>
    public required string Display { get; init; }

    /// <summary>Current description.</summary>
    public required string Description { get; init; }

    /// <summary>Code of the parent node within the same definition; null for root nodes.</summary>
    public string? ParentCode { get; init; }

    /// <summary>Lifecycle status. New classifications against tombstoned nodes are discouraged.</summary>
    public required TaxonomyNodeStatus Status { get; init; }

    /// <summary>Wall-clock time the node was first added to its definition.</summary>
    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>Wall-clock time the node was tombstoned; null while <see cref="Status"/> is <see cref="TaxonomyNodeStatus.Active"/>.</summary>
    public DateTimeOffset? TombstonedAt { get; init; }

    /// <summary>Recommendation for consumers: code of the active node that supersedes this one. Null when no successor exists.</summary>
    public string? SuccessorCode { get; init; }

    /// <summary>Free-text rationale captured at tombstone time. Null while active.</summary>
    public string? DeprecationReason { get; init; }

    /// <summary>Trail of prior display revisions, oldest first. Empty when the display has never changed.</summary>
    public IReadOnlyList<DisplayHistory> DisplayHistoryEntries { get; init; } = Array.Empty<DisplayHistory>();
}
