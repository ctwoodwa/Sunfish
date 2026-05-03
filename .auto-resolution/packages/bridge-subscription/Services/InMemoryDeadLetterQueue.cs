using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// In-memory <see cref="IDeadLetterQueue"/>. Substrate Phase 1 — durable
/// backends are a follow-up workstream.
/// </summary>
public sealed class InMemoryDeadLetterQueue : IDeadLetterQueue
{
    private readonly ConcurrentBag<DeadLetterEntry> _entries = new();
    private readonly TimeProvider _time;

    public InMemoryDeadLetterQueue(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ValueTask EnqueueAsync(BridgeSubscriptionEvent evt, string reason, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentException.ThrowIfNullOrEmpty(reason);
        ct.ThrowIfCancellationRequested();
        _entries.Add(new DeadLetterEntry(evt, reason, _time.GetUtcNow()));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<DeadLetterEntry>> GetByTenantAsync(string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        ct.ThrowIfCancellationRequested();
        var results = _entries.Where(e => e.Event.TenantId == tenantId).ToList();
        return ValueTask.FromResult<IReadOnlyList<DeadLetterEntry>>(results);
    }
}
