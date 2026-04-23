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
    /// Sunfish team; null on a fresh single-user install.
    /// </summary>
    public string? TeamId { get; set; }

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
