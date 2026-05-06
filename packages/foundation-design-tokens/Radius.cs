namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// <c>sf.radius.*</c> tokens per ADR 0077 §5.2.
/// </summary>
public static class Radius
{
    /// <summary>0px — sharp corners.</summary>
    public const string None = "0px";

    /// <summary>2px — subtle rounding.</summary>
    public const string Sm   = "2px";

    /// <summary>4px — standard rounding.</summary>
    public const string Md   = "4px";

    /// <summary>8px — soft rounding.</summary>
    public const string Lg   = "8px";

    /// <summary>9999px — pill / circle.</summary>
    public const string Full = "9999px";
}
