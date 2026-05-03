using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Kernel.Runtime;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Sync.Gossip;

namespace Sunfish.LocalNodeHost;

/// <summary>
/// Long-running hosted service that boots the Sunfish local-node kernel on
/// startup and unwinds it on application shutdown. The kernel itself drives
/// all per-tick work (event-bus dispatch, sync daemon, projection scheduler
/// in later waves) — this worker's job is purely lifecycle coordination,
/// so once start-up completes it parks on <see cref="Task.Delay(int, CancellationToken)"/>
/// until cancellation.
/// </summary>
/// <remarks>
/// <para>
/// Paper §4 calls for the container stack to run as a "persistent background
/// service registered with the OS service manager." The .NET generic host is
/// the stepping stone: we run exactly the same executable under
/// <c>dotnet run</c> during development and (Wave 4) under systemd / launchd /
/// Windows Service in production. No host-code branching is required; the
/// service-manager integration is packaging, not code.
/// </para>
/// <para>
/// Wave 6.3.E.2: gossip is now team-scoped — it lives in the active team
/// context's <see cref="TeamContext.Services"/> rather than on the install-
/// level service provider. The worker resolves it from
/// <see cref="IActiveTeamAccessor.Active"/> at start-up; if no team has been
/// materialized (or activation failed), the worker logs a warning and proceeds
/// without gossip so the host remains useful for plugin lifecycle.
/// </para>
/// <para>
/// The idle loop uses <see cref="Timeout.Infinite"/> rather than a polling
/// <c>while</c> loop so the process burns no CPU at rest. Cancellation from
/// either <see cref="IHostApplicationLifetime.ApplicationStopping"/> or an
/// external signal propagates via <paramref name="stoppingToken"/>, the
/// <see cref="Task.Delay(int, CancellationToken)"/> throws
/// <see cref="OperationCanceledException"/>, and shutdown proceeds deterministically.
/// </para>
/// </remarks>
public sealed class LocalNodeWorker : BackgroundService
{
    private readonly INodeHost _nodeHost;
    private readonly IPluginRegistry _pluginRegistry;
    private readonly IEnumerable<ILocalNodePlugin> _plugins;
    private readonly ITeamContextFactory _teamContextFactory;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly ILogger<LocalNodeWorker> _logger;

    public LocalNodeWorker(
        INodeHost nodeHost,
        IPluginRegistry pluginRegistry,
        IEnumerable<ILocalNodePlugin> plugins,
        ITeamContextFactory teamContextFactory,
        IActiveTeamAccessor activeTeam,
        ILogger<LocalNodeWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(nodeHost);
        ArgumentNullException.ThrowIfNull(pluginRegistry);
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentNullException.ThrowIfNull(teamContextFactory);
        ArgumentNullException.ThrowIfNull(activeTeam);
        ArgumentNullException.ThrowIfNull(logger);

        _nodeHost = nodeHost;
        _pluginRegistry = pluginRegistry;
        _plugins = plugins;
        _teamContextFactory = teamContextFactory;
        _activeTeam = activeTeam;
        _logger = logger;
    }

    /// <summary>Current host state; surfaced for tests and diagnostics.</summary>
    public NodeState CurrentState => _nodeHost.State;

    /// <summary>The plugin registry this worker drives; surfaced for tests and diagnostics.</summary>
    public IPluginRegistry PluginRegistry => _pluginRegistry;

    /// <summary>
    /// The active team's gossip daemon, if a team is currently active and its
    /// provider has one registered. Null on hosts that have not yet had a team
    /// materialized. Surfaced for tests and diagnostics.
    /// </summary>
    public IGossipDaemon? Gossip =>
        _activeTeam.Active?.Services.GetService<IGossipDaemon>();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Sunfish local-node host starting (node state: {State})",
            _nodeHost.State);

        // Materialize the injected plugin enumerable so tests can assert
        // against LoadedPlugins.Count without a second DI resolution.
        var pluginList = _plugins as IReadOnlyCollection<ILocalNodePlugin>
            ?? _plugins.ToList();

        await _pluginRegistry.LoadAllAsync(pluginList, stoppingToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Loaded {Count} plugin(s)",
            _pluginRegistry.LoadedPlugins.Count);

        await _nodeHost.StartAsync(stoppingToken).ConfigureAwait(false);

        // Wave 6.3.E.2: gossip is team-scoped. Resolve it from the active
        // team's provider; if no team is active or the team does not expose
        // IGossipDaemon, the host still runs (plugin lifecycle remains useful)
        // but gossip-dependent work is skipped.
        var gossip = _activeTeam.Active?.Services.GetService<IGossipDaemon>();
        if (gossip is not null)
        {
            // Start the gossip daemon — paper §6.1 tier-1 intra-team sync.
            // The daemon's 30s tick + peer fan-out begin immediately; it's safe
            // to start with an empty peer list and let peer discovery populate.
            await gossip.StartAsync(stoppingToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Gossip daemon started (peers: {Count})",
                gossip.KnownPeers.Count);
        }
        else
        {
            _logger.LogWarning(
                "No active team context exposes IGossipDaemon — local-node host running without sync. " +
                "Active team: {TeamId}",
                _activeTeam.Active?.TeamId.ToString() ?? "(none)");
        }

        // Idle loop — the kernel is the worker; we just wait. Timeout.Infinite
        // so the process burns no CPU; cancellation is the only exit path.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown. Swallow and fall through to teardown.
        }

        _logger.LogInformation("Sunfish local-node host stopping");

        // Shutdown uses a fresh CancellationToken.None so the shutdown path
        // runs to completion even when the caller's token is already cancelled.
        // Errors during unload are logged by the registry itself and do not
        // block subsequent restarts. Reverse-order of startup: gossip first,
        // node host second, plugins last.
        if (gossip is not null)
        {
            await gossip.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        await _nodeHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _pluginRegistry.UnloadAllAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
