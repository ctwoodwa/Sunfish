namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// <c>sf.typography.*</c> tokens per ADR 0077 §5.2.
/// </summary>
public static class Typography
{
    /// <summary>Font family stacks per platform-canonical form.</summary>
    public static class Family
    {
        /// <summary>System UI sans-serif stack — body copy default.</summary>
        public const string Sans  = "system-ui, -apple-system, \"Segoe UI\", Roboto, sans-serif";

        /// <summary>Serif stack — long-form reading mode.</summary>
        public const string Serif = "Georgia, \"Times New Roman\", serif";

        /// <summary>Monospace stack — code + terminal output.</summary>
        public const string Mono  = "ui-monospace, SFMono-Regular, \"Consolas\", monospace";
    }

    /// <summary>Type-scale font sizes (CSS px).</summary>
    public static class Size
    {
        /// <summary>12px — captions / meta.</summary>
        public const string Xs  = "12px";

        /// <summary>14px — secondary text.</summary>
        public const string Sm  = "14px";

        /// <summary>16px — body copy default.</summary>
        public const string Md  = "16px";

        /// <summary>18px — body lead.</summary>
        public const string Lg  = "18px";

        /// <summary>20px — h6.</summary>
        public const string Xl  = "20px";

        /// <summary>24px — h5.</summary>
        public const string Xl2 = "24px";

        /// <summary>30px — h4.</summary>
        public const string Xl3 = "30px";

        /// <summary>36px — h3.</summary>
        public const string Xl4 = "36px";
    }

    /// <summary>Font weights.</summary>
    public static class Weight
    {
        /// <summary>400 — regular body.</summary>
        public const int Regular  = 400;

        /// <summary>500 — emphasized body.</summary>
        public const int Medium   = 500;

        /// <summary>600 — sub-headings.</summary>
        public const int Semibold = 600;

        /// <summary>700 — headings.</summary>
        public const int Bold     = 700;
    }

    /// <summary>Line-height ratios (unitless).</summary>
    public static class LineHeight
    {
        /// <summary>1.25 — display headings.</summary>
        public const double Tight   = 1.25;

        /// <summary>1.5 — body copy default.</summary>
        public const double Normal  = 1.5;

        /// <summary>1.75 — long-form reading mode.</summary>
        public const double Relaxed = 1.75;
    }
}
