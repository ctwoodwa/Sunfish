using System.Collections.Generic;
using System.Linq;
using Sunfish.Tooling.ColorAudit;

namespace Sunfish.Tooling.DesignTokensCodegen;

/// <summary>
/// CVD ΔE2000 pairwise audit for role-band hues.
/// Minimum ΔE2000 ≥ <see cref="DefaultThreshold"/> under each CVD mode (per ADR 0036 precedent).
/// </summary>
public static class CvdAuditor
{
    public const double DefaultThreshold = 11.0;

    public sealed class AuditRow
    {
        public required string HueA { get; init; }
        public required string HueB { get; init; }
        public required string HexA { get; init; }
        public required string HexB { get; init; }
        public required double NormalDeltaE { get; init; }
        public required double ProDeltaE { get; init; }
        public required double DeuterDeltaE { get; init; }
        public required double TriDeltaE { get; init; }
        public required double MinDeltaE { get; init; }
        public required bool Pass { get; init; }
    }

    /// <summary>
    /// Compute pairwise ΔE2000 for all role-band color pairs under all CVD modes.
    /// Audits both light and dark variants; reports the worst-case (lowest) ΔE2000 across both.
    /// </summary>
    public static IReadOnlyList<AuditRow> Audit(
        IReadOnlyList<ColorEntry> roleBand,
        double threshold = DefaultThreshold)
    {
        var rows = new List<AuditRow>();
        var bands = roleBand.ToArray();

        for (int i = 0; i < bands.Length; i++)
        for (int j = i + 1; j < bands.Length; j++)
        {
            var a = bands[i];
            var b = bands[j];

            // Light variant
            double normalL = CvdSimulation.DeltaE2000Under(a.Light, b.Light, CvdMode.None);
            double proL    = CvdSimulation.DeltaE2000Under(a.Light, b.Light, CvdMode.Protanopia);
            double deuterL = CvdSimulation.DeltaE2000Under(a.Light, b.Light, CvdMode.Deuteranopia);
            double triL    = CvdSimulation.DeltaE2000Under(a.Light, b.Light, CvdMode.Tritanopia);

            // Dark variant
            double normalD = CvdSimulation.DeltaE2000Under(a.Dark, b.Dark, CvdMode.None);
            double proD    = CvdSimulation.DeltaE2000Under(a.Dark, b.Dark, CvdMode.Protanopia);
            double deuterD = CvdSimulation.DeltaE2000Under(a.Dark, b.Dark, CvdMode.Deuteranopia);
            double triD    = CvdSimulation.DeltaE2000Under(a.Dark, b.Dark, CvdMode.Tritanopia);

            // Worst-case (minimum) across both variants
            double normal = System.Math.Min(normalL, normalD);
            double pro    = System.Math.Min(proL, proD);
            double deuter = System.Math.Min(deuterL, deuterD);
            double tri    = System.Math.Min(triL, triD);
            double min    = System.Math.Min(System.Math.Min(normal, pro), System.Math.Min(deuter, tri));

            rows.Add(new AuditRow
            {
                HueA = a.CssVar,
                HueB = b.CssVar,
                HexA = a.Light,
                HexB = b.Light,
                NormalDeltaE = normal,
                ProDeltaE = pro,
                DeuterDeltaE = deuter,
                TriDeltaE = tri,
                MinDeltaE = min,
                Pass = min >= threshold,
            });
        }
        return rows;
    }

    public static double MinDeltaE2000(IReadOnlyList<string> hexValues, CvdMode mode)
    {
        double min = double.MaxValue;
        var arr = hexValues.ToArray();
        for (int i = 0; i < arr.Length; i++)
        for (int j = i + 1; j < arr.Length; j++)
        {
            double de = CvdSimulation.DeltaE2000Under(arr[i], arr[j], mode);
            if (de < min) min = de;
        }
        return min;
    }
}
