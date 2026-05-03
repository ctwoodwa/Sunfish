using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.LocalNodeHost;

/// <summary>
/// Wave 6.3.E.2 startup hosted service that materializes the install's teams
/// via <see cref="ITeamContextFactory"/>, opens each team's encrypted store
/// via <see cref="ITeamStoreActivator"/>, and seeds the initial active team
/// on <see cref="IActiveTeamAccessor"/>.
/// </summary>
/// <remarks>
/// <para>
/// Runs BEFORE <see cref="LocalNodeWorker"/> in <c>IHostedService</c>
/// registration order so the worker's <see cref="IActiveTeamAccessor.Active"/>
/// lookup sees a materialized team when it resolves the per-team
/// <c>IGossipDaemon</c>.
/// </para>
/// <para>
/// Two modes per <see cref="MultiTeamOptions.Enabled"/>:
/// <list type="bullet">
///   <item><description><c>Enabled == true</c>: iterate
///     <see cref="MultiTeamOptions.TeamBootstraps"/>; for each, call
///     <c>ITeamContextFactory.GetOrCreateAsync</c> then
///     <c>ITeamStoreActivator.ActivateAsync</c>. The first listed team becomes
///     the initial active team.</description></item>
///   <item><description><c>Enabled == false</c> (legacy single-team): parse
///     <see cref="LocalNodeOptions.TeamId"/> as a Guid, materialize + activate
///     that one team, set it active.</description></item>
/// </list>
/// When <see cref="LocalNodeOptions.TeamId"/> is null/empty under legacy mode,
/// we synthesize a fresh Guid so the host still has a team context on first
/// boot — downstream waves will replace the default with a provisioned id.
/// </para>
/// </remarks>
public sealed class MultiTeamBootstrapHostedService : IHostedService
{
    private readonly ITeamContextFactory _factory;
    private readonly ITeamStoreActivator _activator;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly IOptions<LocalNodeOptions> _options;
    private readonly ILogger<MultiTeamBootstrapHostedService> _logger;

    public MultiTeamBootstrapHostedService(
        ITeamContextFactory factory,
        ITeamStoreActivator activator,
        IActiveTeamAccessor activeTeam,
        IOptions<LocalNodeOptions> options,
        ILogger<MultiTeamBootstrapHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(activator);
        ArgumentNullException.ThrowIfNull(activeTeam);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _factory = factory;
        _activator = activator;
        _activeTeam = activeTeam;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var multi = options.MultiTeam ?? new MultiTeamOptions();

        TeamId? firstTeamId = null;

        if (multi.Enabled && multi.TeamBootstraps.Count > 0)
        {
            _logger.LogInformation(
                "MultiTeam bootstrap: materializing {Count} team(s)",
                multi.TeamBootstraps.Count);

            foreach (var bootstrap in multi.TeamBootstraps)
            {
                var teamId = new TeamId(bootstrap.TeamId);
                var displayName = !string.IsNullOrWhiteSpace(bootstrap.DisplayName)
                    ? bootstrap.DisplayName!
                    : $"Team {bootstrap.TeamId:D}";

                await _factory.GetOrCreateAsync(teamId, displayName, cancellationToken)
                    .ConfigureAwait(false);
                await _activator.ActivateAsync(teamId, cancellationToken)
                    .ConfigureAwait(false);

                firstTeamId ??= teamId;
            }
        }
        else
        {
            // Legacy single-team mode. Parse LocalNodeOptions.TeamId as Guid;
            // synthesize a fresh value if unset so the host still has a team
            // context on first boot (Wave 2.1 behaviour parity).
            Guid guid;
            if (!string.IsNullOrWhiteSpace(options.TeamId) &&
                Guid.TryParse(options.TeamId, out var parsed))
            {
                guid = parsed;
            }
            else
            {
                guid = Guid.NewGuid();
                _logger.LogWarning(
                    "LocalNode:TeamId not configured; synthesized {TeamId} for single-team legacy bootstrap.",
                    guid);
            }

            var teamId = new TeamId(guid);
            var displayName = $"Team {guid:D}";

            await _factory.GetOrCreateAsync(teamId, displayName, cancellationToken)
                .ConfigureAwait(false);
            await _activator.ActivateAsync(teamId, cancellationToken)
                .ConfigureAwait(false);

            firstTeamId = teamId;
        }

        if (firstTeamId is { } seedId)
        {
            await _activeTeam.SetActiveAsync(seedId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Active team set to {TeamId}", seedId);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No-op — TeamContextFactory owns per-team disposal.
        return Task.CompletedTask;
    }
}
