using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Sunfish.Anchor.Services;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Sync.Discovery;
using Sunfish.Kernel.Sync.Gossip;
using Sunfish.Kernel.Sync.Identity;
using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// Phase 1 G1 + G4 follow-up — Anchor sync hosted service wiring. Verifies
/// that <see cref="AnchorSyncHostedService"/> starts both the LAN discovery
/// source and the WAN <see cref="ManagedRelayPeerDiscovery"/>, attaches each
/// to the gossip daemon, and starts the round scheduler on host startup.
/// On shutdown the order reverses cleanly.
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
        var relayDiscovery = BuildNoOpRelayDiscovery();
        var identity = BuildNodeIdentity();
        var activeTeam = BuildActiveTeam();

        var svc = new AnchorSyncHostedService(
            daemon, discovery, relayDiscovery, identity, activeTeam,
            NullLogger<AnchorSyncHostedService>.Instance);

        Assert.False(daemon.IsRunning);

        await svc.StartAsync(CancellationToken.None);

        Assert.True(daemon.IsRunning);
    }

    [Fact]
    public async Task StopAsync_stops_the_gossip_daemon()
    {
        var daemon = BuildDaemon();
        var discovery = BuildDiscovery();
        var relayDiscovery = BuildNoOpRelayDiscovery();
        var identity = BuildNodeIdentity();
        var activeTeam = BuildActiveTeam();

        var svc = new AnchorSyncHostedService(
            daemon, discovery, relayDiscovery, identity, activeTeam,
            NullLogger<AnchorSyncHostedService>.Instance);

        await svc.StartAsync(CancellationToken.None);
        Assert.True(daemon.IsRunning);

        await svc.StopAsync(CancellationToken.None);

        Assert.False(daemon.IsRunning);
    }

    [Fact]
    public async Task StartAsync_seeds_daemon_with_already_discovered_LAN_peers()
    {
        var broker = new InMemoryPeerDiscoveryBroker();
        var daemon = BuildDaemon();
        var ourDiscovery = BuildDiscovery(broker);
        var peerDiscovery = BuildDiscovery(broker);
        var identity = BuildNodeIdentity();
        var activeTeam = BuildActiveTeam();

        // Peer is already advertising before our hosted service starts.
        // Use the team id our IActiveTeamAccessor returns so FilterByTeamId passes.
        var teamId = activeTeam.Active!.TeamId.Value.ToString("D");
        await peerDiscovery.StartAsync(
            Ad("node-peer", "in-mem://peer", teamId: teamId),
            CancellationToken.None);

        // Pre-start ourDiscovery so InMemoryPeerDiscovery's idempotent StartAsync
        // observes the peer ad before AnchorSyncHostedService attaches it. The
        // hosted service's own StartAsync call is a no-op on already-started
        // sources.
        await ourDiscovery.StartAsync(
            Ad("node-self", "in-mem://self", teamId: teamId),
            CancellationToken.None);

        // Wait for the in-memory broker to deliver the discovery event.
        for (var i = 0; i < 50 && ourDiscovery.KnownPeers.Count == 0; i++)
        {
            await Task.Delay(20);
        }

        Assert.NotEmpty(ourDiscovery.KnownPeers);

        var svc = new AnchorSyncHostedService(
            daemon, ourDiscovery, BuildNoOpRelayDiscovery(), identity, activeTeam,
            NullLogger<AnchorSyncHostedService>.Instance);
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
        var identity = BuildNodeIdentity();
        var activeTeam = BuildActiveTeam();
        var teamId = activeTeam.Active!.TeamId.Value.ToString("D");

        await ourDiscovery.StartAsync(
            Ad("node-self", "in-mem://self", teamId: teamId),
            CancellationToken.None);

        var svc = new AnchorSyncHostedService(
            daemon, ourDiscovery, BuildNoOpRelayDiscovery(), identity, activeTeam,
            NullLogger<AnchorSyncHostedService>.Instance);
        await svc.StartAsync(CancellationToken.None);

        // Stop the hosted service. Discovery subscription must be disposed —
        // any subsequent PeerDiscovered event from the broker should NOT add
        // a peer to the daemon (paper §6.1: "discovery is advisory").
        await svc.StopAsync(CancellationToken.None);
        var peerCountAfterStop = daemon.KnownPeers.Count;

        // New peer joins after the hosted service has stopped.
        var latePeerDiscovery = BuildDiscovery(broker);
        await latePeerDiscovery.StartAsync(
            Ad("node-late", "in-mem://late", teamId: teamId),
            CancellationToken.None);

        // Give the broker a moment to deliver — but the subscription is gone,
        // so the daemon's peer set must not grow.
        await Task.Delay(100);

        Assert.Equal(peerCountAfterStop, daemon.KnownPeers.Count);
        Assert.DoesNotContain(daemon.KnownPeers, p => p.Endpoint == "in-mem://late");
    }

    [Fact]
    public async Task StartAsync_attaches_BOTH_LAN_and_relay_discoveries_to_daemon()
    {
        // Phase 1 G4 follow-up — when the relay discovery is configured with a
        // non-empty RelayUrl, AnchorSyncHostedService attaches BOTH discoveries
        // and the daemon's peer set surfaces a relay peer alongside any LAN peers.
        var daemon = BuildDaemon();
        var lanDiscovery = BuildDiscovery();
        var relayDiscovery = new ManagedRelayPeerDiscovery(new ManagedRelayPeerDiscoveryOptions
        {
            RelayUrl = "wss://relay.example.com/sync",
            RelayNodeId = "abcdef0123456789abcdef0123456789",
            RelayPublicKey = new byte[32],
        });
        _cleanup.Add(relayDiscovery);

        var svc = new AnchorSyncHostedService(
            daemon, lanDiscovery, relayDiscovery,
            BuildNodeIdentity(), BuildActiveTeam(),
            NullLogger<AnchorSyncHostedService>.Instance);

        await svc.StartAsync(CancellationToken.None);

        // The relay surface emits a single peer for the configured Bridge.
        Assert.Contains(daemon.KnownPeers, p => p.Endpoint == "wss://relay.example.com/sync");
    }

    [Fact]
    public async Task StartAsync_with_empty_RelayUrl_only_attaches_LAN_discovery()
    {
        // The LAN-only deployment shape — relay discovery is registered (uniform
        // composition) but its RelayUrl is empty, so its StartAsync no-ops and
        // the daemon's peer set never sees a relay peer.
        var daemon = BuildDaemon();
        var lanDiscovery = BuildDiscovery();
        var noOpRelay = BuildNoOpRelayDiscovery();

        var svc = new AnchorSyncHostedService(
            daemon, lanDiscovery, noOpRelay,
            BuildNodeIdentity(), BuildActiveTeam(),
            NullLogger<AnchorSyncHostedService>.Instance);

        await svc.StartAsync(CancellationToken.None);

        Assert.Empty(daemon.KnownPeers); // no LAN peers, no relay peer
    }

    [Fact]
    public async Task StartAsync_skips_when_active_team_is_null()
    {
        // If AnchorBootstrapHostedService hasn't materialized the default team yet
        // (or if some teardown cleared it), AnchorSyncHostedService logs and
        // returns rather than building a malformed advertisement. The daemon
        // remains stopped so a follow-up host restart is clean.
        var daemon = BuildDaemon();
        var nullActiveTeam = new FakeActiveTeamAccessor(null);

        var svc = new AnchorSyncHostedService(
            daemon, BuildDiscovery(), BuildNoOpRelayDiscovery(),
            BuildNodeIdentity(), nullActiveTeam,
            NullLogger<AnchorSyncHostedService>.Instance);

        await svc.StartAsync(CancellationToken.None);

        Assert.False(daemon.IsRunning);
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

    private ManagedRelayPeerDiscovery BuildNoOpRelayDiscovery()
    {
        var d = new ManagedRelayPeerDiscovery(new ManagedRelayPeerDiscoveryOptions());
        _cleanup.Add(d);
        return d;
    }

    private static INodeIdentityProvider BuildNodeIdentity()
    {
        var signer = new Ed25519Signer();
        var (pk, sk) = signer.GenerateKeyPair();
        var nodeIdBytes = new byte[16];
        Buffer.BlockCopy(pk, 0, nodeIdBytes, 0, 16);
        return new InMemoryNodeIdentityProvider(new NodeIdentity(
            NodeId: Convert.ToHexString(nodeIdBytes).ToLowerInvariant(),
            PublicKey: pk,
            PrivateKey: sk));
    }

    private static IActiveTeamAccessor BuildActiveTeam()
    {
        var team = new TeamContext(
            teamId: new TeamId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            displayName: "Anchor Test Team",
            services: new ServiceCollection().BuildServiceProvider());
        return new FakeActiveTeamAccessor(team);
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

    private sealed class FakeActiveTeamAccessor : IActiveTeamAccessor
    {
        public TeamContext? Active { get; }
        public event EventHandler<ActiveTeamChangedEventArgs>? ActiveChanged
        {
            add { /* tests do not subscribe; required by interface */ }
            remove { /* tests do not subscribe; required by interface */ }
        }
        public FakeActiveTeamAccessor(TeamContext? team) { Active = team; }
        public Task SetActiveAsync(TeamId teamId, CancellationToken ct)
            => throw new NotSupportedException("FakeActiveTeamAccessor does not support team switching");
    }
}
