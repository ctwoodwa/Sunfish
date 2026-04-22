using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Compat.FluentIcons;
using Sunfish.Foundation.Enums;
using Xunit;

namespace Sunfish.Compat.FluentIcons.Tests;

/// <summary>
/// Reflection-based tests that exercise the public compat surface without spinning up a
/// full bUnit render cycle. Design-system-specific rendering is validated in the adapter
/// package's own tests; compat-icon tests cover (a) public type shape, (b) Value duck
/// typing, (c) starter-set floor locks, and (d) size extraction mapping.
/// </summary>
public class FluentIconTests
{
    // ----------------------------------------------------------------------
    // 1. Public-surface locks.
    // ----------------------------------------------------------------------

    [Fact]
    public void FluentIcon_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(FluentIcon);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.FluentIcons", type.Namespace);
        Assert.True(typeof(ComponentBase).IsAssignableFrom(type));
    }

    [Fact]
    public void FluentIcon_PublicTypes_InExpectedNamespaces()
    {
        // Flagship wrapper in the root namespace; typed-icon lattice classes in Size20.
        Assert.Equal("Sunfish.Compat.FluentIcons", typeof(FluentIcon).Namespace);
        Assert.Equal("Sunfish.Compat.FluentIcons.Size20", typeof(Size20.Regular).Namespace);
        Assert.Equal("Sunfish.Compat.FluentIcons.Size20", typeof(Size20.Filled).Namespace);
    }

    [Fact]
    public void FluentIcon_AcceptsValue_OfAnyShape()
    {
        var type = typeof(FluentIcon);
        var valueProp = type.GetProperty("Value",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(valueProp);
        // The "object" accepts any typed icon instance, a string, or a RenderFragment.
        Assert.Equal(typeof(object), valueProp!.PropertyType);
        Assert.NotNull(valueProp.GetCustomAttribute<ParameterAttribute>());
    }

    [Fact]
    public void FluentIcon_ExposesExpectedFluentParameters()
    {
        var type = typeof(FluentIcon);
        var paramNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .ToList();

        // Core parameters Fluent consumers rely on must all be present (mapping doc).
        Assert.Contains("Value", paramNames);
        Assert.Contains("Size", paramNames);
        Assert.Contains("AriaLabel", paramNames);
        Assert.Contains("Slot", paramNames);
        Assert.Contains("Title", paramNames);
        Assert.Contains("Color", paramNames);
        Assert.Contains("CustomColor", paramNames);
        Assert.Contains("Width", paramNames);
    }

    [Fact]
    public void FluentIcon_SizeParameter_IsOptionalIconSize()
    {
        var prop = typeof(FluentIcon).GetProperty("Size",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.Equal(typeof(IconSize?), prop!.PropertyType);
    }

    // ----------------------------------------------------------------------
    // 2. Size-mapping helper — Fluent pixel sizes → Sunfish IconSize buckets.
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(10, IconSize.Small)]
    [InlineData(12, IconSize.Small)]
    [InlineData(16, IconSize.Medium)]
    [InlineData(20, IconSize.Medium)]
    [InlineData(24, IconSize.Large)]
    [InlineData(28, IconSize.Large)]
    [InlineData(32, IconSize.ExtraLarge)]
    [InlineData(48, IconSize.ExtraLarge)]
    public void MapFluentSizeToSunfish_BucketsCorrectly(int px, IconSize expected)
    {
        var method = typeof(FluentIcon).GetMethod(
            "MapFluentSizeToSunfish",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var actual = (IconSize)method.Invoke(null, new object[] { px })!;
        Assert.Equal(expected, actual);
    }

    // ----------------------------------------------------------------------
    // 3. Typed-icon starter-set floor locks.
    // ----------------------------------------------------------------------

    [Fact]
    public void Size20_Regular_Has50_StarterIcons()
    {
        int count = typeof(Size20.Regular).GetNestedTypes(BindingFlags.Public).Length;
        Assert.True(count >= 50, $"Size20.Regular has {count} nested icon types; expected at least 50.");
    }

    [Fact]
    public void Size20_Filled_Has50_StarterIcons()
    {
        int count = typeof(Size20.Filled).GetNestedTypes(BindingFlags.Public).Length;
        Assert.True(count >= 50, $"Size20.Filled has {count} nested icon types; expected at least 50.");
    }

    [Fact]
    public void Size20_Regular_Home_HasExpectedName()
    {
        var icon = new Size20.Regular.Home();
        Assert.Equal("home", icon.Name);
    }

    [Fact]
    public void Size20_Filled_Home_HasExpectedName()
    {
        var icon = new Size20.Filled.Home();
        Assert.Equal("home", icon.Name);
    }

    [Fact]
    public void Size20_Regular_CoreIcons_HaveKebabCaseNames()
    {
        // Spot-check the starter set: a few multi-word icons must be kebab-cased.
        Assert.Equal("arrow-up", new Size20.Regular.ArrowUp().Name);
        Assert.Equal("arrow-down", new Size20.Regular.ArrowDown().Name);
        Assert.Equal("chevron-up", new Size20.Regular.ChevronUp().Name);
        Assert.Equal("checkmark-circle", new Size20.Regular.CheckmarkCircle().Name);
        Assert.Equal("error-circle", new Size20.Regular.ErrorCircle().Name);
        Assert.Equal("volume-up", new Size20.Regular.VolumeUp().Name);
        Assert.Equal("eye-off", new Size20.Regular.EyeOff().Name);
    }

    [Fact]
    public void Size20_Filled_MirrorsRegular_StarterSet()
    {
        // Filled and Regular starter sets should carry the same icon catalog — only the
        // variant changes. This guards against accidental drift between the two.
        var regular = typeof(Size20.Regular).GetNestedTypes(BindingFlags.Public)
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToArray();
        var filled = typeof(Size20.Filled).GetNestedTypes(BindingFlags.Public)
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToArray();
        Assert.Equal(regular, filled);
    }

    // ----------------------------------------------------------------------
    // 4. Duck-typed icon-name extraction path (validates the FluentIcon
    //    Value-normalization contract without a full render cycle).
    // ----------------------------------------------------------------------

    private static string? InvokeExtractIconName(object value)
    {
        var method = typeof(FluentIcon).GetMethod(
            "ExtractIconName",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string?)method.Invoke(null, new object[] { value });
    }

    [Fact]
    public void ExtractIconName_ReadsName_FromTypedIcon()
    {
        Assert.Equal("home", InvokeExtractIconName(new Size20.Regular.Home()));
        Assert.Equal("search", InvokeExtractIconName(new Size20.Filled.Search()));
    }

    [Fact]
    public void ExtractSizeFromValueType_InfersSize_FromNamespace()
    {
        var method = typeof(FluentIcon).GetMethod(
            "ExtractSizeFromValueType",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var size = (IconSize?)method.Invoke(null, new object?[] { new Size20.Regular.Home() });
        Assert.Equal(IconSize.Medium, size);
    }
}
