namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// <c>sf.color.text.*</c> on-surface text tokens per ADR 0077 §5.2.
/// </summary>
public static class TextColors
{
    /// <summary>Primary text — body copy + headings.</summary>
    public static readonly ColorToken Primary   = new(Light: "#0a0a0a", Dark: "#fafafa");

    /// <summary>Secondary text — captions + labels.</summary>
    public static readonly ColorToken Secondary = new(Light: "#404040", Dark: "#a3a3a3");

    /// <summary>Tertiary text — disabled / placeholder.</summary>
    public static readonly ColorToken Tertiary  = new(Light: "#737373", Dark: "#737373");
}
