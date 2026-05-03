using Sunfish.Kernel.Sync.Discovery;

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Phase 1 G4 — coverage for <see cref="ManagedRelayPeerDiscovery"/> (paper
/// §17.2 tier-3, ADR 0031). Exercises the start/stop lifecycle, the empty-
/// config no-op path, the single-relay PeerDiscovered/PeerLost events, and
/// the coexistence pattern alongside <see cref="InMemoryPeerDiscovery"/>
/// that the <see cref="GossipDaemon"/> sees on Anchor.
/// </summary>
public sealed class ManagedRelayPeerDiscoveryTests
{
    private static readonly byte[] FakeRelayPubkey = new byte[32]
    {
        0x42, 0x42, 0x42, 0x42, 0x42, 0x42, 0x42, 0x42,
        0x42, 0x42, 0x42, 0x42, 0x42, 0x42, 0x42, 0x42,
        0x42, 0x42, 0x42, 0x42, 0x42, 0x42, 0x42, 0x42,
        0x42, 0x42, 0x42, 0x42, 0x42, 0x42, 0x42, 0x42,
    };

    private static PeerAdvertisement Self(string nodeId = "node-self", string teamId = "team-alpha")
    {
        return new PeerAdvertisement(
            NodeId: nodeId,
            Endpoint: "in-mem://self",
            PublicKey: new byte[32],
            TeamId: teamId,
            SchemaVersion: "1.0",
            Metadata: new Dictionary<string, string>());
    }

    [Fact]
    public async Task StartAsync_emits_one_PeerDiscovered_for_configured_relay()
    {
        var opts = new ManagedRelayPeerDiscoveryOptions
        {
            RelayUrl = "wss://relay.example.com/sync",
            RelayNodeId = "abcdef0123456789abcdef0123456789",
            RelayPublicKey = FakeRelayPubkey,
            RelaySchemaVersion = "1.0",
        };
        await using var discovery = new ManagedRelayPeerDiscovery(opts);

        var discoveredPeers = new List<PeerAdvertisement>();
        discovery.PeerDiscovered += (_, e) => discoveredPeers.Add(e.Peer);

        await discovery.StartAsync(Self(), CancellationToken.None);

        Assert.Single(discoveredPeers);
        Assert.Equal("wss://relay.example.com/sync", discoveredPeers[0].Endpoint);
        Assert.Equal("abcdef0123456789abcdef0123456789", discoveredPeers[0].NodeId);
        Assert.Equal("team-alpha", discoveredPeers[0].TeamId);
        Assert.Equal("3-managed-relay", discoveredPeers[0].Metadata["tier"]);
    }

    [Fact]
    public async Task StartAsync_with_empty_RelayUrl_is_a_noop()
    {
        var opts = new ManagedRelayPeerDiscoveryOptions { RelayUrl = string.Empty };
        await using var discovery = new ManagedRelayPeerDiscovery(opts);

        var discoveredPeers = new List<PeerAdvertisement>();
        discovery.PeerDiscovered += (_, e) => discoveredPeers.Add(e.Peer);

        await discovery.StartAsync(Self(), CancellationToken.None);

        Assert.Empty(discoveredPeers);
        Assert.Empty(discovery.KnownPeers);
    }

    [Fact]
    public async Task StopAsync_emits_PeerLost_and_clears_KnownPeers()
    {
        var opts = new ManagedRelayPeerDiscoveryOptions
        {
            RelayUrl = "wss://relay.example.com/sync",
            RelayNodeId = "abcdef0123456789abcdef0123456789",
            RelayPublicKey = FakeRelayPubkey,
        };
        await using var discovery = new ManagedRelayPeerDiscovery(opts);

        var lostNodeIds = new List<string>();
        discovery.PeerLost += (_, e) => lostNodeIds.Add(e.NodeId);

        await discovery.StartAsync(Self(), CancellationToken.None);
        Assert.Single(discovery.KnownPeers);

        await discovery.StopAsync(CancellationToken.None);

        Assert.Single(lostNodeIds);
        Assert.Equal("abcdef0123456789abcdef0123456789", lostNodeIds[0]);
        Assert.Empty(discovery.KnownPeers);
    }

    [Fact]
    public async Task StartAsync_is_idempotent()
    {
        var opts = new ManagedRelayPeerDiscoveryOptions
        {
            RelayUrl = "wss://relay.example.com/sync",
            RelayNodeId = "abcdef0123456789abcdef0123456789",
            RelayPublicKey = FakeRelayPubkey,
        };
        await using var discovery = new ManagedRelayPeerDiscovery(opts);

        var discoveredCount = 0;
        discovery.PeerDiscovered += (_, _) => discoveredCount++;

        await discovery.StartAsync(Self(), CancellationToken.None);
        await discovery.StartAsync(Self(), CancellationToken.None);

        Assert.Equal(1, discoveredCount);
    }

    [Fact]
    public async Task DisposeAsync_emits_PeerLost_when_running()
    {
        var opts = new ManagedRelayPeerDiscoveryOptions
        {
            RelayUrl = "wss://relay.example.com/sync",
            RelayNodeId = "abcdef0123456789abcdef0123456789",
            RelayPublicKey = FakeRelayPubkey,
        };
        var discovery = new ManagedRelayPeerDiscovery(opts);

        var lostNodeIds = new List<string>();
        discovery.PeerLost += (_, e) => lostNodeIds.Add(e.NodeId);

        await discovery.StartAsync(Self(), CancellationToken.None);
        await discovery.DisposeAsync();

        Assert.Single(lostNodeIds);
    }

    [Fact]
    public async Task Relay_PeerAdvertisement_adopts_self_TeamId()
    {
        // Even though the Bridge relay is shared infrastructure, its
        // advertisement adopts the local team id so any team-aware filter
        // downstream (the gossip daemon's AttachDiscovery glue, future
        // multi-tenant relay multiplexing) treats the relay as same-team.
        var opts = new ManagedRelayPeerDiscoveryOptions
        {
            RelayUrl = "wss://relay.example.com/sync",
            RelayNodeId = "abcdef0123456789abcdef0123456789",
            RelayPublicKey = FakeRelayPubkey,
        };
        await using var discovery = new ManagedRelayPeerDiscovery(opts);

        await discovery.StartAsync(Self(teamId: "team-bravo"), CancellationToken.None);

        var ad = Assert.Single(discovery.KnownPeers);
        Assert.Equal("team-bravo", ad.TeamId);
    }

    [Fact]
    public async Task Coexists_with_InMemoryPeerDiscovery_via_dual_AttachDiscovery_pattern()
    {
        // Reproduces the Anchor composition pattern: a daemon attaches both a
        // LAN-discovery source (here InMemoryPeerDiscovery — proxy for mDNS)
        // and a managed-relay source. Both must surface their peers; neither
        // suppresses the other.
        var broker = new InMemoryPeerDiscoveryBroker();
        await using var lanDiscovery = new InMemoryPeerDiscovery(
            broker, new PeerDiscoveryOptions { FilterByTeamId = true });
        await using var relayDiscovery = new ManagedRelayPeerDiscovery(
            new ManagedRelayPeerDiscoveryOptions
            {
                RelayUrl = "wss://relay.example.com/sync",
                RelayNodeId = "abcdef0123456789abcdef0123456789",
                RelayPublicKey = FakeRelayPubkey,
            });

        // A peer ad already present on the LAN broker before our discovery starts
        // — InMemoryPeerDiscovery seeds existing peers in its StartAsync.
        await using var peerOnLan = new InMemoryPeerDiscovery(
            broker, new PeerDiscoveryOptions { FilterByTeamId = true });
        await peerOnLan.StartAsync(
            new PeerAdvertisement(
                NodeId: "lan-peer-1",
                Endpoint: "in-mem://lan-peer-1",
                PublicKey: new byte[32],
                TeamId: "team-alpha",
                SchemaVersion: "1.0",
                Metadata: new Dictionary<string, string>()),
            CancellationToken.None);

        var combined = new List<string>();
        lanDiscovery.PeerDiscovered += (_, e) => combined.Add($"lan:{e.Peer.NodeId}");
        relayDiscovery.PeerDiscovered += (_, e) => combined.Add($"relay:{e.Peer.NodeId}");

        var self = Self("anchor-self", "team-alpha");
        await lanDiscovery.StartAsync(self, CancellationToken.None);
        await relayDiscovery.StartAsync(self, CancellationToken.None);

        Assert.Contains("lan:lan-peer-1", combined);
        Assert.Contains("relay:abcdef0123456789abcdef0123456789", combined);
    }
}
