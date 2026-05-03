using System.Collections.Concurrent;

namespace Sunfish.Kernel.SchemaRegistry.Lenses;

/// <summary>
/// Directed graph of <see cref="ISchemaLens"/> edges per event type. Supports
/// shortest-path traversal in either direction (forward via
/// <see cref="ISchemaLens.ForwardTransform"/>, backward via
/// <see cref="ISchemaLens.BackwardTransform"/>).
/// </summary>
/// <remarks>
/// <para>
/// Paper §7.3: <i>"Lenses form a version graph; migrations between distant versions
/// traverse the shortest path."</i> This implementation uses breadth-first search —
/// all edges have unit cost in the paper's model, so BFS yields the shortest path
/// without the overhead of a priority queue. Dijkstra would be warranted only if a
/// future extension introduced weighted edges (e.g. preferring a single direct lens
/// over a two-hop chain by applying tie-breakers). In the current contract, "shortest
/// path" means "fewest hops", which is exactly what BFS computes.
/// </para>
/// <para>
/// <b>Cycle safety:</b> version graphs can contain cycles if a lens author registers
/// round-trip lenses (A→B forward, B→A as a separate edge). The BFS visited-set
/// prevents infinite loops; each version node is expanded at most once per search.
/// </para>
/// <para>
/// <b>Thread safety:</b> registration and traversal are concurrent-safe — backing
/// stores are <see cref="ConcurrentDictionary{TKey,TValue}"/>s. Individual lens
/// transforms are called outside any lock; lens authors must keep transforms pure
/// per the <see cref="ISchemaLens"/> contract.
/// </para>
/// </remarks>
public sealed class LensGraph
{
    // eventType -> fromVersion -> list of outgoing lenses (for forward edges)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<ISchemaLens>>> _forward = new();

    // eventType -> toVersion -> list of incoming lenses (for backward edges)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<ISchemaLens>>> _backward = new();

    /// <summary>Register a lens into the graph. Repeated registration of the same lens instance is idempotent at the edge level (the same edge is added once per call; the graph does not de-duplicate).</summary>
    public void AddLens(ISchemaLens lens)
    {
        ArgumentNullException.ThrowIfNull(lens);

        var byFrom = _forward.GetOrAdd(lens.EventType, _ => new());
        var fwdList = byFrom.GetOrAdd(lens.FromVersion, _ => new());
        lock (fwdList) { fwdList.Add(lens); }

        var byTo = _backward.GetOrAdd(lens.EventType, _ => new());
        var bwdList = byTo.GetOrAdd(lens.ToVersion, _ => new());
        lock (bwdList) { bwdList.Add(lens); }
    }

    /// <summary>
    /// Transform <paramref name="evt"/> from <paramref name="fromVersion"/> to
    /// <paramref name="toVersion"/> by traversing the shortest path of lenses.
    /// Returns <see langword="null"/> when no path exists.
    /// </summary>
    /// <remarks>
    /// When <paramref name="fromVersion"/> equals <paramref name="toVersion"/> the
    /// event is returned unchanged (zero-hop path).
    /// </remarks>
    public object? Transform(string eventType, object evt, string fromVersion, string toVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentException.ThrowIfNullOrEmpty(fromVersion);
        ArgumentException.ThrowIfNullOrEmpty(toVersion);

        if (fromVersion == toVersion)
        {
            return evt;
        }

        var path = FindPath(eventType, fromVersion, toVersion);
        if (path is null)
        {
            return null;
        }

        var current = evt;
        foreach (var (lens, forward) in path)
        {
            current = forward ? lens.ForwardTransform(current) : lens.BackwardTransform(current);
        }
        return current;
    }

    /// <summary>Returns <see langword="true"/> when a lens path exists between the two versions.</summary>
    public bool HasPath(string eventType, string fromVersion, string toVersion)
    {
        if (fromVersion == toVersion) return true;
        return FindPath(eventType, fromVersion, toVersion) is not null;
    }

    /// <summary>
    /// The ordered list of version identifiers visited on the shortest path from
    /// <paramref name="fromVersion"/> to <paramref name="toVersion"/> (inclusive of
    /// both ends). Empty when no path exists or for zero-hop identity paths.
    /// </summary>
    public IReadOnlyList<string> ShortestPath(string eventType, string fromVersion, string toVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentException.ThrowIfNullOrEmpty(fromVersion);
        ArgumentException.ThrowIfNullOrEmpty(toVersion);

        if (fromVersion == toVersion)
        {
            return new[] { fromVersion };
        }

        var path = FindPath(eventType, fromVersion, toVersion);
        if (path is null) return Array.Empty<string>();

        var versions = new List<string>(path.Count + 1) { fromVersion };
        foreach (var (lens, forward) in path)
        {
            versions.Add(forward ? lens.ToVersion : lens.FromVersion);
        }
        return versions;
    }

    /// <summary>
    /// BFS across forward + backward adjacency. Returns the ordered list of lens
    /// edges to apply (paired with a direction flag: <see langword="true"/> = forward,
    /// <see langword="false"/> = backward). Returns <see langword="null"/> when no path
    /// exists.
    /// </summary>
    private IReadOnlyList<(ISchemaLens Lens, bool Forward)>? FindPath(string eventType, string fromVersion, string toVersion)
    {
        // Snapshot the per-event-type adjacency to avoid enumerating under concurrent writes.
        _forward.TryGetValue(eventType, out var fwdByFrom);
        _backward.TryGetValue(eventType, out var bwdByTo);

        if (fwdByFrom is null && bwdByTo is null)
        {
            return null;
        }

        // BFS: queue holds (currentVersion, pathSoFar). Visited tracks versions
        // already dequeued to cut cycles.
        var visited = new HashSet<string> { fromVersion };
        var queue = new Queue<(string Version, List<(ISchemaLens, bool)> Path)>();
        queue.Enqueue((fromVersion, new List<(ISchemaLens, bool)>()));

        while (queue.Count > 0)
        {
            var (version, path) = queue.Dequeue();

            // Forward edges: version --lens--> lens.ToVersion
            if (fwdByFrom is not null && fwdByFrom.TryGetValue(version, out var outgoing))
            {
                List<ISchemaLens> snapshot;
                lock (outgoing) { snapshot = new List<ISchemaLens>(outgoing); }
                foreach (var lens in snapshot)
                {
                    var next = lens.ToVersion;
                    if (next == toVersion)
                    {
                        var final = new List<(ISchemaLens, bool)>(path) { (lens, true) };
                        return final;
                    }
                    if (visited.Add(next))
                    {
                        var extended = new List<(ISchemaLens, bool)>(path) { (lens, true) };
                        queue.Enqueue((next, extended));
                    }
                }
            }

            // Backward edges: version --lens(inverse)--> lens.FromVersion
            if (bwdByTo is not null && bwdByTo.TryGetValue(version, out var incoming))
            {
                List<ISchemaLens> snapshot;
                lock (incoming) { snapshot = new List<ISchemaLens>(incoming); }
                foreach (var lens in snapshot)
                {
                    var next = lens.FromVersion;
                    if (next == toVersion)
                    {
                        var final = new List<(ISchemaLens, bool)>(path) { (lens, false) };
                        return final;
                    }
                    if (visited.Add(next))
                    {
                        var extended = new List<(ISchemaLens, bool)>(path) { (lens, false) };
                        queue.Enqueue((next, extended));
                    }
                }
            }
        }

        return null;
    }
}
