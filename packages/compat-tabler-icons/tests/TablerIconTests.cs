using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sunfish.Compat.TablerIcons;
using Xunit;

namespace Sunfish.Compat.TablerIcons.Tests;

/// <summary>
/// Reflection-based tests that exercise the public compat surface without spinning up a
/// full bUnit render cycle. We validate (a) public type shape, (b) the
/// <see cref="TablerIconName"/> starter-set contract, and (c) the kebab-case slug map
/// exposed by <see cref="TablerIconNameExtensions.ToSlug"/>.
/// </summary>
public class TablerIconTests
{
    // ----------------------------------------------------------------------
    // 1. Public-surface locks: type must be public, in root namespace,
    //    and inherit from ComponentBase.
    // ----------------------------------------------------------------------

    [Fact]
    public void TablerIcon_TypesInNamespace()
    {
        var iconType = typeof(TablerIcon);
        Assert.True(iconType.IsPublic);
        Assert.Equal("Sunfish.Compat.TablerIcons", iconType.Namespace);
        Assert.True(typeof(ComponentBase).IsAssignableFrom(iconType));

        var enumType = typeof(TablerIconName);
        Assert.True(enumType.IsPublic);
        Assert.Equal("Sunfish.Compat.TablerIcons", enumType.Namespace);
        Assert.True(enumType.IsEnum);

        var extType = typeof(TablerIconNameExtensions);
        Assert.True(extType.IsPublic);
        Assert.Equal("Sunfish.Compat.TablerIcons", extType.Namespace);
        Assert.True(extType.IsAbstract && extType.IsSealed, "TablerIconNameExtensions should be a static class.");
    }

    // ----------------------------------------------------------------------
    // 2. Starter-set contract: at least 50 enum values.
    // ----------------------------------------------------------------------

    [Fact]
    public void TablerIconName_Has50_StarterValues()
    {
        int count = Enum.GetValues(typeof(TablerIconName)).Length;
        Assert.True(count >= 50, $"TablerIconName has {count} members; expected at least 50.");
    }

    // ----------------------------------------------------------------------
    // 3. Slug map: every enum value must map to a non-empty, non-whitespace
    //    kebab-case slug. This guards against a new TablerIconName entry landing
    //    without a corresponding ToSlug arm.
    // ----------------------------------------------------------------------

    [Fact]
    public void TablerIconName_Extensions_HandlesEveryValue()
    {
        foreach (TablerIconName value in Enum.GetValues(typeof(TablerIconName)))
        {
            string slug = value.ToSlug();
            Assert.False(string.IsNullOrWhiteSpace(slug),
                $"TablerIconName.{value} produced an empty slug.");
            // Sanity: Tabler slugs are lowercase ASCII kebab-case with optional
            // trailing digits (e.g. menu-2).
            foreach (char c in slug)
            {
                Assert.True(
                    (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-',
                    $"TablerIconName.{value} slug '{slug}' contains non-kebab-case character '{c}'.");
            }
            Assert.DoesNotContain("--", slug);
            Assert.False(slug.StartsWith('-'), $"Slug '{slug}' must not start with '-'.");
            Assert.False(slug.EndsWith('-'), $"Slug '{slug}' must not end with '-'.");
        }
    }

    [Fact]
    public void TablerIconName_Home_SlugIsHome()
    {
        // Spot-check a trivial mapping to lock the ToSlug contract.
        Assert.Equal("home", TablerIconName.Home.ToSlug());
    }

    [Fact]
    public void TablerIconName_X_SlugIsX()
    {
        // Single-letter slug — guard against a naive case transform adding stray
        // hyphens.
        Assert.Equal("x", TablerIconName.X.ToSlug());
    }

    [Fact]
    public void TablerIconName_Menu2_SlugPreservesNumericSuffix()
    {
        // Tabler preserves numeric-suffix variants (menu-2) as part of the slug —
        // a naive transform that drops digits would silently break.
        Assert.Equal("menu-2", TablerIconName.Menu2.ToSlug());
    }

    [Fact]
    public void TablerIconName_InfoCircle_SlugIsKebabCase()
    {
        // Spot-check a multi-word mapping — guards against a naive
        // PascalCase→kebab transform diverging from Tabler's canonical name.
        Assert.Equal("info-circle", TablerIconName.InfoCircle.ToSlug());
    }

    [Fact]
    public void TablerIconName_DeviceFloppy_SlugIsKebabCase()
    {
        // Tabler's "save" icon is named `device-floppy` upstream — a common
        // migrator-surprise compared to Lucide's `save`.
        Assert.Equal("device-floppy", TablerIconName.DeviceFloppy.ToSlug());
    }

    [Fact]
    public void TablerIconName_PlayerPlay_SlugIsKebabCase()
    {
        // Tabler's media-control family is prefixed `player-*`.
        Assert.Equal("player-play", TablerIconName.PlayerPlay.ToSlug());
    }

    [Fact]
    public void TablerIconName_CircleCheck_SlugIsKebabCase()
    {
        // Tabler's "status success" icon is `circle-check` (not Lucide's
        // `check-circle`) — the prefix/suffix order is vendor-specific.
        Assert.Equal("circle-check", TablerIconName.CircleCheck.ToSlug());
    }

    [Fact]
    public void TablerIconName_ArrowsSort_SlugIsKebabCase()
    {
        // Plural `arrows-*` — Tabler uses the plural form for combined-arrow glyphs.
        Assert.Equal("arrows-sort", TablerIconName.ArrowsSort.ToSlug());
    }

    // ----------------------------------------------------------------------
    // 4. Parameter surface: TablerIcon must expose the Tabler-wrapper-shaped
    //    parameters (Name / NameString / Size / AriaLabel / Stroke).
    // ----------------------------------------------------------------------

    [Fact]
    public void TablerIcon_HasName_Parameter()
    {
        var type = typeof(TablerIcon);
        var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(nameProp);
        Assert.NotNull(nameProp!.GetCustomAttribute<ParameterAttribute>());
        // Nullable enum — confirm the underlying type is TablerIconName.
        Assert.Equal(typeof(TablerIconName?), nameProp.PropertyType);
    }

    [Fact]
    public void TablerIcon_HasNameString_Parameter()
    {
        var type = typeof(TablerIcon);
        var prop = type.GetProperty("NameString", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetCustomAttribute<ParameterAttribute>());
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void TablerIcon_HasAriaLabel_Parameter()
    {
        var type = typeof(TablerIcon);
        var prop = type.GetProperty("AriaLabel", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetCustomAttribute<ParameterAttribute>());
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void TablerIcon_HasStroke_Parameter()
    {
        // Tabler's distinguishing parameter — lock the `Stroke` (nullable double)
        // shape so it stays as Tabler's stroke-width control (not int, not string).
        var type = typeof(TablerIcon);
        var prop = type.GetProperty("Stroke", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetCustomAttribute<ParameterAttribute>());
        Assert.Equal(typeof(double?), prop.PropertyType);
    }

    [Fact]
    public void TablerIcon_ExposesExpectedParameters()
    {
        var type = typeof(TablerIcon);
        var paramNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("Name", paramNames);
        Assert.Contains("NameString", paramNames);
        Assert.Contains("Size", paramNames);
        Assert.Contains("AriaLabel", paramNames);
        Assert.Contains("Stroke", paramNames);
    }

    [Fact]
    public void TablerIcon_CapturesUnmatchedAttributes()
    {
        var type = typeof(TablerIcon);
        var splat = type.GetProperty("AdditionalAttributes", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(splat);
        var attr = splat!.GetCustomAttribute<ParameterAttribute>();
        Assert.NotNull(attr);
        Assert.True(attr!.CaptureUnmatchedValues,
            "TablerIcon should splat unmatched attributes (class, style, data-*).");
    }

    [Fact]
    public void TablerIconName_HasNoDuplicateSlugs()
    {
        // Two enum members mapping to the same slug would mask a typo on one of them —
        // lock the starter set so any additions stay unique.
        var values = Enum.GetValues(typeof(TablerIconName)).Cast<TablerIconName>().ToList();
        var slugs = values.Select(v => v.ToSlug()).ToList();
        var duplicates = slugs.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }
}
