using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Wave 6.3.F Anchor-shell bootstrap hosted service. Materializes the single
/// default team on app launch via <see cref="ITeamContextFactory"/>, opens its
/// encrypted store via <see cref="ITeamStoreActivator"/>, and seeds the active
/// team on <see cref="IActiveTeamAccessor"/>. Anchor is a MAUI client shell
/// running a single team at a time — the multi-team fan-out lives in
/// <c>local-node-host</c> (Wave 6.3.E.2).
/// </summary>
/// <remarks>
/// <para>
/// Analogous to <c>MultiTeamBootstrapHostedService</c> in
/// <c>apps/local-node-host</c> but scoped down to one team: Anchor does not
/// iterate a configured list. A fresh Guid is synthesized on first launch
/// (and written to MAUI's <c>Preferences</c> store so subsequent launches see
/// the same team id). The "join additional team" flow that lets this id be
/// replaced with a real provisioned team id ships in Wave 6.8.
/// </para>
/// <para>
/// Null-active-team guard on downstream services (<c>QrOnboardingService</c>)
/// throws a clear <see cref="InvalidOperationException"/> rather than an NRE —
/// but with this bootstrap in place, the active team is always materialized
/// before Blazor pages render. See Wave 6.3.F Part 2 Option B rationale in
/// <c>_shared/product/wave-6.3-decomposition.md</c> §6.3.F.
/// </para>
/// </remarks>
public sealed class AnchorBootstrapHostedService : IHostedService
{
    /// <summary>
    /// <see cref="Preferences"/> key used to persist the synthesized default
    /// team id across app launches. Wave 6.8's join-team flow replaces this
    /// with a provisioned team id delivered via the onboarding QR.
    /// </summary>
    public const string DefaultTeamIdPreferenceKey = "sunfish.anchor.default-team-id";

    private readonly ITeamContextFactory _factory;
    private readonly ITeamStoreActivator _activator;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly ILogger<AnchorBootstrapHostedService> _logger;

    /// <summary>Construct the bootstrap service.</summary>
    public AnchorBootstrapHostedService(
        ITeamContextFactory factory,
        ITeamStoreActivator activator,
        IActiveTeamAccessor activeTeam,
        ILogger<AnchorBootstrapHostedService> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _activator = activator ?? throw new ArgumentNullException(nameof(activator));
        _activeTeam = activeTeam ?? throw new ArgumentNullException(nameof(activeTeam));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var teamId = ReadOrCreateDefaultTeamId();
        var displayName = $"Team {teamId.Value:D}";

        _logger.LogInformation(
            "Anchor bootstrap: materializing default team {TeamId}",
            teamId);

        await _factory.GetOrCreateAsync(teamId, displayName, cancellationToken)
            .ConfigureAwait(false);
        await _activator.ActivateAsync(teamId, cancellationToken)
            .ConfigureAwait(false);
        await _activeTeam.SetActiveAsync(teamId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Active team set to {TeamId}", teamId);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No-op — TeamContextFactory owns per-team disposal.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Read the persisted default team id from MAUI preferences; on first
    /// launch, synthesize a new <see cref="Guid"/> and persist it.
    /// </summary>
    /// <remarks>
    /// TODO (Wave 6.8): replace the synthesized-Guid fallback with the join-team
    /// QR-scan flow so the default team id is provisioned by the founder
    /// device rather than locally invented.
    /// </remarks>
    private static TeamId ReadOrCreateDefaultTeamId()
    {
        var existing = Preferences.Get(DefaultTeamIdPreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing) &&
            Guid.TryParse(existing, out var parsed))
        {
            return new TeamId(parsed);
        }

        var fresh = Guid.NewGuid();
        Preferences.Set(DefaultTeamIdPreferenceKey, fresh.ToString("D"));
        return new TeamId(fresh);
    }
}
