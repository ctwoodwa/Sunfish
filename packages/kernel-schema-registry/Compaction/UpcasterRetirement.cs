using System.Collections.Concurrent;

namespace Sunfish.Kernel.SchemaRegistry.Compaction;

/// <summary>
/// In-memory <see cref="IUpcasterRetirement"/>. Backed by a concurrent hash-set keyed by
/// <c>(EventType, FromVersion, ToVersion)</c> plus a parallel list that preserves
/// retirement order for the <see cref="Retirements"/> snapshot.
/// </summary>
/// <remarks>
/// <para>
/// Retirement is idempotent: retiring the same edge twice is a no-op and does not add a
/// duplicate row to <see cref="Retirements"/>.
/// </para>
/// </remarks>
public sealed class UpcasterRetirement : IUpcasterRetirement
{
    private readonly ConcurrentDictionary<(string EventType, string FromVersion, string ToVersion), RetiredUpcaster> _byKey = new();
    private readonly object _orderGate = new();
    private readonly List<RetiredUpcaster> _order = new();
    private readonly TimeProvider _time;

    /// <summary>Create a retirement registry using <see cref="TimeProvider.System"/>.</summary>
    public UpcasterRetirement() : this(TimeProvider.System) { }

    /// <summary>Create a retirement registry using the supplied time provider.</summary>
    public UpcasterRetirement(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <inheritdoc />
    public void Retire(string eventType, string fromVersion, string toVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentException.ThrowIfNullOrEmpty(fromVersion);
        ArgumentException.ThrowIfNullOrEmpty(toVersion);

        var record = new RetiredUpcaster(eventType, fromVersion, toVersion, _time.GetUtcNow());

        if (_byKey.TryAdd((eventType, fromVersion, toVersion), record))
        {
            lock (_orderGate)
            {
                _order.Add(record);
            }
        }
    }

    /// <inheritdoc />
    public bool IsRetired(string eventType, string fromVersion, string toVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentException.ThrowIfNullOrEmpty(fromVersion);
        ArgumentException.ThrowIfNullOrEmpty(toVersion);

        return _byKey.ContainsKey((eventType, fromVersion, toVersion));
    }

    /// <inheritdoc />
    public IReadOnlyList<RetiredUpcaster> Retirements
    {
        get
        {
            lock (_orderGate)
            {
                // Immutable snapshot — callers must not observe subsequent mutations.
                return _order.ToArray();
            }
        }
    }
}
