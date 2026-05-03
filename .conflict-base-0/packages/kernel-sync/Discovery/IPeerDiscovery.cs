namespace Sunfish.Kernel.Sync.Discovery;

/// <summary>
/// Paper §6.1 tier-1 peer-discovery abstraction. Implementations advertise
/// this node's presence and surface peers discovered on the local segment.
/// The gossip daemon subscribes to <see cref="PeerDiscovered"/> /
/// <see cref="PeerLost"/> via
/// <see cref="GossipDaemonDiscoveryExtensions.AttachDiscovery"/> and
/// auto-<c>AddPeer</c>s / <c>RemovePeer</c>s in response.
/// </summary>
/// <remarks>
/// Three implementations are planned:
/// <list type="bullet">
///   <item><see cref="MdnsPeerDiscovery"/> — tier-1, LAN-only, zero-config.</item>
///   <item>WireGuard mesh-VPN discovery — tier-2 (future wave).</item>
///   <item>Managed-relay discovery — tier-3 (Wave 4.2).</item>
/// </list>
/// <see cref="InMemoryPeerDiscovery"/> is provided as a test harness; it wires
/// multiple in-process instances through a shared broker.
/// </remarks>
public interface IPeerDiscovery : IAsyncDisposable
{
    /// <summary>Begin advertising this node and listening for peers.</summary>
    Task StartAsync(PeerAdvertisement self, CancellationToken ct);

    /// <summary>Stop advertising and listening.</summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>Currently-discovered peers (snapshot).</summary>
    IReadOnlyCollection<PeerAdvertisement> KnownPeers { get; }

    /// <summary>Raised when a new peer appears on the segment.</summary>
    event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;

    /// <summary>Raised when a previously-known peer drops off.</summary>
    event EventHandler<PeerLostEventArgs>? PeerLost;
}

/// <summary>
/// The TXT-record payload for a node advertisement. <see cref="Endpoint"/> is
/// the transport endpoint the gossip daemon will <c>AddPeer</c> on; the rest
/// are trust-and-filter hints surfaced from the discovery layer.
/// </summary>
public sealed record PeerAdvertisement(
    string NodeId,
    string Endpoint,
    byte[] PublicKey,
    string TeamId,
    string SchemaVersion,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>Fired by <see cref="IPeerDiscovery.PeerDiscovered"/>.</summary>
public sealed record PeerDiscoveredEventArgs(PeerAdvertisement Peer);

/// <summary>Fired by <see cref="IPeerDiscovery.PeerLost"/>.</summary>
public sealed record PeerLostEventArgs(string NodeId);
