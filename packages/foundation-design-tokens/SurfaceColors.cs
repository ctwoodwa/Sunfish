namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// <c>sf.color.surface.*</c> tokens per ADR 0077 §5.2. Each
/// <see cref="ColorToken"/> carries paired light + dark hex values per
/// §5.3 OS-preference tokens. W#46 Phase 2a handcrafted; Phase 2b
/// codegen will replace this file with output from
/// <c>tokens.json → tooling/design-tokens-codegen/</c>.
/// </summary>
public static class SurfaceColors
{
    /// <summary>The primary surface — page background.</summary>
    public static readonly ColorToken Primary   = new(Light: "#ffffff", Dark: "#0a0a0a");

    /// <summary>The secondary surface — card / pane background.</summary>
    public static readonly ColorToken Secondary = new(Light: "#f5f5f5", Dark: "#171717");

    /// <summary>The tertiary surface — recessed / inset background.</summary>
    public static readonly ColorToken Tertiary  = new(Light: "#e5e5e5", Dark: "#262626");
}
