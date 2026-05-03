using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Migration;

/// <summary>
/// Reference <see cref="ISequestrationStore"/> per ADR 0028-A5.4. Thread-
/// safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>; not durable
/// across process restarts (substrate Phase 1 — durable backends are a
/// follow-up workstream).
/// </summary>
public sealed class InMemorySequestrationStore : ISequestrationStore
{
    private readonly ConcurrentDictionary<(string NodeId, string RecordId), SequesteredRecord> _entries = new();

    /// <inheritdoc />
    public ValueTask RegisterAsync(SequesteredRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ct.ThrowIfCancellationRequested();
        _entries[(record.NodeId, record.RecordId)] = record;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SequesterAsync(string nodeId, string recordId, SequestrationFlagKind flag, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ArgumentException.ThrowIfNullOrEmpty(recordId);
        ct.ThrowIfCancellationRequested();
        var key = (nodeId, recordId);
        if (_entries.TryGetValue(key, out var existing))
        {
            _entries[key] = existing with { Flag = flag };
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ReleaseAsync(string nodeId, string recordId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ArgumentException.ThrowIfNullOrEmpty(recordId);
        ct.ThrowIfCancellationRequested();
        var key = (nodeId, recordId);
        if (_entries.TryGetValue(key, out var existing))
        {
            _entries[key] = existing with { Flag = null };
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SequesteredRecord>> GetByNodeAsync(string nodeId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ct.ThrowIfCancellationRequested();
        var results = _entries
            .Where(kv => kv.Key.NodeId == nodeId)
            .Select(kv => kv.Value)
            .ToList();
        return ValueTask.FromResult<IReadOnlyList<SequesteredRecord>>(results);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SequesteredRecord>> GetSequesteredAsync(string nodeId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ct.ThrowIfCancellationRequested();
        var results = _entries
            .Where(kv => kv.Key.NodeId == nodeId && kv.Value.Flag is not null)
            .Select(kv => kv.Value)
            .ToList();
        return ValueTask.FromResult<IReadOnlyList<SequesteredRecord>>(results);
    }
}
