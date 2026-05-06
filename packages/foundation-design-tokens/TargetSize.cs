namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// <c>sf.target-size.*</c> tokens per ADR 0077 §5.2.
/// </summary>
/// <remarks>
/// Per WCAG SC 2.5.8 (web 24×24 px) + Apple HIG (iOS 44pt) +
/// Material Design (Android 48dp). Phase 2b's CI accessibility-audit
/// gate will assert all interactive controls meet at least the
/// platform-canonical minimum at runtime.
/// </remarks>
public static class TargetSize
{
    /// <summary>24px — minimum web target per WCAG SC 2.5.8.</summary>
    public const string MinWeb     = "24px";

    /// <summary>44pt — minimum iOS tap target per Apple HIG.</summary>
    public const string MinIos     = "44pt";

    /// <summary>48dp — minimum Android tap target per Material Design.</summary>
    public const string MinAndroid = "48dp";
}
