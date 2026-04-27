using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Sync.Discovery;
using Sunfish.Kernel.Sync.Gossip;
using Sunfish.Kernel.Sync.Identity;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Phase 1 G1 + G4 follow-up — Anchor sync daemon hosted service. Drives
/// the <see cref="IGossipDaemon"/> lifecycle on the MAUI app's hosted-service
/// pipeline. On <see cref="StartAsync"/> it builds the local
/// <see cref="PeerAdvertisement"/> (self) from the configured node identity
/// + active team, starts BOTH the LAN discovery source
/// (<see cref="IPeerDiscovery"/>, typically <see cref="MdnsPeerDiscovery"/>)
/// and the WAN <see cref="ManagedRelayPeerDiscovery"/>, attaches each to the
/// gossip daemon, then starts the daemon's round scheduler. On
/// <see cref="StopAsync"/> the order reverses: daemon stops, attach
/// subscriptions dispose, both discoveries stop.
/// </summary>
/// <remarks>
/// <para>
/// This service must be registered AFTER <see cref="AnchorBootstrapHostedService"/>
/// so the default team's identity, encrypted store, and active-team accessor
/// are materialized before the daemon advertises its node identity to peers.
/// MAUI's <see cref="MauiHostedServiceLifetime"/> pumps hosted services in
/// registration order on the MAUI app's <c>Window.Created</c> /
/// <c>Window.Destroying</c> hooks.
/// </para>
/// <para>
/// <b>Profile C from the connection-topology spec.</b> Anchor's recommended
/// Phase 1 deployment attaches BOTH a tier-1 LAN discovery source (mDNS) and
/// the tier-3 WAN <see cref="ManagedRelayPeerDiscovery"/>. The two sources
/// surface peers independently — LAN peers reach via direct UDS / Named Pipe
/// transport, WAN peers reach via the configured Bridge relay endpoint. If
/// the relay is unconfigured (empty <see cref="ManagedRelayPeerDiscoveryOptions.RelayUrl"/>),
/// its <see cref="IPeerDiscovery.StartAsync"/> is a no-op and only the LAN
/// source produces peers — that's the LAN-only deployment shape.
/// </para>
/// <para>
/// Per <see cref="GossipDaemonDiscoveryExtensions.AttachDiscovery"/>, each
/// returned <see cref="IDisposable"/> must be retained and disposed during
/// <see cref="StopAsync"/> so the discovery subscriptions are unsubscribed
/// cleanly. The daemon's peer table is not cleared on subscription dispose
/// — that's the "discovery is advisory, gossip owns membership" boundary
/// from paper §6.1.
/// </para>
/// <para>
/// The hosted service intentionally takes the discovery sources by interface
/// or concrete type — neither references <c>Microsoft.Maui.Storage</c>, so the
/// file compiles and unit-tests against the pure-.NET
/// <see cref="InMemoryPeerDiscovery"/> +
/// <see cref="Sunfish.Kernel.Sync.Protocol.InMemorySyncDaemonTransport"/>
/// stack from the test project's source-link compilation pattern.
/// </para>
/// </remarks>
public sealed class AnchorSyncHostedService : IHostedService
{
    private readonly IGossipDaemon _daemon;
    private readonly IPeerDiscovery _discovery;
    private readonly ManagedRelayPeerDiscovery _relayDiscovery;
    private readonly INodeIdentityProvider _nodeIdentity;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly ILogger<AnchorSyncHostedService> _logger;
    private IDisposable? _discoverySubscription;
    private IDisposable? _relayDiscoverySubscription;
    private bool _discoveryStarted;
    private bool _relayDiscoveryStarted;

    /// <summary>Construct the sync hosted service.</summary>
    /// <param name="daemon">Gossip daemon (paper §6.1) registered by <c>AddSunfishKernelSync</c>.</param>
    /// <param name="discovery">Tier-1 LAN peer discovery — typically mDNS in production or
    /// <see cref="InMemoryPeerDiscovery"/> in tests.</param>
    /// <param name="relayDiscovery">Tier-3 WAN peer discovery (paper §17.2). Always registered;
    /// when <see cref="ManagedRelayPeerDiscoveryOptions.RelayUrl"/> is empty its <c>StartAsync</c>
    /// is a no-op so LAN-only deployments are unaffected.</param>
    /// <param name="nodeIdentity">Local node's Ed25519 identity provider; supplies the
    /// <c>NodeId</c> + <c>PublicKey</c> in the self <see cref="PeerAdvertisement"/>.</param>
    /// <param name="activeTeam">Active-team accessor; supplies the <c>TeamId</c> in the
    /// self <see cref="PeerAdvertisement"/>. Materialized by
    /// <see cref="AnchorBootstrapHostedService"/> before this service starts.</param>
    /// <param name="logger">Logger.</param>
    public AnchorSyncHostedService(
        IGossipDaemon daemon,
        IPeerDiscovery discovery,
        ManagedRelayPeerDiscovery relayDiscovery,
        INodeIdentityProvider nodeIdentity,
        IActiveTeamAccessor activeTeam,
        ILogger<AnchorSyncHostedService> logger)
    {
        _daemon = daemon ?? throw new ArgumentNullException(nameof(daemon));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _relayDiscovery = relayDiscovery ?? throw new ArgumentNullException(nameof(relayDiscovery));
        _nodeIdentity = nodeIdentity ?? throw new ArgumentNullException(nameof(nodeIdentity));
        _activeTeam = activeTeam ?? throw new ArgumentNullException(nameof(activeTeam));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var self = BuildSelfAdvertisement();
        if (self is null)
        {
            // Active team not materialized yet — bootstrap hosted service should have run
            // first; if it didn't, advertising on the wire with a missing team id would
            // produce a malformed PeerAdvertisement. Log and skip.
            _logger.LogWarning(
                "Anchor sync: skipping start — active team not yet materialized. " +
                "Verify AnchorBootstrapHostedService runs before AnchorSyncHostedService.");
            return;
        }

        _logger.LogInformation(
            "Anchor sync: starting LAN discovery {LanDiscoveryType} and managed-relay discovery, " +
            "attaching both to gossip daemon, then starting round scheduler",
            _discovery.GetType().Name);

        // Start discoveries first so their KnownPeers are populated before the daemon's
        // AttachDiscovery seeds membership. Both StartAsync calls are idempotent on the
        // discovery side; a previously-started source returns without re-emitting.
        await _discovery.StartAsync(self, cancellationToken).ConfigureAwait(false);
        _discoveryStarted = true;

        await _relayDiscovery.StartAsync(self, cancellationToken).ConfigureAwait(false);
        _relayDiscoveryStarted = true;

        _discoverySubscription = _daemon.AttachDiscovery(_discovery);
        _relayDiscoverySubscription = _daemon.AttachDiscovery(_relayDiscovery);

        await _daemon.StartAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Anchor sync daemon started");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Anchor sync: stopping daemon, disposing discovery subscriptions, stopping discoveries");

        await _daemon.StopAsync(cancellationToken).ConfigureAwait(false);
        _discoverySubscription?.Dispose();
        _discoverySubscription = null;
        _relayDiscoverySubscription?.Dispose();
        _relayDiscoverySubscription = null;

        if (_relayDiscoveryStarted)
        {
            try { await _relayDiscovery.StopAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogWarning(ex, "Relay discovery stop failed"); }
            _relayDiscoveryStarted = false;
        }
        if (_discoveryStarted)
        {
            try { await _discovery.StopAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { _logger.LogWarning(ex, "LAN discovery stop failed"); }
            _discoveryStarted = false;
        }

        _logger.LogInformation("Anchor sync daemon stopped");
    }

    /// <summary>
    /// Build the local <see cref="PeerAdvertisement"/> from the configured node identity
    /// + active team. Returns <c>null</c> when no team is active yet — the caller logs and
    /// skips start. The endpoint follows the named-pipe convention introduced in
    /// MauiProgram (Phase 1 G1, ADR 0044): <c>sunfish-anchor-{nodeId[..8]}</c>.
    /// </summary>
    private PeerAdvertisement? BuildSelfAdvertisement()
    {
        var teamContext = _activeTeam.Active;
        if (teamContext is null)
        {
            return null;
        }

        var identity = _nodeIdentity.Current;
        var endpoint = $"sunfish-anchor-{identity.NodeId[..Math.Min(8, identity.NodeId.Length)]}";

        return new PeerAdvertisement(
            NodeId: identity.NodeId,
            Endpoint: endpoint,
            PublicKey: identity.PublicKey,
            TeamId: teamContext.TeamId.Value.ToString("D"),
            SchemaVersion: "1.0",
            Metadata: new Dictionary<string, string>
            {
                ["anchor"] = "1",
                ["host"] = "MAUI",
            });
    }
}
