using Sunfish.Foundation.MissionSpace;

namespace Sunfish.Anchor.Services;

/// <summary>
/// ADR 0063-A1.1 platform-key and dimension-key helpers for the Anchor MAUI
/// ISystemRequirementsRenderer. Extracted as statics so tests.csproj can
/// validate mappings without pulling in the MAUI workload.
/// </summary>
public static class SystemRequirementsViewHelpers
{
    /// <summary>
    /// Maps a <c>DevicePlatform.ToString()</c> value to the ADR 0063 §163
    /// platform key used in <see cref="IMinimumSpecResolver.EvaluateAsync"/>.
    /// Falls back to <c>"unknown"</c> for unrecognised platform strings.
    /// </summary>
    public static string GetPlatformKey(string platform) => platform switch
    {
        "WinUI"        => "windows-desktop",
        "MacCatalyst"  => "macos-desktop",
        "iOS"          => "ios",
        "Android"      => "android",
        _              => "unknown",
    };

    /// <summary>
    /// Returns the RESX key fragment used to look up the dimension display name:
    /// <c>sysreq.dimension.{fragment}.name</c>. The fragment matches the
    /// handoff §Phase 1 key roster; <c>TrustAnchor</c> maps to <c>"Trust"</c>
    /// (user-facing shorthand per the spec).
    /// </summary>
    public static string GetDimensionLocKey(DimensionChangeKind dimension) => dimension switch
    {
        DimensionChangeKind.Hardware       => "Hardware",
        DimensionChangeKind.User           => "User",
        DimensionChangeKind.Regulatory     => "Regulatory",
        DimensionChangeKind.Runtime        => "Runtime",
        DimensionChangeKind.FormFactor     => "FormFactor",
        DimensionChangeKind.Edition        => "Edition",
        DimensionChangeKind.Network        => "Network",
        DimensionChangeKind.TrustAnchor    => "Trust",
        DimensionChangeKind.SyncState      => "SyncState",
        DimensionChangeKind.VersionVector  => "VersionVector",
        _                                  => dimension.ToString(),
    };
}
