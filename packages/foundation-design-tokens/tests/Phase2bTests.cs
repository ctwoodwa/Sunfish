using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Sunfish.Tooling.ColorAudit;
using Sunfish.Tooling.DesignTokensCodegen;
using Xunit;

namespace Sunfish.Foundation.DesignTokens.Tests;

/// <summary>
/// Phase 2b: codegen pipeline + WCAG contrast gate + CVD ΔE2000 audit.
/// </summary>
public class Phase2bContrastVerifierTests
{
    [Fact]
    public void ContrastVerifier_HighContrastPair_ReturnsPass()
    {
        double ratio = ContrastVerifier.ContrastRatio("#000000", "#ffffff");
        Assert.True(ratio >= 4.5, $"Expected ≥4.5:1; got {ratio:F2}:1");
    }

    [Fact]
    public void ContrastVerifier_LowContrastPair_FailsEnhancedThreshold()
    {
        // #777777 on #ffffff ≈ 4.48:1 — fails normal-text (≥4.5:1) but passes large-text (≥3:1).
        // This is the deliberate build-failure signal for the contrast gate.
        double ratio = ContrastVerifier.ContrastRatio("#777777", "#ffffff");
        Assert.True(ratio < 4.5,
            $"Expected this pair to fail enhanced contrast (≥4.5:1); ratio was {ratio:F2}:1");
        Assert.True(ratio >= 3.0,
            $"Expected this pair to pass large-text threshold (≥3.0:1); ratio was {ratio:F2}:1");
    }

    [Fact]
    public void ContrastVerifier_AllCurrentTokenPairs_PassGate()
    {
        // text.primary + text.secondary require ≥4.5:1; text.tertiary requires ≥3:1 (large text).
        var catalog = TokensReader.Read(P2bHelpers.EmbeddedJson());
        var violations = ContrastVerifier.Verify(catalog.ContrastPairs);
        Assert.Empty(violations);
    }
}

public class Phase2bCvdAuditorTests
{
    [Fact]
    public void CvdAuditor_IdenticalHues_FailsThreshold()
    {
        // Identical hues → ΔE2000 = 0 → deliberate CVD audit failure signal.
        double minDe = CvdAuditor.MinDeltaE2000(["#7c3aed", "#7c3aed"], CvdMode.Deuteranopia);
        Assert.True(minDe < CvdAuditor.DefaultThreshold,
            $"Expected identical hues to fail CVD gate; minDE={minDe:F2}");
    }

    [Fact]
    public void CvdAuditor_MaximallyDistinctHues_PassesThreshold()
    {
        double minDe = CvdAuditor.MinDeltaE2000(["#000000", "#ffffff"], CvdMode.Deuteranopia);
        Assert.True(minDe >= CvdAuditor.DefaultThreshold,
            $"Expected black/white to pass CVD gate; minDE={minDe:F2}");
    }
}

public class Phase2bGeneratedCssTests
{
    [Fact]
    public void GeneratedCss_ContainsSurfacePrimaryLightVariant()
    {
        // Gate: --sf-color-surface-primary-light must exist (Phase 2b explicit-variant scheme).
        Assert.Contains("--sf-color-surface-primary-light:", P2bHelpers.GeneratedCss());
    }

    [Fact]
    public void GeneratedCss_HasForcedColorsMediaBlock()
    {
        Assert.Contains("@media (forced-colors: active)", P2bHelpers.GeneratedCss());
    }

    [Fact]
    public void GeneratedCss_HasReducedMotionMediaBlock()
    {
        Assert.Contains("@media (prefers-reduced-motion: reduce)", P2bHelpers.GeneratedCss());
    }

    [Fact]
    public void CommittedTokensCss_MatchesGeneratorOutput()
    {
        // Ensures the committed packages/ui-core/src/tokens.css is in sync with
        // what the codegen tool would produce from the current tokens.json.
        // Prevents drift when tokens.json is edited without re-running BeforeBuild.
        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var root = Path.GetFullPath(Path.Combine(asmDir, "../../../../../.."));
        var committed = Path.Combine(root, "packages", "ui-core", "src", "tokens.css");

        if (!File.Exists(committed))
        {
            Assert.Fail(
                $"Committed tokens.css not found at '{committed}'. "
                + "Run 'dotnet build packages/foundation-design-tokens/' to generate it, "
                + "then commit the result.");
            return;
        }

        var expected = CssGenerator.Generate(TokensReader.Read(P2bHelpers.EmbeddedJson()));
        var actual   = File.ReadAllText(committed);
        Assert.Equal(expected, actual);
    }
}

public class Phase2bTokenJsonTests
{
    [Fact]
    public void TokensJson_TargetSizeMinWeb_Is24px_PerWcagSc258()
    {
        using var doc = JsonDocument.Parse(P2bHelpers.EmbeddedJson());
        var val = doc.RootElement
            .GetProperty("sf")
            .GetProperty("target-size")
            .GetProperty("min-web")
            .GetProperty("$value")
            .GetString();
        Assert.Equal("24px", val);
    }

    [Fact]
    public void TokensJson_RoleBand_HasExactlySevenHues()
    {
        using var doc = JsonDocument.Parse(P2bHelpers.EmbeddedJson());
        var roleBand = doc.RootElement
            .GetProperty("sf").GetProperty("color").GetProperty("role-band");
        int count = 0;
        foreach (var p in roleBand.EnumerateObject())
            if (!p.Name.StartsWith("$")) count++;
        Assert.Equal(7, count);
    }
}

internal static class P2bHelpers
{
    internal static string EmbeddedJson()
    {
        var asm = typeof(SurfaceColors).Assembly;
        using var stream = asm.GetManifestResourceStream(
            "Sunfish.Foundation.DesignTokens.tokens.json")
            ?? throw new InvalidOperationException("tokens.json embedded resource not found");
        return new System.IO.StreamReader(stream).ReadToEnd();
    }

    internal static string GeneratedCss()
    {
        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        // Walk up from bin/[config]/net11.0 to the repo root.
        var root = Path.GetFullPath(Path.Combine(asmDir, "../../../../../.."));
        var path = Path.Combine(root, "packages", "ui-core", "src", "tokens.css");
        if (File.Exists(path))
            return File.ReadAllText(path);

        // In-memory fallback: generate from embedded JSON so structural tests pass
        // even on a clean checkout. CommittedTokensCss_MatchesGeneratorOutput enforces
        // that the committed file exists and matches — don't remove that test.
        return CssGenerator.Generate(TokensReader.Read(EmbeddedJson()));
    }
}
