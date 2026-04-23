using System.Runtime.InteropServices;

namespace Sunfish.LocalNodeHost;

/// <summary>
/// Host-wide configuration for the local-node process. Bound from the
/// <c>LocalNode</c> configuration section. See <c>appsettings.json</c> for the
/// default binding and the README for the service-manager integration
/// roadmap (Wave 4).
/// </summary>
public sealed class LocalNodeOptions
{
    /// <summary>
    /// Stable identifier for this local node, persisted across restarts once
    /// set. Defaults to a fresh GUID so the first boot is never anonymous;
    /// downstream waves (2.1 sync daemon, 3.3 Anchor) are expected to pin
    /// this from a provisioned identity store rather than accept the default.
    /// </summary>
    public string NodeId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Optional team identifier. Populated once the node is enrolled in a
    /// Sunfish team; null on a fresh single-user install. Consumed by the
    /// Wave 6.3.E.2 single-team legacy bootstrap path (when
    /// <see cref="MultiTeamOptions.Enabled"/> is <c>false</c>).
    /// </summary>
    public string? TeamId { get; set; }

    /// <summary>
    /// Multi-team bootstrap configuration. When
    /// <see cref="MultiTeamOptions.Enabled"/> is <c>true</c>, the Wave 6.3.E.2
    /// <c>MultiTeamBootstrapHostedService</c> materializes the configured
    /// <see cref="MultiTeamOptions.TeamBootstraps"/> instead of the legacy
    /// single-team <see cref="TeamId"/> path.
    /// </summary>
    public MultiTeamOptions MultiTeam { get; set; } = new();

    /// <summary>
    /// Root directory for node-local state: encrypted store, event log,
    /// quarantine queue, key material, projections. Defaults to the
    /// platform-conventional per-user application data location (see
    /// <see cref="GetDefaultDataDirectory"/>).
    /// </summary>
    public string DataDirectory { get; set; } = GetDefaultDataDirectory();

    /// <summary>
    /// Platform-conventional default for <see cref="DataDirectory"/>:
    /// <list type="bullet">
    ///   <item>Windows: <c>%LOCALAPPDATA%\Sunfish\LocalNode</c></item>
    ///   <item>macOS: <c>~/Library/Application Support/Sunfish/LocalNode</c></item>
    ///   <item>Linux: <c>$XDG_DATA_HOME/sunfish/local-node</c> (falls back to <c>~/.local/share</c>).</item>
    /// </list>
    /// Kept as a static helper so tests can inject a writable temp directory without
    /// relying on the host environment.
    /// </summary>
    public static string GetDefaultDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Sunfish", "LocalNode");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Sunfish", "LocalNode");
        }

        // Linux / BSD / other Unix: honor XDG_DATA_HOME, fall back to ~/.local/share.
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
        {
            return Path.Combine(xdg, "sunfish", "local-node");
        }
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userHome, ".local", "share", "sunfish", "local-node");
    }
}

/// <summary>
/// Wave 6.3.E.2 multi-team bootstrap configuration. Controls whether the host
/// materializes a single legacy team (via <see cref="LocalNodeOptions.TeamId"/>)
/// or an explicit list of teams at startup.
/// </summary>
public sealed class MultiTeamOptions
{
    /// <summary>
    /// When <c>true</c>, the <c>MultiTeamBootstrapHostedService</c> iterates
    /// <see cref="TeamBootstraps"/> and materializes each listed team. When
    /// <c>false</c> (the default — legacy single-team mode), a single team is
    /// materialized from <see cref="LocalNodeOptions.TeamId"/>.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Ordered list of teams to materialize at startup when
    /// <see cref="Enabled"/> is <c>true</c>. The first entry becomes the
    /// initial active team via <see cref="Sunfish.Kernel.Runtime.Teams.IActiveTeamAccessor"/>.
    /// </summary>
    public List<TeamBootstrap> TeamBootstraps { get; set; } = new();
}

/// <summary>
/// One configured team in <see cref="MultiTeamOptions.TeamBootstraps"/>. Carries
/// the team's GUID identifier plus an optional human-readable display name
/// forwarded to <c>ITeamContextFactory.GetOrCreateAsync</c>.
/// </summary>
public sealed class TeamBootstrap
{
    /// <summary>The team GUID (will be wrapped in a <see cref="Sunfish.Kernel.Runtime.Teams.TeamId"/>).</summary>
    public required Guid TeamId { get; set; }

    /// <summary>
    /// Optional display name surfaced in the team-switcher UI. When
    /// <c>null</c>, the bootstrap synthesizes a default
    /// (<c>"Team {guid:D}"</c>).
    /// </summary>
    public string? DisplayName { get; set; }
}
