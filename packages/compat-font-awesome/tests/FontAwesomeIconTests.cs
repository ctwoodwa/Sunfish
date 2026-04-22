using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Compat.FontAwesome;
using Sunfish.Foundation.Enums;
using Xunit;

namespace Sunfish.Compat.FontAwesome.Tests;

/// <summary>
/// Reflection-based tests that exercise the public compat surface without spinning up a
/// full bUnit render cycle. The design-system-specific CSS provider contract
/// (<c>ISunfishCssProvider</c>) is very large, and rendering the underlying
/// <c>SunfishIcon</c> requires a faithful stub — which is out of scope for a compat
/// package's unit tests. We instead validate (a) public type shape, (b) parameter
/// mapping via direct method invocation, and (c) error-path behavior via
/// <see cref="ComponentBase.SetParametersAsync(ParameterView)"/>.
/// </summary>
public class FontAwesomeIconTests
{
    // ----------------------------------------------------------------------
    // 1. Public-surface locks: type must be public, in root namespace,
    //    and inherit from ComponentBase.
    // ----------------------------------------------------------------------

    [Fact]
    public void FontAwesomeIcon_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(FontAwesomeIcon);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.FontAwesome", type.Namespace);
        Assert.True(typeof(ComponentBase).IsAssignableFrom(type));
    }

    [Fact]
    public void FontAwesomeIcon_ExposesExpectedFaParameters()
    {
        var type = typeof(FontAwesomeIcon);
        var paramNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .ToList();

        // Core parameters FA consumers rely on must all be present (intake §11 / mapping doc).
        Assert.Contains("Icon", paramNames);
        Assert.Contains("Size", paramNames);
        Assert.Contains("AriaLabel", paramNames);
        Assert.Contains("FixedWidth", paramNames);
        Assert.Contains("ListItem", paramNames);
        Assert.Contains("Pull", paramNames);
        Assert.Contains("Border", paramNames);
        Assert.Contains("Rotation", paramNames);
        Assert.Contains("Flip", paramNames);
        Assert.Contains("Spin", paramNames);
        Assert.Contains("Pulse", paramNames);
        Assert.Contains("Transform", paramNames);
    }

    // ----------------------------------------------------------------------
    // 2. Size-mapping path — invoke MapSize directly to verify translation.
    // ----------------------------------------------------------------------

    private static IconSize InvokeMapSize(FontAwesomeIcon icon, string? s)
    {
        var method = typeof(FontAwesomeIcon).GetMethod("MapSize",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        try
        {
            return (IconSize)method.Invoke(icon, new object?[] { s })!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            throw tie.InnerException;
        }
    }

    private static FontAwesomeIcon NewIcon()
    {
        var icon = new FontAwesomeIcon();
        // Inject a NullLogger so LogAndFallback paths don't NRE on the Logger member.
        // Razor's @inject compiles to an [Inject] property in newer SDKs and to a field
        // in older ones — set both if present.
        var type = typeof(FontAwesomeIcon);
        var loggerProp = type.GetProperty("Logger",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        loggerProp?.SetValue(icon, NullLogger<FontAwesomeIcon>.Instance);
        var loggerField = type.GetField("Logger",
            BindingFlags.NonPublic | BindingFlags.Instance);
        loggerField?.SetValue(icon, NullLogger<FontAwesomeIcon>.Instance);
        return icon;
    }

    [Theory]
    [InlineData(null, IconSize.Medium)]
    [InlineData("", IconSize.Medium)]
    [InlineData("xs", IconSize.Small)]
    [InlineData("sm", IconSize.Small)]
    [InlineData("1x", IconSize.Small)]
    [InlineData("md", IconSize.Medium)]
    [InlineData("2x", IconSize.Medium)]
    [InlineData("lg", IconSize.Large)]
    [InlineData("3x", IconSize.Large)]
    [InlineData("xl", IconSize.ExtraLarge)]
    [InlineData("2xl", IconSize.ExtraLarge)]
    [InlineData("4x", IconSize.ExtraLarge)]
    [InlineData("5x", IconSize.ExtraLarge)]
    [InlineData("6x", IconSize.ExtraLarge)]
    public void FontAwesomeIcon_MapsFontAwesomeSizes(string? faSize, IconSize expected)
    {
        var icon = NewIcon();
        Assert.Equal(expected, InvokeMapSize(icon, faSize));
    }

    [Theory]
    [InlineData("7x")]
    [InlineData("8x")]
    [InlineData("9x")]
    [InlineData("10x")]
    public void FontAwesomeIcon_UnsupportedSize_Throws(string faSize)
    {
        var icon = NewIcon();
        var ex = Assert.Throws<NotSupportedException>(() => InvokeMapSize(icon, faSize));
        Assert.Contains("compat-font-awesome-mapping", ex.Message);
        Assert.Contains(faSize, ex.Message);
    }

    [Fact]
    public void FontAwesomeIcon_UnrecognizedSize_LogAndFallsBackToMedium()
    {
        var icon = NewIcon();
        // 'gigantic' is not a valid FA size; should fall back silently to Medium.
        Assert.Equal(IconSize.Medium, InvokeMapSize(icon, "gigantic"));
    }

    // ----------------------------------------------------------------------
    // 3. Typed-icon classes: values, counts, and pass-through identity.
    // ----------------------------------------------------------------------

    [Fact]
    public void FasIcons_ContainsExpectedCoreIcons()
    {
        Assert.Equal("star", FasIcons.Star);
        Assert.Equal("heart", FasIcons.Heart);
        Assert.Equal("home", FasIcons.Home);
        Assert.Equal("user", FasIcons.User);
        Assert.Equal("search", FasIcons.Search);
    }

    [Fact]
    public void FarIcons_ContainsExpectedRegularIcons()
    {
        Assert.Equal("bell", FarIcons.Bell);
        Assert.Equal("calendar", FarIcons.Calendar);
        Assert.Equal("envelope", FarIcons.Envelope);
    }

    [Fact]
    public void FabIcons_ContainsExpectedBrandIcons()
    {
        Assert.Equal("github", FabIcons.Github);
        Assert.Equal("twitter", FabIcons.Twitter);
        Assert.Equal("google", FabIcons.Google);
    }

    [Fact]
    public void TypedIconClasses_ShipStarterSetOfAtLeast50PerFamily()
    {
        // Lock the Phase-1 starter-set floor so an accidental removal is caught.
        int fasCount = typeof(FasIcons).GetFields(BindingFlags.Public | BindingFlags.Static).Length;
        int farCount = typeof(FarIcons).GetFields(BindingFlags.Public | BindingFlags.Static).Length;
        int fabCount = typeof(FabIcons).GetFields(BindingFlags.Public | BindingFlags.Static).Length;
        Assert.True(fasCount >= 50, $"FasIcons has {fasCount}; expected at least 50.");
        Assert.True(farCount >= 50, $"FarIcons has {farCount}; expected at least 50.");
        Assert.True(fabCount >= 50, $"FabIcons has {fabCount}; expected at least 50.");
    }

    // ----------------------------------------------------------------------
    // 4. FaList / FaLayers family: public types exist and inherit ComponentBase.
    // ----------------------------------------------------------------------

    [Fact]
    public void FaList_FaListItem_FaLayers_AreAllPublicComponents()
    {
        foreach (var t in new[] {
            typeof(FaList), typeof(FaListItem),
            typeof(FaLayers), typeof(FaLayersText), typeof(FaLayersCounter) })
        {
            Assert.True(t.IsPublic, $"{t.Name} should be public.");
            Assert.Equal("Sunfish.Compat.FontAwesome", t.Namespace);
            Assert.True(typeof(ComponentBase).IsAssignableFrom(t),
                $"{t.Name} should inherit ComponentBase.");
        }
    }

    [Fact]
    public void FaListItem_DeclaresCascadingFaListParent()
    {
        var parentProp = typeof(FaListItem).GetProperty("Parent");
        Assert.NotNull(parentProp);
        Assert.Equal(typeof(FaList), parentProp!.PropertyType);
        Assert.NotNull(parentProp.GetCustomAttribute<CascadingParameterAttribute>());
    }
}
