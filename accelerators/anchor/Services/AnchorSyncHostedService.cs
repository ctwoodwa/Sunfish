using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Kernel.Sync.Discovery;
using Sunfish.Kernel.Sync.Gossip;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Phase 1 G1 — Anchor sync daemon hosted service. Drives the
/// <see cref="IGossipDaemon"/> lifecycle on the MAUI app's hosted-service
/// pipeline: attaches the configured <see cref="IPeerDiscovery"/> source
/// during <see cref="StartAsync"/>, then starts the gossip round scheduler.
/// On <see cref="StopAsync"/>, disposes the discovery subscription and stops
/// the daemon.
/// </summary>
/// <remarks>
/// <para>
/// This service must be registered AFTER <see cref="AnchorBootstrapHostedService"/>
/// so the default team's identity, encrypted store, and active-team accessor
/// are materialized before the daemon can advertise its node identity to
/// peers. MAUI's <see cref="MauiHostedServiceLifetime"/> pumps hosted services
/// in registration order on the MAUI app's <c>Window.Created</c> /
/// <c>Window.Destroying</c> hooks.
/// </para>
/// <para>
/// The hosted service intentionally takes <see cref="IGossipDaemon"/> and
/// <see cref="IPeerDiscovery"/> by interface — neither references
/// <c>Microsoft.Maui.Storage</c>, so the file compiles and unit-tests
/// against the pure-.NET <see cref="InMemoryPeerDiscovery"/> +
/// <see cref="Sunfish.Kernel.Sync.Protocol.InMemorySyncDaemonTransport"/>
/// stack from the test project's source-link compilation pattern (see
/// <c>accelerators/anchor/tests/tests.csproj</c>).
/// </para>
/// <para>
/// Per <see cref="GossipDaemonDiscoveryExtensions.AttachDiscovery"/>, the
/// returned <see cref="IDisposable"/> must be retained and disposed during
/// <see cref="StopAsync"/> so the discovery subscription is unsubscribed
/// cleanly. The daemon's peer table is not cleared on subscription dispose
/// — that's the "discovery is advisory, gossip owns membership" boundary
/// from paper §6.1.
/// </para>
/// </remarks>
public sealed class AnchorSyncHostedService : IHostedService
{
    private readonly IGossipDaemon _daemon;
    private readonly IPeerDiscovery _discovery;
    private readonly ILogger<AnchorSyncHostedService> _logger;
    private IDisposable? _discoverySubscription;

    /// <summary>Construct the sync hosted service.</summary>
    /// <param name="daemon">Gossip daemon (paper §6.1) registered by <c>AddSunfishKernelSync</c>.</param>
    /// <param name="discovery">Peer discovery source (e.g., mDNS for tier-1 LAN, managed-relay for tier-3 WAN).</param>
    /// <param name="logger">Logger.</param>
    public AnchorSyncHostedService(
        IGossipDaemon daemon,
        IPeerDiscovery discovery,
        ILogger<AnchorSyncHostedService> logger)
    {
        _daemon = daemon ?? throw new ArgumentNullException(nameof(daemon));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Anchor sync: attaching peer discovery {DiscoveryType} and starting gossip daemon",
            _discovery.GetType().Name);

        _discoverySubscription = _daemon.AttachDiscovery(_discovery);
        await _daemon.StartAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Anchor sync daemon started");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Anchor sync: stopping daemon and disposing discovery subscription");

        await _daemon.StopAsync(cancellationToken).ConfigureAwait(false);
        _discoverySubscription?.Dispose();
        _discoverySubscription = null;

        _logger.LogInformation("Anchor sync daemon stopped");
    }
}
