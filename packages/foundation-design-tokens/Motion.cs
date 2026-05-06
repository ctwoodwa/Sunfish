namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// <c>sf.motion.*</c> tokens per ADR 0077 §5.2. Includes
/// <c>reduced-motion-fallback</c> values per WCAG SC 2.3.3.
/// </summary>
public static class Motion
{
    /// <summary>Animation durations (CSS time units).</summary>
    public static class Duration
    {
        /// <summary>100ms — micro-interactions.</summary>
        public const string Fast    = "100ms";

        /// <summary>200ms — standard transitions.</summary>
        public const string Normal  = "200ms";

        /// <summary>300ms — major state changes.</summary>
        public const string Slow    = "300ms";

        /// <summary>500ms — page-level transitions.</summary>
        public const string Slower  = "500ms";
    }

    /// <summary>Easing curves (CSS cubic-bezier).</summary>
    public static class Easing
    {
        /// <summary>Linear — no acceleration.</summary>
        public const string Linear   = "cubic-bezier(0, 0, 1, 1)";

        /// <summary>Ease-in — accelerate.</summary>
        public const string In       = "cubic-bezier(0.4, 0, 1, 1)";

        /// <summary>Ease-out — decelerate.</summary>
        public const string Out      = "cubic-bezier(0, 0, 0.2, 1)";

        /// <summary>Ease-in-out — accelerate then decelerate.</summary>
        public const string InOut    = "cubic-bezier(0.4, 0, 0.2, 1)";

        /// <summary>Material standard curve.</summary>
        public const string Standard = "cubic-bezier(0.2, 0, 0, 1)";
    }

    /// <summary>
    /// Reduced-motion-fallback duration per WCAG SC 2.3.3 — when
    /// <c>prefers-reduced-motion: reduce</c> is active, all motion
    /// tokens fall back to this zero-duration value via the
    /// <c>tokens.css</c> <c>@media</c> block.
    /// </summary>
    public const string ReducedMotionFallbackDuration = "0ms";
}
