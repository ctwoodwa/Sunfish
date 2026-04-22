using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sunfish.Compat.MaterialIcons;
using Sunfish.Foundation.Enums;
using Xunit;

namespace Sunfish.Compat.MaterialIcons.Tests;

/// <summary>
/// Reflection-based tests that exercise the public compat surface without spinning up
/// a full bUnit render cycle. We validate (a) public type shape, (b) parameter
/// presence, and (c) the internal class-attribute mapping that backs the emitted
/// markup.
/// </summary>
public class MaterialIconTests
{
    // ----------------------------------------------------------------------
    // 1. Public-surface locks: types must be public, in root namespace, and
    //    inherit from ComponentBase where applicable.
    // ----------------------------------------------------------------------

    [Fact]
    public void MaterialIcon_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(MaterialIcon);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.MaterialIcons", type.Namespace);
        Assert.True(typeof(ComponentBase).IsAssignableFrom(type));
    }

    [Fact]
    public void MaterialSymbol_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(MaterialSymbol);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.MaterialIcons", type.Namespace);
        Assert.True(typeof(ComponentBase).IsAssignableFrom(type));
    }

    [Fact]
    public void MaterialIcon_ExposesNameSizeAriaLabelAndSplat()
    {
        var type = typeof(MaterialIcon);
        var paramNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("Name", paramNames);
        Assert.Contains("Size", paramNames);
        Assert.Contains("AriaLabel", paramNames);
        Assert.Contains("AdditionalAttributes", paramNames);

        // Splat parameter must have CaptureUnmatchedValues = true.
        var splat = type.GetProperty("AdditionalAttributes")!;
        var attr = splat.GetCustomAttribute<ParameterAttribute>()!;
        Assert.True(attr.CaptureUnmatchedValues);
    }

    [Fact]
    public void MaterialSymbol_ExposesNameVariantSizeAriaLabelAndSplat()
    {
        var type = typeof(MaterialSymbol);
        var paramNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("Name", paramNames);
        Assert.Contains("Variant", paramNames);
        Assert.Contains("Size", paramNames);
        Assert.Contains("AriaLabel", paramNames);
        Assert.Contains("AdditionalAttributes", paramNames);
    }

    // ----------------------------------------------------------------------
    // 2. MaterialSymbolVariant enum — three required members.
    // ----------------------------------------------------------------------

    [Fact]
    public void MaterialSymbolVariant_HasOutlinedRoundedSharp()
    {
        var type = typeof(MaterialSymbolVariant);
        Assert.True(type.IsEnum);
        Assert.Equal("Sunfish.Compat.MaterialIcons", type.Namespace);
        var names = Enum.GetNames(type).ToHashSet();
        Assert.Contains("Outlined", names);
        Assert.Contains("Rounded", names);
        Assert.Contains("Sharp", names);

        // Outlined must be the default (value 0) so unparameterized MaterialSymbol
        // defaults to Outlined — matches Google's default variant.
        Assert.Equal(default(MaterialSymbolVariant), MaterialSymbolVariant.Outlined);
    }

    // ----------------------------------------------------------------------
    // 3. MaterialIconName — 50-constant starter-set.
    // ----------------------------------------------------------------------

    [Fact]
    public void MaterialIconName_ShipsStarterSetOfAtLeast50Constants()
    {
        // Constants materialize as static literal fields; count those.
        int count = typeof(MaterialIconName)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Count(f => f.IsLiteral);
        Assert.True(count >= 50, $"MaterialIconName has {count}; expected at least 50.");
    }

    [Fact]
    public void MaterialIconName_ContainsCoreNames()
    {
        Assert.Equal("home", MaterialIconName.Home);
        Assert.Equal("search", MaterialIconName.Search);
        Assert.Equal("settings", MaterialIconName.Settings);
        Assert.Equal("calendar_today", MaterialIconName.CalendarToday);
        Assert.Equal("arrow_back", MaterialIconName.ArrowBack);
        Assert.Equal("expand_more", MaterialIconName.ExpandMore);
        Assert.Equal("content_copy", MaterialIconName.ContentCopy);
        Assert.Equal("visibility_off", MaterialIconName.VisibilityOff);
    }

    // ----------------------------------------------------------------------
    // 4. Internal class-attribute mapping (drives the emitted markup).
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null, "")]
    [InlineData(IconSize.Small, "sf-material-size-sm")]
    [InlineData(IconSize.Medium, "sf-material-size-md")]
    [InlineData(IconSize.Large, "sf-material-size-lg")]
    [InlineData(IconSize.ExtraLarge, "sf-material-size-xl")]
    public void MaterialSizeClass_ToClass_MapsIconSize(IconSize? size, string expected)
    {
        var method = typeof(MaterialIcon).Assembly
            .GetType("Sunfish.Compat.MaterialIcons.MaterialSizeClass", throwOnError: true)!
            .GetMethod("ToClass", BindingFlags.Public | BindingFlags.Static)!;
        var actual = (string)method.Invoke(null, new object?[] { size })!;
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(MaterialSymbolVariant.Outlined, "material-symbols-outlined")]
    [InlineData(MaterialSymbolVariant.Rounded, "material-symbols-rounded")]
    [InlineData(MaterialSymbolVariant.Sharp, "material-symbols-sharp")]
    public void MaterialSizeClass_ToSymbolClass_MapsVariant(MaterialSymbolVariant variant, string expected)
    {
        var method = typeof(MaterialSymbol).Assembly
            .GetType("Sunfish.Compat.MaterialIcons.MaterialSizeClass", throwOnError: true)!
            .GetMethod("ToSymbolClass", BindingFlags.Public | BindingFlags.Static)!;
        var actual = (string)method.Invoke(null, new object?[] { variant })!;
        Assert.Equal(expected, actual);
    }
}
