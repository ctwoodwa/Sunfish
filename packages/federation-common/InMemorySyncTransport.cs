using System.Collections.Concurrent;

namespace Sunfish.Federation.Common;

/// <summary>
/// In-process <see cref="ISyncTransport"/> that routes envelopes by <see cref="PeerId"/> to registered
/// handlers. Useful for tests and dev-loop scenarios where federation peers share an address space.
/// </summary>
/// <remarks>
/// Thread-safe. Uses a <see cref="ConcurrentDictionary{TKey, TValue}"/> internally. Disposing the
/// <see cref="IDisposable"/> returned from <see cref="RegisterHandler"/> removes the handler from
/// the routing map.
/// </remarks>
public sealed class InMemorySyncTransport : ISyncTransport
{
    private readonly ConcurrentDictionary<PeerId, Func<SyncEnvelope, ValueTask<SyncEnvelope>>> _handlers = new();

    /// <inheritdoc />
    public ValueTask<SyncEnvelope> SendAsync(PeerDescriptor target, SyncEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(envelope);
        ct.ThrowIfCancellationRequested();

        if (!_handlers.TryGetValue(target.Id, out var handler))
            throw new InvalidOperationException($"No peer registered for {target.Id}.");

        return handler(envelope);
    }

    /// <inheritdoc />
    public IDisposable RegisterHandler(PeerId local, Func<SyncEnvelope, ValueTask<SyncEnvelope>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!_handlers.TryAdd(local, handler))
            throw new InvalidOperationException($"Peer {local} already registered.");

        return new Unregister(() => _handlers.TryRemove(local, out _));
    }

    private sealed class Unregister(Action onDispose) : IDisposable
    {
        private readonly Action _onDispose = onDispose;
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _onDispose();
        }
    }
}
