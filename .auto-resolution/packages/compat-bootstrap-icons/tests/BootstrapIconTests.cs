using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sunfish.Compat.BootstrapIcons;
using Xunit;

namespace Sunfish.Compat.BootstrapIcons.Tests;

/// <summary>
/// Reflection-based tests that exercise the public compat surface without spinning up a
/// full bUnit render cycle. We validate (a) public type shape, (b) the
/// <see cref="IconName"/> starter-set contract, and (c) the kebab-case slug map exposed
/// by <see cref="IconNameExtensions.ToSlug"/>.
/// </summary>
public class BootstrapIconTests
{
    // ----------------------------------------------------------------------
    // 1. Public-surface locks: type must be public, in root namespace,
    //    and inherit from ComponentBase.
    // ----------------------------------------------------------------------

    [Fact]
    public void BootstrapIcon_TypesInNamespace()
    {
        var iconType = typeof(BootstrapIcon);
        Assert.True(iconType.IsPublic);
        Assert.Equal("Sunfish.Compat.BootstrapIcons", iconType.Namespace);
        Assert.True(typeof(ComponentBase).IsAssignableFrom(iconType));

        var enumType = typeof(IconName);
        Assert.True(enumType.IsPublic);
        Assert.Equal("Sunfish.Compat.BootstrapIcons", enumType.Namespace);
        Assert.True(enumType.IsEnum);

        var extType = typeof(IconNameExtensions);
        Assert.True(extType.IsPublic);
        Assert.Equal("Sunfish.Compat.BootstrapIcons", extType.Namespace);
        Assert.True(extType.IsAbstract && extType.IsSealed, "IconNameExtensions should be a static class.");
    }

    // ----------------------------------------------------------------------
    // 2. Starter-set contract: at least 50 enum values.
    // ----------------------------------------------------------------------

    [Fact]
    public void IconName_Has50_StarterValues()
    {
        int count = Enum.GetValues(typeof(IconName)).Length;
        Assert.True(count >= 50, $"IconName has {count} members; expected at least 50.");
    }

    // ----------------------------------------------------------------------
    // 3. Slug map: every enum value must map to a non-empty, non-whitespace
    //    kebab-case slug. This guards against a new IconName entry landing
    //    without a corresponding ToSlug arm.
    // ----------------------------------------------------------------------

    [Fact]
    public void IconName_Extensions_HandlesEveryValue()
    {
        foreach (IconName value in Enum.GetValues(typeof(IconName)))
        {
            string slug = value.ToSlug();
            Assert.False(string.IsNullOrWhiteSpace(slug),
                $"IconName.{value} produced an empty slug.");
            // Sanity: Bootstrap Icons slugs are lowercase ASCII kebab-case.
            foreach (char c in slug)
            {
                Assert.True(
                    (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-',
                    $"IconName.{value} slug '{slug}' contains non-kebab-case character '{c}'.");
            }
            Assert.DoesNotContain("--", slug);
            Assert.False(slug.StartsWith('-'), $"Slug '{slug}' must not start with '-'.");
            Assert.False(slug.EndsWith('-'), $"Slug '{slug}' must not end with '-'.");
        }
    }

    [Fact]
    public void IconName_House_SlugIsHouse()
    {
        // Spot-check a trivial mapping to lock the ToSlug contract.
        Assert.Equal("house", IconName.House.ToSlug());
    }

    [Fact]
    public void IconName_ArrowUp_SlugIsKebabCase()
    {
        // Spot-check a multi-word mapping — guards against a naive
        // PascalCase→kebab transform diverging from Bootstrap Icons' canonical name.
        Assert.Equal("arrow-up", IconName.ArrowUp.ToSlug());
    }

    [Fact]
    public void IconName_ExclamationTriangle_SlugIsKebabCase()
    {
        Assert.Equal("exclamation-triangle", IconName.ExclamationTriangle.ToSlug());
    }

    // ----------------------------------------------------------------------
    // 4. Parameter surface: BootstrapIcon must expose the BlazorBootstrap-shaped
    //    parameters (Name / NameString / Size / AriaLabel).
    // ----------------------------------------------------------------------

    [Fact]
    public void BootstrapIcon_HasName_Parameter()
    {
        var type = typeof(BootstrapIcon);
        var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(nameProp);
        Assert.NotNull(nameProp!.GetCustomAttribute<ParameterAttribute>());
        // Nullable enum — confirm the underlying type is IconName.
        Assert.Equal(typeof(IconName?), nameProp.PropertyType);
    }

    [Fact]
    public void BootstrapIcon_ExposesExpectedParameters()
    {
        var type = typeof(BootstrapIcon);
        var paramNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("Name", paramNames);
        Assert.Contains("NameString", paramNames);
        Assert.Contains("Size", paramNames);
        Assert.Contains("AriaLabel", paramNames);
    }

    [Fact]
    public void BootstrapIcon_CapturesUnmatchedAttributes()
    {
        var type = typeof(BootstrapIcon);
        var splat = type.GetProperty("AdditionalAttributes", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(splat);
        var attr = splat!.GetCustomAttribute<ParameterAttribute>();
        Assert.NotNull(attr);
        Assert.True(attr!.CaptureUnmatchedValues,
            "BootstrapIcon should splat unmatched attributes (class, style, data-*).");
    }

    [Fact]
    public void IconName_HasNoDuplicateSlugs()
    {
        // Two enum members mapping to the same slug would mask a typo on one of them —
        // lock the starter set so any additions stay unique.
        var values = Enum.GetValues(typeof(IconName)).Cast<IconName>().ToList();
        var slugs = values.Select(v => v.ToSlug()).ToList();
        var duplicates = slugs.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }
}
