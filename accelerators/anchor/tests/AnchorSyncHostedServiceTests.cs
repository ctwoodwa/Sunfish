using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Sunfish.Anchor.Services;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Sync.Discovery;
using Sunfish.Kernel.Sync.Gossip;
using Sunfish.Kernel.Sync.Identity;
using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// Phase 1 G1 — Anchor sync hosted service wiring. Verifies that
/// <see cref="AnchorSyncHostedService"/> attaches the configured
/// <see cref="IPeerDiscovery"/> source to the gossip daemon and starts the
/// round scheduler on host startup, then stops the scheduler and unsubscribes
/// the discovery source on host shutdown.
/// </summary>
/// <remarks>
/// Builds a real <see cref="GossipDaemon"/> wired through
/// <see cref="InMemorySyncDaemonTransport"/> + <see cref="InMemoryPeerDiscovery"/>
/// — the same pattern that <c>packages/kernel-sync/tests</c> uses
/// (see <c>GossipDaemonDiscoveryIntegrationTests.BuildDaemon</c>). The Anchor
/// test project compiles <c>AnchorSyncHostedService.cs</c> directly via the
/// source-link pattern so we avoid the MAUI workload requirement on CI.
/// </remarks>
public sealed class AnchorSyncHostedServiceTests : IAsyncLifetime
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

    [Fact]
    public async Task StartAsync_starts_the_gossip_daemon()
    {
        var daemon = BuildDaemon();
        var discovery = BuildDiscovery();

        var svc = new AnchorSyncHostedService(daemon, discovery, NullLogger<AnchorSyncHostedService>.Instance);

        Assert.False(daemon.IsRunning);

        await svc.StartAsync(CancellationToken.None);

        Assert.True(daemon.IsRunning);
    }

    [Fact]
    public async Task StopAsync_stops_the_gossip_daemon()
    {
        var daemon = BuildDaemon();
        var discovery = BuildDiscovery();

        var svc = new AnchorSyncHostedService(daemon, discovery, NullLogger<AnchorSyncHostedService>.Instance);

        await svc.StartAsync(CancellationToken.None);
        Assert.True(daemon.IsRunning);

        await svc.StopAsync(CancellationToken.None);

        Assert.False(daemon.IsRunning);
    }

    [Fact]
    public async Task StartAsync_seeds_daemon_with_already_discovered_peers()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var daemon = BuildDaemon();
        var ourDiscovery = BuildDiscovery(broker);
        var peerDiscovery = BuildDiscovery(broker);

        // Peer is already advertising before our hosted service starts.
        await peerDiscovery.StartAsync(
            Ad("node-peer", "in-mem://peer", teamId: "team-alpha"),
            CancellationToken.None);

        // Our discovery source observes the peer before AnchorSyncHostedService
        // attaches it — exercises the AttachDiscovery seeding path.
        await ourDiscovery.StartAsync(
            Ad("node-self", "in-mem://self", teamId: "team-alpha"),
            CancellationToken.None);

        // Wait for the in-memory broker to deliver the discovery event.
        for (var i = 0; i < 50 && ourDiscovery.KnownPeers.Count == 0; i++)
        {
            await Task.Delay(20);
        }

        Assert.NotEmpty(ourDiscovery.KnownPeers);

        var svc = new AnchorSyncHostedService(daemon, ourDiscovery, NullLogger<AnchorSyncHostedService>.Instance);
        await svc.StartAsync(CancellationToken.None);

        // The seed-on-attach path adds known peers to the daemon's membership.
        Assert.Contains(daemon.KnownPeers, p => p.Endpoint == "in-mem://peer");
    }

    [Fact]
    public async Task StopAsync_disposes_discovery_subscription_so_later_PeerDiscovered_does_not_reach_daemon()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var daemon = BuildDaemon();
        var ourDiscovery = BuildDiscovery(broker);

        await ourDiscovery.StartAsync(
            Ad("node-self", "in-mem://self", teamId: "team-alpha"),
            CancellationToken.None);

        var svc = new AnchorSyncHostedService(daemon, ourDiscovery, NullLogger<AnchorSyncHostedService>.Instance);
        await svc.StartAsync(CancellationToken.None);

        // Stop the hosted service. Discovery subscription must be disposed —
        // any subsequent PeerDiscovered event from the broker should NOT add
        // a peer to the daemon (paper §6.1: "discovery is advisory").
        await svc.StopAsync(CancellationToken.None);
        var peerCountAfterStop = daemon.KnownPeers.Count;

        // New peer joins after the hosted service has stopped.
        var latePeerDiscovery = BuildDiscovery(broker);
        await latePeerDiscovery.StartAsync(
            Ad("node-late", "in-mem://late", teamId: "team-alpha"),
            CancellationToken.None);

        // Give the broker a moment to deliver — but the subscription is gone,
        // so the daemon's peer set must not grow.
        await Task.Delay(100);

        Assert.Equal(peerCountAfterStop, daemon.KnownPeers.Count);
        Assert.DoesNotContain(daemon.KnownPeers, p => p.Endpoint == "in-mem://late");
    }

    private GossipDaemon BuildDaemon()
    {
        var transport = new InMemorySyncDaemonTransport();
        _cleanup.Add(transport);

        var opts = Options.Create(new GossipDaemonOptions
        {
            // We're not exercising the round loop in these tests; a long
            // interval keeps the daemon's scheduler from doing real work.
            RoundIntervalSeconds = 30,
            PeerPickCount = 1,
            ConnectTimeoutSeconds = 2,
            DeadPeerBackoffSeconds = 60,
        });

        var signer = new Ed25519Signer();
        var (publicKey, privateKey) = signer.GenerateKeyPair();
        var nodeIdBytes = new byte[16];
        Buffer.BlockCopy(publicKey, 0, nodeIdBytes, 0, 16);
        var nodeIdHex = Convert.ToHexString(nodeIdBytes).ToLowerInvariant();
        var identityProvider = new InMemoryNodeIdentityProvider(
            new NodeIdentity(nodeIdHex, publicKey, privateKey));

        var daemon = new GossipDaemon(
            transport,
            new VectorClock(),
            opts,
            identityProvider,
            signer);
        _cleanup.Add(daemon);
        return daemon;
    }

    private InMemoryPeerDiscovery BuildDiscovery(InMemoryPeerDiscoveryBroker? broker = null)
    {
        broker ??= new InMemoryPeerDiscoveryBroker();
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
}
