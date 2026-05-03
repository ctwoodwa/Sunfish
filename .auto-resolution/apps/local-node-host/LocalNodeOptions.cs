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
    /// TCP port the Wave 5.2.D health endpoint binds to. <c>0</c> (the
    /// default) means "auto-assign" — the host listens on whatever
    /// <c>ASPNETCORE_URLS</c> specifies, or lets Kestrel pick a free
    /// ephemeral port when no URL is configured. Bridge's
    /// <see cref="Sunfish.Bridge.Orchestration.ITenantProcessSupervisor"/>
    /// sets this explicitly per child (Wave 5.2.C) so
    /// <see cref="Sunfish.Bridge.Orchestration.TenantHealthMonitor"/> knows
    /// where to poll.
    /// </summary>
    public int HealthPort { get; set; } = 0;

    /// <summary>
    /// Optional hex-encoded 32-byte Ed25519 root seed injected by a parent
    /// supervisor. Wave 5.2 stop-work #1: Bridge's
    /// <see cref="Sunfish.Bridge.Orchestration.ITenantSeedProvider"/>
    /// HKDF-expands the Bridge install-level root seed with info
    /// <c>"sunfish:bridge:tenant-seed:v1:{TenantId}"</c> and passes the
    /// result to each spawned <c>local-node-host</c> child via the
    /// <c>LocalNode__RootSeedHex</c> environment variable. When set, the
    /// host honors the injected seed and <b>skips its own keystore
    /// lookup</b>, giving each tenant on a shared Bridge host a
    /// cryptographically independent root identity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Direct installs leave this null.</b> Anchor and standalone
    /// <c>local-node-host</c> runs continue to use the default keystore-
    /// backed <see cref="Sunfish.Kernel.Security.Keys.IRootSeedProvider"/>
    /// path. Only Bridge-spawned children set this value.
    /// </para>
    /// <para>
    /// <b>Validation.</b> The value must be a 64-character hex string
    /// (32 raw bytes). Any other length / encoding is treated as a
    /// configuration error and fails the host startup.
    /// </para>
    /// <para>
    /// <b>Trust model.</b> The env-var injection is trusted because parent
    /// and child share the same OS security domain. Deeper attestation
    /// (signed seed envelopes, mutual TLS) is a future wave.
    /// </para>
    /// </remarks>
    public string? RootSeedHex { get; set; }

    /// <summary>
    /// Wave 5.3.C — browser-facing WebSocket sync-daemon endpoint configuration.
    /// Controls whether <c>/ws</c> is mapped on the shared Kestrel listener and
    /// what per-message size cap is enforced on the inbound side.
    /// </summary>
    public BrowserWebSocketOptions BrowserWebSocket { get; set; } = new();

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
/// Wave 5.3.C configuration for the <c>/ws</c> endpoint that carries the
/// WebSocket-framed sync-daemon transport. See
/// <c>_shared/product/wave-5.3-decomposition.md</c> §5.3.C for the full
/// design. Bound from <c>LocalNode:BrowserWebSocket</c>.
/// </summary>
public sealed class BrowserWebSocketOptions
{
    /// <summary>
    /// When <c>true</c> (the default) the hosted WebSocket endpoint maps
    /// <c>/ws</c> on the shared Kestrel listener and hands inbound
    /// connections to the registered <c>ISyncDaemonAcceptor</c>. When
    /// <c>false</c> the path is not mapped — useful for tenant children that
    /// intentionally expose only <c>/health</c> (e.g. Anchor direct-install).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum inbound WebSocket message size in bytes. Frames exceeding the
    /// cap are rejected with a <c>MessageTooBig</c> close. Default 4 MiB
    /// matches the sync-daemon-protocol §2.2 guidance for non-snapshot
    /// frames while leaving headroom below the 16 MiB hard-cap.
    /// </summary>
    public int MaxMessageBytes { get; set; } = 4 * 1024 * 1024;
}

/// <summary>
/// Wave 6.3.E.2 multi-team bootstrap configuration. Controls whether the host
/// materializes a single legacy team (via <see cref="LocalNodeOptions.TeamId"/>)
/// or an explicit list of teams at startup.
/// </summary>
public sealed class MultiTeamOptions
{
    /// <summary>
    /// When <c>true</c> (the default as of Wave 6.7), the
    /// <c>MultiTeamBootstrapHostedService</c> iterates
    /// <see cref="TeamBootstraps"/> and materializes each listed team. When
    /// <c>false</c>, a single legacy team is materialized from
    /// <see cref="LocalNodeOptions.TeamId"/>.
    /// </summary>
    /// <remarks>
    /// Wave 6.3 shipped this gated to <c>false</c> so v1 installs would not
    /// strand their top-level <c>sunfish.db</c> under the new
    /// <c>teams/{id}/</c> layout before the migration landed. Wave 6.7
    /// introduces <c>AnchorV1MigrationService</c> (accelerators/anchor) which
    /// detects the v1 layout on first launch and moves it into place, so
    /// multi-team is now the safe default. Downstream composition roots can
    /// still opt out by explicitly setting
    /// <c>LocalNode:MultiTeam:Enabled = false</c> in configuration.
    /// </remarks>
    public bool Enabled { get; set; } = true;

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
