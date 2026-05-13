using System;
using System.Collections.Generic;
using Sunfish.Tooling.ColorAudit;

namespace Sunfish.Tooling.DesignTokensCodegen;

/// <summary>
/// WCAG 1.4.3 / 1.4.11 contrast ratio verification.
/// Normal text: ≥4.5:1. Large text / non-text: ≥3:1.
/// </summary>
public static class ContrastVerifier
{
    /// <summary>
    /// Relative luminance of a sRGB hex color, per WCAG 2.x formula.
    /// </summary>
    public static double RelativeLuminance(string srgbHex)
    {
        var rgb = LinearRgb.FromSrgbHex(srgbHex);
        return 0.2126 * rgb.R + 0.7152 * rgb.G + 0.0722 * rgb.B;
    }

    /// <summary>
    /// WCAG contrast ratio between two colors (always ≥1).
    /// </summary>
    public static double ContrastRatio(string foregroundHex, string backgroundHex)
    {
        double l1 = RelativeLuminance(foregroundHex);
        double l2 = RelativeLuminance(backgroundHex);
        double lighter = Math.Max(l1, l2);
        double darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    public sealed class Violation
    {
        public required string TextVar { get; init; }
        public required string SurfaceVar { get; init; }
        public required string Mode { get; init; }          // "light" or "dark"
        public required double Ratio { get; init; }
        public required double Required { get; init; }
    }

    /// <summary>
    /// Verify all text × surface contrast pairs against WCAG thresholds.
    /// Returns the list of violations (empty = pass).
    /// </summary>
    public static IReadOnlyList<Violation> Verify(IReadOnlyList<TextSurfacePair> pairs)
    {
        var violations = new List<Violation>();
        foreach (var pair in pairs)
        {
            double required = pair.RequireEnhanced ? 4.5 : 3.0;

            double lightRatio = ContrastRatio(pair.Text.Light, pair.Surface.Light);
            if (lightRatio < required)
            {
                violations.Add(new Violation
                {
                    TextVar = pair.Text.CssVar,
                    SurfaceVar = pair.Surface.CssVar,
                    Mode = "light",
                    Ratio = lightRatio,
                    Required = required,
                });
            }

            double darkRatio = ContrastRatio(pair.Text.Dark, pair.Surface.Dark);
            if (darkRatio < required)
            {
                violations.Add(new Violation
                {
                    TextVar = pair.Text.CssVar,
                    SurfaceVar = pair.Surface.CssVar,
                    Mode = "dark",
                    Ratio = darkRatio,
                    Required = required,
                });
            }
        }
        return violations;
    }
}
