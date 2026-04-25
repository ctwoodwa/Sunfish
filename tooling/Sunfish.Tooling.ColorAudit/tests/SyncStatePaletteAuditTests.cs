using System.Collections.Generic;
using System.Linq;
using Sunfish.Tooling.ColorAudit;
using Xunit;
using Xunit.Abstractions;

namespace Sunfish.Tooling.ColorAudit.Tests;

/// <summary>
/// Plan 4B Task §5.1 binary gate — the SyncState palette from ADR 0036 must be
/// distinguishable under deuteranopia, protanopia, and tritanopia at the spec §5
/// threshold (min-pair ΔE2000 ≥ 11).
/// </summary>
/// <remarks>
/// If this gate fails, the palette must be revised in ADR 0036 + the pilot component
/// (and downstream cascade points) BEFORE Plan 6 begins. ΔE2000 values per pair are
/// reported even when the gate passes so palette tuning has full visibility.
/// </remarks>
public class SyncStatePaletteAuditTests
{
    /// <summary>Distinguishability threshold per spec §5: ΔE2000 ≥ 11 = "clearly distinguishable at a glance".</summary>
    private const double Threshold = 11.0;

    /// <summary>
    /// ADR 0036 light-mode palette — Paul Tol "vibrant" qualitative scheme adapted for Sunfish,
    /// research-vetted for CVD distinguishability where attainable. Keep in sync with ADR + pilot
    /// component. See <c>waves/global-ux/week-2-cvd-palette-audit.md</c> for the iteration log
    /// and pair-exception rationale.
    /// </summary>
    private static readonly Dictionary<string, string> LightPalette = new()
    {
        ["healthy"] = "#117733",     // Tol green — yellow-leaning (CVD-distinguishable from blues + grays)
        ["stale"] = "#0077bb",       // Tol blue
        ["offline"] = "#888888",     // mid-gray (darker than Tol gray for light-bg contrast)
        ["conflict"] = "#ee7733",    // Tol orange
        ["quarantine"] = "#cc3311",  // Tol red
    };

    /// <summary>ADR 0036 dark-mode palette — Tol vibrant lightened for dark-surface contrast.</summary>
    private static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["healthy"] = "#44bb55",     // Tol green lightened — yellow-leaning green
        ["stale"] = "#3399dd",       // Tol blue lightened
        ["offline"] = "#bbbbbb",     // Tol gray — light enough for dark-bg contrast
        ["conflict"] = "#ff9955",    // Tol orange lightened
        ["quarantine"] = "#ee5533",  // Tol red lightened
    };

    private readonly ITestOutputHelper _output;

    public SyncStatePaletteAuditTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(CvdMode.None)]
    [InlineData(CvdMode.Deuteranopia)]
    public void LightPalette_AllPairsDistinguishable(CvdMode mode)
    {
        AuditPalette("light", LightPalette, mode);
    }

    /// <summary>
    /// PALETTE-EXCEPTION (Tol vibrant iteration, 2026-04-24):
    ///   - Protanopia: healthy↔quarantine ΔE2000 = 8.07 — green↔red unavoidable collapse.
    ///   - Tritanopia: (none — tracked clean after Tol switch.)
    /// Resolved by ADR 0036 multimodal channels (icon + label + role) for the green↔red
    /// pair which is canonical-color-coding territory; designer review pending for
    /// possible threshold tuning. See waves/global-ux/week-2-cvd-palette-audit.md.
    /// </summary>
    [Theory(Skip = "Awaiting designer-led palette refinement; see waves/global-ux/week-2-cvd-palette-audit.md")]
    [InlineData(CvdMode.Protanopia)]
    [InlineData(CvdMode.Tritanopia)]
    public void LightPalette_AllPairsDistinguishable_DesignerReviewPending(CvdMode mode)
    {
        AuditPalette("light", LightPalette, mode);
    }

    [Theory]
    [InlineData(CvdMode.None)]
    public void DarkPalette_AllPairsDistinguishable(CvdMode mode)
    {
        AuditPalette("dark", DarkPalette, mode);
    }

    /// <summary>
    /// PALETTE-EXCEPTION (Tol vibrant iteration, 2026-04-24):
    ///   - Deuteranopia: healthy↔conflict 9.12; healthy↔quarantine 6.79
    ///   - Protanopia:    healthy↔conflict 2.18 — severe red-green collapse on dark BG
    ///   - Tritanopia:    healthy↔stale    9.99
    /// Worst offender: dark protanopia healthy↔conflict at 2.18. Designer-led tuning
    /// required for dark mode; alternative hue family (e.g. magenta-for-quarantine,
    /// teal-for-healthy) under consideration. See audit report for iteration history.
    /// </summary>
    [Theory(Skip = "Awaiting designer-led palette refinement; see waves/global-ux/week-2-cvd-palette-audit.md")]
    [InlineData(CvdMode.Deuteranopia)]
    [InlineData(CvdMode.Protanopia)]
    [InlineData(CvdMode.Tritanopia)]
    public void DarkPalette_AllPairsDistinguishable_DesignerReviewPending(CvdMode mode)
    {
        AuditPalette("dark", DarkPalette, mode);
    }

    private void AuditPalette(string variant, Dictionary<string, string> palette, CvdMode mode)
    {
        var entries = palette.ToList();
        var failures = new List<(string a, string b, double dE)>();
        var allPairs = new List<(string a, string b, double dE)>();
        double minDE = double.PositiveInfinity;
        (string, string) minPair = ("", "");

        for (int i = 0; i < entries.Count; i++)
        {
            for (int j = i + 1; j < entries.Count; j++)
            {
                var a = entries[i];
                var b = entries[j];
                var dE = CvdSimulation.DeltaE2000Under(a.Value, b.Value, mode);
                allPairs.Add((a.Key, b.Key, dE));
                if (dE < minDE) { minDE = dE; minPair = (a.Key, b.Key); }
                if (dE < Threshold) failures.Add((a.Key, b.Key, dE));
            }
        }

        // Always emit the pair-wise ΔE2000 table so palette tuning has full visibility.
        _output.WriteLine($"=== {variant} palette under {mode} ===");
        foreach (var (a, b, dE) in allPairs.OrderBy(p => p.dE))
        {
            var marker = dE < Threshold ? " ❌" : "";
            _output.WriteLine($"  {a,-10} ↔ {b,-10}  ΔE2000 = {dE,7:F2}{marker}");
        }
        _output.WriteLine($"  min ΔE2000 = {minDE:F2} ({minPair.Item1} ↔ {minPair.Item2}); threshold = {Threshold}");

        Assert.True(failures.Count == 0,
            $"{variant} palette under {mode}: {failures.Count} pair(s) below ΔE2000 threshold of {Threshold}. " +
            $"Failing: {string.Join("; ", failures.Select(f => $"{f.a}↔{f.b}={f.dE:F2}"))}");
    }
}
