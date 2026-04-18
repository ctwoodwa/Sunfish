using System.Collections.Concurrent;

namespace Sunfish.Federation.Common;

/// <summary>
/// Registry of known federation peers for the local node. Higher-level sync code queries this
/// registry to discover routing targets; operators populate it from configuration or a bootstrap
/// discovery mechanism.
/// </summary>
public interface IPeerRegistry
{
    /// <summary>Returns all currently-registered peer descriptors.</summary>
    ValueTask<IReadOnlyList<PeerDescriptor>> ListAsync(CancellationToken ct);

    /// <summary>Returns the descriptor for <paramref name="id"/>, or <c>null</c> if not registered.</summary>
    ValueTask<PeerDescriptor?> FindAsync(PeerId id, CancellationToken ct);

    /// <summary>
    /// Adds or updates a peer descriptor. Implementations must be safe to call concurrently.
    /// </summary>
    ValueTask AddAsync(PeerDescriptor peer, CancellationToken ct);

    /// <summary>Removes the peer with the given id, if present.</summary>
    ValueTask RemoveAsync(PeerId id, CancellationToken ct);
}

/// <summary>
/// Volatile in-process <see cref="IPeerRegistry"/>. Suitable for tests and single-node dev. Production
/// deployments typically back the registry with durable configuration; swap via DI in Task D-3+.
/// </summary>
public sealed class InMemoryPeerRegistry : IPeerRegistry
{
    private readonly ConcurrentDictionary<PeerId, PeerDescriptor> _peers = new();

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<PeerDescriptor>> ListAsync(CancellationToken ct)
        => ValueTask.FromResult<IReadOnlyList<PeerDescriptor>>(_peers.Values.ToList());

    /// <inheritdoc />
    public ValueTask<PeerDescriptor?> FindAsync(PeerId id, CancellationToken ct)
        => ValueTask.FromResult(_peers.TryGetValue(id, out var peer) ? peer : null);

    /// <inheritdoc />
    public ValueTask AddAsync(PeerDescriptor peer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(peer);
        _peers[peer.Id] = peer;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(PeerId id, CancellationToken ct)
    {
        _peers.TryRemove(id, out _);
        return ValueTask.CompletedTask;
    }
}
