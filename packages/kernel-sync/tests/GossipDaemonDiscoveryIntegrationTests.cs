using Microsoft.Extensions.Options;

using Sunfish.Kernel.Sync.Discovery;

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Integration coverage for the glue between
/// <see cref="IPeerDiscovery"/> and <see cref="IGossipDaemon"/>
/// (<see cref="GossipDaemonDiscoveryExtensions.AttachDiscovery"/>).
/// </summary>
public class GossipDaemonDiscoveryIntegrationTests : IAsyncLifetime
{
    private readonly List<IAsyncDisposable> _cleanup = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var d in _cleanup)
        {
            try { await d.DisposeAsync(); } catch { /* best-effort */ }
        }
    }

    private GossipDaemon BuildDaemon()
    {
        var transport = new InMemorySyncDaemonTransport();
        _cleanup.Add(transport);

        var opts = Options.Create(new GossipDaemonOptions
        {
            RoundIntervalSeconds = 30,   // we are not exercising the round loop
            PeerPickCount = 1,
            ConnectTimeoutSeconds = 2,
            DeadPeerBackoffSeconds = 60,
        });
        var signer = TestIdentityFactory.NewSigner();
        var identityProvider = new InMemoryNodeIdentityProvider(
            TestIdentityFactory.NewNodeIdentity(signer));
        var daemon = new GossipDaemon(
            transport, new VectorClock(), opts, identityProvider, signer);
        _cleanup.Add(daemon);
        return daemon;
    }

    private InMemoryPeerDiscovery BuildDiscovery(InMemoryPeerDiscoveryBroker broker)
    {
        var d = new InMemoryPeerDiscovery(
            broker,
            new PeerDiscoveryOptions { FilterByTeamId = true });
        _cleanup.Add(d);
        return d;
    }

    private static PeerAdvertisement Ad(string nodeId, string endpoint, string teamId = "team-alpha")
    {
        return new PeerAdvertisement(
            NodeId: nodeId,
            Endpoint: endpoint,
            PublicKey: new byte[32],
            TeamId: teamId,
            SchemaVersion: "1.0",
            Metadata: new Dictionary<string, string>());
    }

    [Fact]
    public async Task PeerDiscovered_Auto_AddPeers_On_Daemon()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var discovery = BuildDiscovery(broker);
        var daemon = BuildDaemon();

        using var _sub = daemon.AttachDiscovery(discovery);

        await discovery.StartAsync(Ad("node-self", "in-mem://self"), CancellationToken.None);

        // Peer 2 joins via a second discovery instance on the same broker.
        var peer = BuildDiscovery(broker);
        await peer.StartAsync(Ad("node-peer", "in-mem://peer"), CancellationToken.None);

        // In-memory broker fires synchronously on the announce path, so by the
        // time StartAsync returns the daemon has seen AddPeer.
        Assert.Single(daemon.KnownPeers);
        Assert.Equal("in-mem://peer", daemon.KnownPeers.First().Endpoint);
    }

    [Fact]
    public async Task PeerLost_Auto_RemovePeers_On_Daemon()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var discovery = BuildDiscovery(broker);
        var daemon = BuildDaemon();

        using var _sub = daemon.AttachDiscovery(discovery);

        await discovery.StartAsync(Ad("node-self", "in-mem://self"), CancellationToken.None);

        var peer = BuildDiscovery(broker);
        await peer.StartAsync(Ad("node-peer", "in-mem://peer"), CancellationToken.None);
        Assert.Single(daemon.KnownPeers);

        await peer.StopAsync(CancellationToken.None);

        Assert.Empty(daemon.KnownPeers);
    }

    [Fact]
    public async Task Detach_Stops_Further_Updates()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var discovery = BuildDiscovery(broker);
        var daemon = BuildDaemon();

        var sub = daemon.AttachDiscovery(discovery);

        await discovery.StartAsync(Ad("node-self", "in-mem://self"), CancellationToken.None);

        // Detach — subsequent peer joins should NOT reach the daemon.
        sub.Dispose();

        var peer = BuildDiscovery(broker);
        await peer.StartAsync(Ad("node-peer", "in-mem://peer"), CancellationToken.None);

        Assert.Empty(daemon.KnownPeers);
    }

    [Fact]
    public async Task Seed_Catches_Daemon_Up_To_Existing_Peers()
    {
        // Discovery already running with a peer when AttachDiscovery is called
        // — the daemon should receive the existing peer set as its seed.
        var broker = new InMemoryPeerDiscoveryBroker();
        var discovery = BuildDiscovery(broker);
        var daemon = BuildDaemon();

        await discovery.StartAsync(Ad("node-self", "in-mem://self"), CancellationToken.None);

        var peer = BuildDiscovery(broker);
        await peer.StartAsync(Ad("node-peer", "in-mem://peer"), CancellationToken.None);
        Assert.Single(discovery.KnownPeers);

        using var _sub = daemon.AttachDiscovery(discovery);

        Assert.Single(daemon.KnownPeers);
        Assert.Equal("in-mem://peer", daemon.KnownPeers.First().Endpoint);
    }
}
