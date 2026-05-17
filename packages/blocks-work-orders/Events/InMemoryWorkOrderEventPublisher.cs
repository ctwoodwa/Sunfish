using System.Collections.Concurrent;

namespace Sunfish.Blocks.WorkOrders.Events;

/// <summary>
/// In-memory <see cref="IWorkOrderEventPublisher"/> — accumulates
/// emitted events in publish order for test assertions + the
/// kitchen-sink demo to inspect. <see cref="DrainEvents"/> returns
/// + clears the accumulated batch atomically.
/// </summary>
public sealed class InMemoryWorkOrderEventPublisher : IWorkOrderEventPublisher
{
    private readonly ConcurrentQueue<object> _events = new();

    /// <summary>Captured payloads in publish order (read-only snapshot).</summary>
    public IReadOnlyList<object> Events => _events.ToArray();

    /// <summary>
    /// Drain the accumulated events into a list + clear the internal
    /// queue. Atomic-ish — concurrent publishes between the snapshot
    /// and the dequeue loop will be returned in subsequent calls.
    /// </summary>
    public IReadOnlyList<object> DrainEvents()
    {
        var drained = new List<object>();
        while (_events.TryDequeue(out var ev))
            drained.Add(ev);
        return drained;
    }

    /// <inheritdoc />
    public Task PublishWorkOrderCreatedAsync(WorkOrderCreatedEvent payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _events.Enqueue(payload);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PublishWorkOrderAssignedAsync(WorkOrderAssignedEvent payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _events.Enqueue(payload);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PublishWorkOrderCompletedAsync(WorkOrderCompletedEvent payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _events.Enqueue(payload);
        return Task.CompletedTask;
    }
}
