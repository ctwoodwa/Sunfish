using System.IO;
using System.Reflection;
using System.Text.Json;
using Sunfish.Foundation.DesignTokens;
using Xunit;

namespace Sunfish.Foundation.DesignTokens.Tests;

public class TokenJsonTests
{
    [Fact]
    public void TokensJson_EmbeddedResource_IsLoadable()
    {
        var asm = typeof(SurfaceColors).Assembly;
        using var stream = asm.GetManifestResourceStream(
            "Sunfish.Foundation.DesignTokens.tokens.json");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var json = reader.ReadToEnd();
        Assert.False(string.IsNullOrEmpty(json));
        // tokens.json must round-trip through System.Text.Json.
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("sf", out _));
    }

    [Fact]
    public void TokensJson_HasAllTopLevelGroups()
    {
        var asm = typeof(SurfaceColors).Assembly;
        using var stream = asm.GetManifestResourceStream(
            "Sunfish.Foundation.DesignTokens.tokens.json");
        Assert.NotNull(stream);
        using var doc = JsonDocument.Parse(stream!);
        var sf = doc.RootElement.GetProperty("sf");
        // Every top-level token group per ADR 0077 §5.2.
        Assert.True(sf.TryGetProperty("color", out _));
        Assert.True(sf.TryGetProperty("typography", out _));
        Assert.True(sf.TryGetProperty("space", out _));
        Assert.True(sf.TryGetProperty("radius", out _));
        Assert.True(sf.TryGetProperty("elevation", out _));
        Assert.True(sf.TryGetProperty("motion", out _));
        Assert.True(sf.TryGetProperty("target-size", out _));
    }
}

public class CardinalityTests
{
    [Fact]
    public void RoleBandColors_HasExactly7Hues()
    {
        // Per ADR 0077 §5.2: 7 role-band hues
        // (captain/xo/department-head/division-officer/idc/scribe/watch).
        var fields = typeof(RoleBandColors).GetFields(
            BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(7, fields.Length);
    }

    [Fact]
    public void SurfaceColors_HasThreeTiers()
    {
        var fields = typeof(SurfaceColors).GetFields(
            BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(3, fields.Length);
    }

    [Fact]
    public void TextColors_HasThreeTiers()
    {
        var fields = typeof(TextColors).GetFields(
            BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(3, fields.Length);
    }

    [Fact]
    public void StateColors_HasFiveValues()
    {
        // 4 state hues + focus-ring per ADR 0077 §5.2.
        var fields = typeof(StateColors).GetFields(
            BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(5, fields.Length);
    }

    [Fact]
    public void Space_HasThirteenSteps()
    {
        // 4px-grid scale: 0, 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64.
        var fields = typeof(Space).GetFields(
            BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(13, fields.Length);
    }

    [Fact]
    public void Radius_HasFiveValues()
    {
        var fields = typeof(Radius).GetFields(
            BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(5, fields.Length);
    }

    [Fact]
    public void Elevation_HasSixValues()
    {
        var fields = typeof(Elevation).GetFields(
            BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(6, fields.Length);
    }
}

public class TargetSizeTests
{
    [Fact]
    public void MinWeb_Is24Px_PerWcag258()
    {
        Assert.Equal("24px", TargetSize.MinWeb);
    }

    [Fact]
    public void MinIos_Is44Pt_PerAppleHig()
    {
        Assert.Equal("44pt", TargetSize.MinIos);
    }

    [Fact]
    public void MinAndroid_Is48Dp_PerMaterialDesign()
    {
        Assert.Equal("48dp", TargetSize.MinAndroid);
    }
}

public class HexFormatTests
{
    [Theory]
    [InlineData("#ffffff")]
    [InlineData("#0a0a0a")]
    [InlineData("#7c3aed")]
    public void ColorToken_Light_IsLowercaseSevenCharHex(string value)
    {
        Assert.Equal(7, value.Length);
        Assert.Equal('#', value[0]);
        Assert.True(System.Linq.Enumerable.All(value.Substring(1), c =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
    }

    [Fact]
    public void SurfaceColors_AllValues_AreLowercaseSevenCharHex()
    {
        AssertAllHex(typeof(SurfaceColors));
    }

    [Fact]
    public void TextColors_AllValues_AreLowercaseSevenCharHex()
    {
        AssertAllHex(typeof(TextColors));
    }

    [Fact]
    public void StateColors_AllValues_AreLowercaseSevenCharHex()
    {
        AssertAllHex(typeof(StateColors));
    }

    [Fact]
    public void RoleBandColors_AllValues_AreLowercaseSevenCharHex()
    {
        AssertAllHex(typeof(RoleBandColors));
    }

    private static void AssertAllHex(System.Type t)
    {
        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var f in fields)
        {
            var token = (ColorToken)f.GetValue(null)!;
            Assert.Matches("^#[0-9a-f]{6}$", token.Light);
            Assert.Matches("^#[0-9a-f]{6}$", token.Dark);
        }
    }
}

public class TypographyTests
{
    [Fact]
    public void Typography_FontSize_RangeIsXsToXl4()
    {
        Assert.Equal("12px", Typography.Size.Xs);
        Assert.Equal("14px", Typography.Size.Sm);
        Assert.Equal("16px", Typography.Size.Md);
        Assert.Equal("18px", Typography.Size.Lg);
        Assert.Equal("20px", Typography.Size.Xl);
        Assert.Equal("24px", Typography.Size.Xl2);
        Assert.Equal("30px", Typography.Size.Xl3);
        Assert.Equal("36px", Typography.Size.Xl4);
    }

    [Fact]
    public void Typography_FontWeight_HasFourValues()
    {
        Assert.Equal(400, Typography.Weight.Regular);
        Assert.Equal(500, Typography.Weight.Medium);
        Assert.Equal(600, Typography.Weight.Semibold);
        Assert.Equal(700, Typography.Weight.Bold);
    }
}

public class MotionTests
{
    [Fact]
    public void ReducedMotionFallback_IsZeroMs_PerWcagSc233()
    {
        Assert.Equal("0ms", Motion.ReducedMotionFallbackDuration);
    }

    [Fact]
    public void Motion_HasFiveEasingCurves()
    {
        // linear / in / out / in-out / standard
        var fields = typeof(Motion.Easing).GetFields(
            BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(5, fields.Length);
    }

    [Fact]
    public void Motion_HasFourDurationValues()
    {
        var fields = typeof(Motion.Duration).GetFields(
            BindingFlags.Public | BindingFlags.Static);
        Assert.Equal(4, fields.Length);
    }
}

public class TokensCssTests
{
    private static string ReadEmbeddedTokensCss()
    {
        var asm = typeof(SurfaceColors).Assembly;
        using var stream = asm.GetManifestResourceStream(
            "Sunfish.Foundation.DesignTokens.tokens.css");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    [Fact]
    public void TokensCss_EmbeddedResource_IsLoadable()
    {
        var css = ReadEmbeddedTokensCss();
        Assert.False(string.IsNullOrEmpty(css));
    }

    [Fact]
    public void TokensCss_ContainsSurfacePrimaryLightCustomProperty()
    {
        var css = ReadEmbeddedTokensCss();
        Assert.Contains("--sf-color-surface-primary:", css);
    }

    [Fact]
    public void TokensCss_HasReducedMotionMediaBlock()
    {
        var css = ReadEmbeddedTokensCss();
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
    }

    [Fact]
    public void TokensCss_HasForcedColorsMediaBlock()
    {
        var css = ReadEmbeddedTokensCss();
        Assert.Contains("@media (forced-colors: active)", css);
    }

    [Fact]
    public void TokensCss_HasReducedTransparencyMediaBlock()
    {
        var css = ReadEmbeddedTokensCss();
        Assert.Contains("@media (prefers-reduced-transparency: reduce)", css);
    }
}
