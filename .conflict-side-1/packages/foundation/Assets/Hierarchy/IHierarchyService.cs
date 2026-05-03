using System.Text.Json;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Hierarchy;

/// <summary>
/// Persistent hierarchy service.
/// </summary>
/// <remarks>
/// Exposes mutation ops for <see cref="EntityEdge"/>s plus temporal read queries that walk
/// the materialized closure table. Plan D-HIERARCHY.
/// </remarks>
public interface IHierarchyService
{
    /// <summary>Adds a new edge. Closure rows are maintained synchronously.</summary>
    Task<EntityEdge> AddEdgeAsync(
        EntityId from,
        EntityId to,
        EdgeKind kind,
        DateTimeOffset validFrom,
        JsonDocument? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates an edge at the given instant. Closure rows touching the edge have their
    /// <c>ValidTo</c> set accordingly.
    /// </summary>
    Task InvalidateEdgeAsync(long edgeId, DateTimeOffset validTo, CancellationToken ct = default);

    /// <summary>Streams direct children of <paramref name="parent"/> valid at <paramref name="asOf"/>.</summary>
    IAsyncEnumerable<EntityEdge> GetChildrenAsync(EntityId parent, DateTimeOffset? asOf = null, CancellationToken ct = default);

    /// <summary>Streams direct parents of <paramref name="child"/> valid at <paramref name="asOf"/>.</summary>
    IAsyncEnumerable<EntityEdge> GetParentsAsync(EntityId child, DateTimeOffset? asOf = null, CancellationToken ct = default);

    /// <summary>Streams ancestor-descendant closure rows valid at <paramref name="asOf"/>.</summary>
    IAsyncEnumerable<ClosureEntry> GetAncestorsAsync(EntityId descendant, DateTimeOffset? asOf = null, CancellationToken ct = default);

    /// <summary>Streams descendant closure rows valid at <paramref name="asOf"/>.</summary>
    IAsyncEnumerable<ClosureEntry> GetDescendantsAsync(EntityId ancestor, DateTimeOffset? asOf = null, CancellationToken ct = default);

    /// <summary>Returns a full as-of snapshot of the subtree rooted at <paramref name="root"/>.</summary>
    Task<TemporalSnapshot> GetSubtreeAsync(EntityId root, DateTimeOffset? asOf = null, CancellationToken ct = default);
}
