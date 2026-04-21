namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Diagnostic verbosity for Sunfish components rendered beneath a
/// <see cref="Components.Utility.SunfishThemeProvider"/>. Cascaded to descendants
/// so individual components can opt into structured console logging without
/// taking a dependency on <c>Microsoft.Extensions.Logging</c>.
/// </summary>
public enum SunfishLogLevel
{
    /// <summary>Logging disabled. Default for production builds.</summary>
    None,

    /// <summary>Emit verbose diagnostic traces (internal state, JS interop round-trips).</summary>
    Debug,

    /// <summary>Informational lifecycle events (initialization, theme change).</summary>
    Info,

    /// <summary>Recoverable problems (deprecated parameter, fallback applied).</summary>
    Warning,

    /// <summary>Errors that prevent expected behaviour.</summary>
    Error
}
