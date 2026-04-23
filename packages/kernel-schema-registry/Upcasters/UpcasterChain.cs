using System.Collections.Concurrent;

using Sunfish.Kernel.SchemaRegistry.Compaction;

namespace Sunfish.Kernel.SchemaRegistry.Upcasters;

/// <summary>
/// A chain of <see cref="IUpcaster"/> instances keyed by event type. Applying the chain
/// walks adjacent <c>FromVersion</c> / <c>ToVersion</c> edges from the caller's starting
/// version toward the requested target.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="Lenses.LensGraph"/>, the upcaster chain is strictly forward-directed —
/// upcasters have no backward transform. The chain is a simple directed walk, not a
/// general graph; when multiple upcasters share a <c>FromVersion</c> the first matching
/// <c>ToVersion</c> wins (per-event-type adjacency is built as a dictionary keyed by
/// <c>(EventType, FromVersion)</c>).
/// </para>
/// <para>
/// Paper §7.2 treats upcasters as a short- to medium-term tool and mandates periodic
/// stream compaction. After a compaction run, obsolete upcasters are "retired" via the
/// optional <see cref="IUpcasterRetirement"/> registry — retired edges remain physically
/// registered (so the record is preserved for audit) but <see cref="ApplyChain"/> will
/// refuse to traverse them, effectively removing them from the hot read path. This is
/// distinct from deletion: a retired upcaster can still be inspected in
/// <see cref="IUpcasterRetirement.Retirements"/>.
/// </para>
/// </remarks>
public sealed class UpcasterChain
{
    // (eventType, fromVersion) -> upcaster producing the next version
    private readonly ConcurrentDictionary<(string EventType, string FromVersion), IUpcaster> _edges = new();
    private readonly IUpcasterRetirement? _retirement;

    /// <summary>Create an upcaster chain with no retirement registry wired in.</summary>
    public UpcasterChain() : this(retirement: null) { }

    /// <summary>
    /// Create an upcaster chain with an optional retirement registry. When a retirement
    /// registry is supplied, <see cref="ApplyChain"/> skips any edge whose
    /// <c>(EventType, FromVersion, ToVersion)</c> triple has been marked retired.
    /// </summary>
    public UpcasterChain(IUpcasterRetirement? retirement)
    {
        _retirement = retirement;
    }

    /// <summary>Register an upcaster into the chain. Re-registering the same edge overwrites the previous entry.</summary>
    public void AddUpcaster(IUpcaster upcaster)
    {
        ArgumentNullException.ThrowIfNull(upcaster);
        _edges[(upcaster.EventType, upcaster.FromVersion)] = upcaster;
    }

    /// <summary>
    /// Apply upcasters in sequence from <paramref name="fromVersion"/> to
    /// <paramref name="toVersion"/>. Returns the transformed event, or
    /// <see langword="null"/> when no chain of edges reaches the target.
    /// </summary>
    /// <remarks>
    /// Identity transforms (<paramref name="fromVersion"/> == <paramref name="toVersion"/>)
    /// return <paramref name="evt"/> unchanged. Cycles in the chain are detected by a
    /// visited set; the method returns <see langword="null"/> if a cycle is reached
    /// without finding the target. Edges marked retired via <see cref="IUpcasterRetirement"/>
    /// are treated as absent.
    /// </remarks>
    public object? ApplyChain(string eventType, object evt, string fromVersion, string toVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentException.ThrowIfNullOrEmpty(fromVersion);
        ArgumentException.ThrowIfNullOrEmpty(toVersion);

        if (fromVersion == toVersion)
        {
            return evt;
        }

        var visited = new HashSet<string> { fromVersion };
        var current = evt;
        var currentVersion = fromVersion;

        while (currentVersion != toVersion)
        {
            if (!_edges.TryGetValue((eventType, currentVersion), out var upcaster))
            {
                return null;
            }

            if (_retirement is not null && _retirement.IsRetired(upcaster.EventType, upcaster.FromVersion, upcaster.ToVersion))
            {
                // Retired edges are treated as absent on the hot read path.
                return null;
            }

            current = upcaster.Upcast(current);
            currentVersion = upcaster.ToVersion;

            if (!visited.Add(currentVersion))
            {
                // Cycle detected — bail out rather than loop forever.
                return null;
            }
        }

        return current;
    }
}
