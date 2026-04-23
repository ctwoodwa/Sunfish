using System.Runtime.CompilerServices;

namespace Sunfish.Kernel.Events;

/// <summary>
/// In-memory <see cref="IEventLog"/> for tests and ephemeral scenarios. Backed by a
/// <see cref="List{T}"/> of <see cref="LogEntry"/> plus a dictionary keyed by
/// <c>(AggregateId, EpochId, SchemaVersion)</c> for snapshots.
/// </summary>
/// <remarks>
/// <para>
/// Writes are serialized behind a single lock; reads copy the list under the lock and then iterate
/// without holding it, so readers never block writers for longer than a pointer snapshot.
/// </para>
/// <para>
/// This implementation is deliberately minimal — it exists so contract tests can run without
/// filesystem side-effects, and so higher-level components can substitute an in-memory log in
/// unit tests the same way <see cref="InMemoryEventBus"/> is used for the bus contract.
/// </para>
/// </remarks>
public sealed class InMemoryEventLog : IEventLog
{
    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = new();
    private readonly Dictionary<SnapshotKey, List<Snapshot>> _snapshots = new();

    /// <inheritdoc />
    public ulong CurrentSequence
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count == 0 ? 0UL : _entries[^1].Sequence;
            }
        }
    }

    /// <inheritdoc />
    public Task<ulong> AppendAsync(KernelEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var next = (ulong)_entries.Count + 1;
            _entries.Add(new LogEntry(next, evt));
            return Task.FromResult(next);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LogEntry> ReadAfterAsync(ulong afterSeq, [EnumeratorCancellation] CancellationToken ct)
    {
        LogEntry[] snapshot;
        lock (_gate)
        {
            snapshot = _entries.ToArray();
        }

        foreach (var entry in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.Sequence > afterSeq)
            {
                yield return entry;
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LogEntry> ReadRangeAsync(ulong fromSeq, ulong toSeqInclusive, [EnumeratorCancellation] CancellationToken ct)
    {
        LogEntry[] snapshot;
        lock (_gate)
        {
            snapshot = _entries.ToArray();
        }

        foreach (var entry in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.Sequence >= fromSeq && entry.Sequence <= toSeqInclusive)
            {
                yield return entry;
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task WriteSnapshotAsync(Snapshot snapshot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ct.ThrowIfCancellationRequested();

        var key = new SnapshotKey(snapshot.AggregateId, snapshot.EpochId, snapshot.SchemaVersion);
        lock (_gate)
        {
            if (!_snapshots.TryGetValue(key, out var list))
            {
                list = new List<Snapshot>();
                _snapshots[key] = list;
            }
            list.Add(snapshot);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Snapshot?> ReadLatestSnapshotAsync(string aggregateId, string epochId, string schemaVersion, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(aggregateId);
        ArgumentException.ThrowIfNullOrEmpty(epochId);
        ArgumentException.ThrowIfNullOrEmpty(schemaVersion);
        ct.ThrowIfCancellationRequested();

        var key = new SnapshotKey(aggregateId, epochId, schemaVersion);
        lock (_gate)
        {
            if (!_snapshots.TryGetValue(key, out var list) || list.Count == 0)
            {
                return Task.FromResult<Snapshot?>(null);
            }

            Snapshot? latest = null;
            foreach (var s in list)
            {
                if (latest is null || s.CreatedAt > latest.CreatedAt)
                {
                    latest = s;
                }
            }
            return Task.FromResult<Snapshot?>(latest);
        }
    }

    private readonly record struct SnapshotKey(string AggregateId, string EpochId, string SchemaVersion);
}
