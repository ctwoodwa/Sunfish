namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// Lifecycle status of a single taxonomy node within a definition version.
/// </summary>
public enum TaxonomyNodeStatus
{
    /// <summary>Valid for resolution and new classifications.</summary>
    Active,

    /// <summary>Soft-deleted; resolves but flagged. New classifications discouraged; tombstoning is monotonic — a tombstoned node may not transition back to <see cref="Active"/>.</summary>
    Tombstoned
}
