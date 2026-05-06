namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// <c>sf.color.state.*</c> tokens per ADR 0077 §5.2 — extends ADR 0036
/// SyncState palette with focus-ring + 4 generic state hues.
/// </summary>
public static class StateColors
{
    /// <summary>Success state (e.g., healthy sync).</summary>
    public static readonly ColorToken Success   = new(Light: "#16a34a", Dark: "#22c55e");

    /// <summary>Warning state (e.g., stale sync, deferred operation).</summary>
    public static readonly ColorToken Warning   = new(Light: "#ca8a04", Dark: "#eab308");

    /// <summary>Error state (e.g., failed operation, conflict).</summary>
    public static readonly ColorToken Error     = new(Light: "#dc2626", Dark: "#ef4444");

    /// <summary>Info state (e.g., neutral notification).</summary>
    public static readonly ColorToken Info      = new(Light: "#2563eb", Dark: "#3b82f6");

    /// <summary>Focus-ring color per WCAG SC 2.4.7 + 2.4.11.</summary>
    public static readonly ColorToken FocusRing = new(Light: "#2563eb", Dark: "#60a5fa");
}
