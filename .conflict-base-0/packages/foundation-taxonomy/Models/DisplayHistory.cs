namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// One revision of a taxonomy node's display label or description; preserved
/// alongside the node so consumers can render historical labels for archived
/// classifications.
/// </summary>
public sealed record DisplayHistory
{
    /// <summary>Display label captured at the revision moment.</summary>
    public required string Display { get; init; }

    /// <summary>Description captured at the revision moment.</summary>
    public required string Description { get; init; }

    /// <summary>Wall-clock time of the revision.</summary>
    public required DateTimeOffset RevisedAt { get; init; }

    /// <summary>Free-text rationale; null when no reason was supplied.</summary>
    public string? RevisionReason { get; init; }
}
