using System.Runtime.CompilerServices;
using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Temporal;

namespace Sunfish.Foundation.Assets.Hierarchy;

/// <summary>
/// Zero-dependency in-memory <see cref="IHierarchyService"/>.
/// </summary>
/// <remarks>
/// Maintains an append-only list of <see cref="EntityEdge"/> rows and a synchronously-updated
/// closure table. Plan D-HIERARCHY.
/// </remarks>
public sealed class InMemoryHierarchyService : IHierarchyService
{
    private readonly object _lock = new();
    private readonly List<EntityEdge> _edges = new();
    private readonly List<ClosureEntry> _closure = new();
    private long _nextEdgeId;

    /// <inheritdoc />
    public Task<EntityEdge> AddEdgeAsync(
        EntityId from,
        EntityId to,
        EdgeKind kind,
        DateTimeOffset validFrom,
        JsonDocument? metadata = null,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            var id = Interlocked.Increment(ref _nextEdgeId);
            var edge = new EntityEdge(id, from, to, kind, new TemporalRange(validFrom, null), metadata);
            _edges.Add(edge);

            if (kind == EdgeKind.ChildOf)
            {
                // Closure is built as: ancestor → descendant, where `to` is the parent and `from` is the child.
                // (Plan semantics: AddEdgeAsync(child, parent, ChildOf) — i.e. `from` is the child
                // and `to` is the parent. We keep that interpretation consistent across the codebase.)
                // Ensure self-rows for both endpoints.
                EnsureSelfRow(from, validFrom);
                EnsureSelfRow(to, validFrom);

                // For every ancestor A of `to` (including `to` itself), and every descendant D of `from`
                // (including `from` itself), add closure (A, D, depthA→to + 1 + depthFrom→D).
                var parentAncestors = _closure
                    .Where(c => c.Descendant == to && c.Validity.IsValidAt(validFrom))
                    .ToList();
                var childDescendants = _closure
                    .Where(c => c.Ancestor == from && c.Validity.IsValidAt(validFrom))
                    .ToList();

                foreach (var pa in parentAncestors)
                {
                    foreach (var cd in childDescendants)
                    {
                        var newDepth = pa.Depth + 1 + cd.Depth;
                        // Avoid duplicate active rows with the same (ancestor, descendant, depth).
                        bool exists = _closure.Any(x =>
                            x.Ancestor == pa.Ancestor &&
                            x.Descendant == cd.Descendant &&
                            x.Depth == newDepth &&
                            x.Validity.IsValidAt(validFrom));
                        if (!exists)
                        {
                            _closure.Add(new ClosureEntry(
                                pa.Ancestor,
                                cd.Descendant,
                                newDepth,
                                new TemporalRange(validFrom, null)));
                        }
                    }
                }
            }

            return Task.FromResult(edge);
        }
    }

    private void EnsureSelfRow(EntityId entity, DateTimeOffset validFrom)
    {
        if (!_closure.Any(c => c.Ancestor == entity && c.Descendant == entity && c.Depth == 0))
            _closure.Add(new ClosureEntry(entity, entity, 0, new TemporalRange(DateTimeOffset.MinValue, null)));
    }

    /// <inheritdoc />
    public Task InvalidateEdgeAsync(long edgeId, DateTimeOffset validTo, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var idx = _edges.FindIndex(e => e.Id == edgeId);
            if (idx < 0) throw new InvalidOperationException($"Edge {edgeId} not found.");
            var edge = _edges[idx];
            if (edge.Validity.ValidTo is not null) return Task.CompletedTask;

            _edges[idx] = edge with { Validity = new TemporalRange(edge.Validity.ValidFrom, validTo) };

            if (edge.Kind == EdgeKind.ChildOf)
            {
                // The edge connects child=from → parent=to. Its disappearance affects every
                // closure row where ancestor ∈ ancestors-of(parent) and descendant ∈ descendants-of(child).
                // We keep it simple: for every still-open closure row whose ancestor is an ancestor of `to`
                // (or is `to` itself) AND whose descendant is a descendant of `from` (or is `from` itself)
                // AND depth > 0, close it.
                var ancestorsOfParent = _closure
                    .Where(c => c.Descendant == edge.To && c.Validity.IsValidAt(validTo))
                    .Select(c => c.Ancestor)
                    .ToHashSet();
                var descendantsOfChild = _closure
                    .Where(c => c.Ancestor == edge.From && c.Validity.IsValidAt(validTo))
                    .Select(c => c.Descendant)
                    .ToHashSet();

                for (int i = 0; i < _closure.Count; i++)
                {
                    var row = _closure[i];
                    if (row.Depth == 0) continue;
                    if (row.Validity.ValidTo is not null) continue;
                    if (!ancestorsOfParent.Contains(row.Ancestor)) continue;
                    if (!descendantsOfChild.Contains(row.Descendant)) continue;
                    _closure[i] = row with { Validity = new TemporalRange(row.Validity.ValidFrom, validTo) };
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EntityEdge> GetChildrenAsync(EntityId parent, DateTimeOffset? asOf = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var t = asOf ?? DateTimeOffset.UtcNow;
        EntityEdge[] snapshot;
        lock (_lock)
        {
            snapshot = _edges
                .Where(e => e.Kind == EdgeKind.ChildOf && e.To == parent && e.Validity.IsValidAt(t))
                .ToArray();
        }
        foreach (var edge in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return edge;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EntityEdge> GetParentsAsync(EntityId child, DateTimeOffset? asOf = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var t = asOf ?? DateTimeOffset.UtcNow;
        EntityEdge[] snapshot;
        lock (_lock)
        {
            snapshot = _edges
                .Where(e => e.Kind == EdgeKind.ChildOf && e.From == child && e.Validity.IsValidAt(t))
                .ToArray();
        }
        foreach (var edge in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return edge;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ClosureEntry> GetAncestorsAsync(EntityId descendant, DateTimeOffset? asOf = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var t = asOf ?? DateTimeOffset.UtcNow;
        ClosureEntry[] snapshot;
        lock (_lock)
        {
            snapshot = _closure
                .Where(c => c.Descendant == descendant && c.Depth > 0 && c.Validity.IsValidAt(t))
                .OrderBy(c => c.Depth)
                .ToArray();
        }
        foreach (var c in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return c;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ClosureEntry> GetDescendantsAsync(EntityId ancestor, DateTimeOffset? asOf = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var t = asOf ?? DateTimeOffset.UtcNow;
        ClosureEntry[] snapshot;
        lock (_lock)
        {
            snapshot = _closure
                .Where(c => c.Ancestor == ancestor && c.Depth > 0 && c.Validity.IsValidAt(t))
                .OrderBy(c => c.Depth)
                .ToArray();
        }
        foreach (var c in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return c;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public Task<TemporalSnapshot> GetSubtreeAsync(EntityId root, DateTimeOffset? asOf = null, CancellationToken ct = default)
    {
        var t = asOf ?? DateTimeOffset.UtcNow;
        ClosureEntry[] rows;
        lock (_lock)
        {
            rows = _closure
                .Where(c => c.Ancestor == root && c.Validity.IsValidAt(t))
                .OrderBy(c => c.Depth)
                .ToArray();
        }
        return Task.FromResult(new TemporalSnapshot(root, t, rows));
    }

    /// <summary>Internal helper for <c>HierarchyOperations</c> to enumerate edges by destination.</summary>
    internal IReadOnlyList<EntityEdge> GetOutgoingActiveChildEdges(EntityId parent, DateTimeOffset at)
    {
        lock (_lock)
        {
            return _edges
                .Where(e => e.Kind == EdgeKind.ChildOf && e.To == parent && e.Validity.IsValidAt(at))
                .ToList();
        }
    }
}
