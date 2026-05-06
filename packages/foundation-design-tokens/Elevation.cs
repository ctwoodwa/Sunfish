namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// <c>sf.elevation.*</c> CSS box-shadow tokens per ADR 0077 §5.2.
/// </summary>
public static class Elevation
{
    /// <summary>No shadow.</summary>
    public const string Level0 = "none";

    /// <summary>Subtle hairline shadow.</summary>
    public const string Level1 = "0 1px 2px rgba(0,0,0,0.05)";

    /// <summary>Standard card shadow.</summary>
    public const string Level2 = "0 1px 3px rgba(0,0,0,0.1), 0 1px 2px rgba(0,0,0,0.06)";

    /// <summary>Lifted card shadow.</summary>
    public const string Level3 = "0 4px 6px -1px rgba(0,0,0,0.1), 0 2px 4px -1px rgba(0,0,0,0.06)";

    /// <summary>Modal / dialog shadow.</summary>
    public const string Modal  = "0 20px 25px -5px rgba(0,0,0,0.1), 0 10px 10px -5px rgba(0,0,0,0.04)";

    /// <summary>Drawer / sheet shadow.</summary>
    public const string Drawer = "0 25px 50px -12px rgba(0,0,0,0.25)";
}
