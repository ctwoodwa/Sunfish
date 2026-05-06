namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// <c>sf.space.*</c> 4-px-grid spacing scale per ADR 0077 §5.2. Each
/// constant is the CSS px value as a string, suitable for direct
/// embedding in inline styles or Razor binding.
/// </summary>
public static class Space
{
    /// <summary>0px — no spacing.</summary>
    public const string Step0  = "0px";

    /// <summary>4px — single grid unit.</summary>
    public const string Step1  = "4px";

    /// <summary>8px — minimum tap target padding.</summary>
    public const string Step2  = "8px";

    /// <summary>12px — control inner padding.</summary>
    public const string Step3  = "12px";

    /// <summary>16px — base spacing.</summary>
    public const string Step4  = "16px";

    /// <summary>24px — section spacing.</summary>
    public const string Step6  = "24px";

    /// <summary>32px — block spacing.</summary>
    public const string Step8  = "32px";

    /// <summary>48px — major section gap.</summary>
    public const string Step12 = "48px";

    /// <summary>64px — page section gap.</summary>
    public const string Step16 = "64px";

    /// <summary>96px — large layout gap.</summary>
    public const string Step24 = "96px";

    /// <summary>128px — extra-large layout gap.</summary>
    public const string Step32 = "128px";

    /// <summary>192px — page-level gap.</summary>
    public const string Step48 = "192px";

    /// <summary>256px — landing-page gap.</summary>
    public const string Step64 = "256px";
}
