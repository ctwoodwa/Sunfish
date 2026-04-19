using System.Collections.Concurrent;

namespace Sunfish.Foundation.LocalFirst;

/// <summary>
/// One pending outbound operation waiting to be synced. Kind is caller-defined
/// (e.g. <c>lease.created</c>); payload is format-agnostic bytes.
/// </summary>
public sealed record OfflineOperation
{
    /// <summary>Unique operation identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Caller-defined operation kind / topic.</summary>
    public required string Kind { get; init; }

    /// <summary>Opaque payload bytes.</summary>
    public required byte[] Payload { get; init; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Number of prior sync attempts for this operation.</summary>
    public int AttemptCount { get; init; }
}

/// <summary>
/// Pending-operation queue. Producers enqueue locally; the sync engine peeks
/// and acknowledges as operations are successfully delivered.
/// </summary>
public interface IOfflineQueue
{
    /// <summary>Enqueues an operation at the tail of the queue.</summary>
    ValueTask EnqueueAsync(OfflineOperation operation, CancellationToken cancellationToken = default);

    /// <summary>Returns up to <paramref name="max"/> pending operations, oldest first.</summary>
    ValueTask<IReadOnlyList<OfflineOperation>> PeekPendingAsync(int max = 100, CancellationToken cancellationToken = default);

    /// <summary>Acknowledges successful delivery and removes from the queue.</summary>
    ValueTask AcknowledgeAsync(Guid operationId, CancellationToken cancellationToken = default);

    /// <summary>Returns the count of pending operations.</summary>
    ValueTask<int> CountAsync(CancellationToken cancellationToken = default);
}

/// <summary>In-memory reference implementation of <see cref="IOfflineQueue"/>.</summary>
public sealed class InMemoryOfflineQueue : IOfflineQueue
{
    private readonly ConcurrentDictionary<Guid, OfflineOperation> _byId = new();
    private readonly List<Guid> _order = new();
    private readonly object _orderLock = new();

    /// <inheritdoc />
    public ValueTask EnqueueAsync(OfflineOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!_byId.TryAdd(operation.Id, operation))
        {
            throw new InvalidOperationException($"Operation '{operation.Id}' is already enqueued.");
        }

        lock (_orderLock)
        {
            _order.Add(operation.Id);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<OfflineOperation>> PeekPendingAsync(int max = 100, CancellationToken cancellationToken = default)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "max must be positive.");
        }

        OfflineOperation[] snapshot;
        lock (_orderLock)
        {
            snapshot = _order
                .Take(max)
                .Select(id => _byId[id])
                .ToArray();
        }

        return ValueTask.FromResult<IReadOnlyList<OfflineOperation>>(snapshot);
    }

    /// <inheritdoc />
    public ValueTask AcknowledgeAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        if (_byId.TryRemove(operationId, out _))
        {
            lock (_orderLock)
            {
                _order.Remove(operationId);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_byId.Count);
}
