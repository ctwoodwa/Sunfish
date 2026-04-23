using Sunfish.Kernel.Sync.Gossip;

namespace Sunfish.Kernel.Sync.Discovery;

/// <summary>
/// Bridge between <see cref="IPeerDiscovery"/> and <see cref="IGossipDaemon"/>.
/// Discovered peers are auto-<see cref="IGossipDaemon.AddPeer"/>ed; lost peers
/// are auto-<see cref="IGossipDaemon.RemovePeer"/>ed.
/// </summary>
public static class GossipDaemonDiscoveryExtensions
{
    /// <summary>
    /// Subscribe <paramref name="daemon"/> to <paramref name="discovery"/>'s
    /// <see cref="IPeerDiscovery.PeerDiscovered"/> and
    /// <see cref="IPeerDiscovery.PeerLost"/> events and seed it with every
    /// peer already in <see cref="IPeerDiscovery.KnownPeers"/>.
    /// </summary>
    /// <remarks>
    /// Disposing the returned handle unsubscribes both events. The daemon's
    /// peer table is <b>not</b> cleared on dispose — existing peers remain
    /// until the caller explicitly removes them. This matches the "discovery
    /// is advisory, gossip owns membership" boundary from paper §6.1.
    /// </remarks>
    public static IDisposable AttachDiscovery(this IGossipDaemon daemon, IPeerDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(daemon);
        ArgumentNullException.ThrowIfNull(discovery);
        return new DiscoverySubscription(daemon, discovery);
    }

    private sealed class DiscoverySubscription : IDisposable
    {
        private readonly IGossipDaemon _daemon;
        private readonly IPeerDiscovery _discovery;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _nodeIdToEndpoint =
            new(StringComparer.Ordinal);
        private bool _disposed;

        public DiscoverySubscription(IGossipDaemon daemon, IPeerDiscovery discovery)
        {
            _daemon = daemon;
            _discovery = discovery;

            _discovery.PeerDiscovered += OnPeerDiscovered;
            _discovery.PeerLost += OnPeerLost;

            // Seed — if discovery was already running, catch the daemon up.
            foreach (var peer in _discovery.KnownPeers)
            {
                _nodeIdToEndpoint[peer.NodeId] = peer.Endpoint;
                _daemon.AddPeer(peer.Endpoint, peer.PublicKey);
            }
        }

        private void OnPeerDiscovered(object? sender, PeerDiscoveredEventArgs e)
        {
            _nodeIdToEndpoint[e.Peer.NodeId] = e.Peer.Endpoint;
            _daemon.AddPeer(e.Peer.Endpoint, e.Peer.PublicKey);
        }

        private void OnPeerLost(object? sender, PeerLostEventArgs e)
        {
            // PeerLost fires after the discovery has already evicted the peer,
            // so we can't scan KnownPeers — use our own NodeId → endpoint map.
            if (_nodeIdToEndpoint.TryRemove(e.NodeId, out var endpoint))
            {
                _daemon.RemovePeer(endpoint);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _discovery.PeerDiscovered -= OnPeerDiscovered;
            _discovery.PeerLost -= OnPeerLost;
        }
    }
}
