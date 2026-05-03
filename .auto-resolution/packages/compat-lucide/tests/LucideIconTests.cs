using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sunfish.Compat.Lucide;
using Xunit;

namespace Sunfish.Compat.Lucide.Tests;

/// <summary>
/// Reflection-based tests that exercise the public compat surface without spinning up a
/// full bUnit render cycle. We validate (a) public type shape, (b) the
/// <see cref="LucideIconName"/> starter-set contract, and (c) the kebab-case slug map
/// exposed by <see cref="LucideIconNameExtensions.ToSlug"/>.
/// </summary>
public class LucideIconTests
{
    // ----------------------------------------------------------------------
    // 1. Public-surface locks: type must be public, in root namespace,
    //    and inherit from ComponentBase.
    // ----------------------------------------------------------------------

    [Fact]
    public void LucideIcon_TypesInNamespace()
    {
        var iconType = typeof(LucideIcon);
        Assert.True(iconType.IsPublic);
        Assert.Equal("Sunfish.Compat.Lucide", iconType.Namespace);
        Assert.True(typeof(ComponentBase).IsAssignableFrom(iconType));

        var enumType = typeof(LucideIconName);
        Assert.True(enumType.IsPublic);
        Assert.Equal("Sunfish.Compat.Lucide", enumType.Namespace);
        Assert.True(enumType.IsEnum);

        var extType = typeof(LucideIconNameExtensions);
        Assert.True(extType.IsPublic);
        Assert.Equal("Sunfish.Compat.Lucide", extType.Namespace);
        Assert.True(extType.IsAbstract && extType.IsSealed, "LucideIconNameExtensions should be a static class.");
    }

    // ----------------------------------------------------------------------
    // 2. Starter-set contract: at least 50 enum values.
    // ----------------------------------------------------------------------

    [Fact]
    public void LucideIconName_Has50_StarterValues()
    {
        int count = Enum.GetValues(typeof(LucideIconName)).Length;
        Assert.True(count >= 50, $"LucideIconName has {count} members; expected at least 50.");
    }

    // ----------------------------------------------------------------------
    // 3. Slug map: every enum value must map to a non-empty, non-whitespace
    //    kebab-case slug. This guards against a new LucideIconName entry landing
    //    without a corresponding ToSlug arm.
    // ----------------------------------------------------------------------

    [Fact]
    public void LucideIconName_Extensions_HandlesEveryValue()
    {
        foreach (LucideIconName value in Enum.GetValues(typeof(LucideIconName)))
        {
            string slug = value.ToSlug();
            Assert.False(string.IsNullOrWhiteSpace(slug),
                $"LucideIconName.{value} produced an empty slug.");
            // Sanity: Lucide slugs are lowercase ASCII kebab-case with optional
            // trailing digits (e.g. trash-2, share-2).
            foreach (char c in slug)
            {
                Assert.True(
                    (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-',
                    $"LucideIconName.{value} slug '{slug}' contains non-kebab-case character '{c}'.");
            }
            Assert.DoesNotContain("--", slug);
            Assert.False(slug.StartsWith('-'), $"Slug '{slug}' must not start with '-'.");
            Assert.False(slug.EndsWith('-'), $"Slug '{slug}' must not end with '-'.");
        }
    }

    [Fact]
    public void LucideIconName_Home_SlugIsHome()
    {
        // Spot-check a trivial mapping to lock the ToSlug contract.
        Assert.Equal("home", LucideIconName.Home.ToSlug());
    }

    [Fact]
    public void LucideIconName_X_SlugIsX()
    {
        // Single-letter slug — guard against a naive case transform adding stray
        // hyphens.
        Assert.Equal("x", LucideIconName.X.ToSlug());
    }

    [Fact]
    public void LucideIconName_ChevronUp_SlugIsKebabCase()
    {
        // Spot-check a multi-word mapping — guards against a naive
        // PascalCase→kebab transform diverging from Lucide's canonical name.
        Assert.Equal("chevron-up", LucideIconName.ChevronUp.ToSlug());
    }

    [Fact]
    public void LucideIconName_AlertTriangle_SlugIsKebabCase()
    {
        Assert.Equal("alert-triangle", LucideIconName.AlertTriangle.ToSlug());
    }

    [Fact]
    public void LucideIconName_Trash2_SlugPreservesNumericSuffix()
    {
        // Lucide preserves numeric-suffix variants (trash-2, share-2) as part of
        // the slug — a naive transform that drops digits would silently break.
        Assert.Equal("trash-2", LucideIconName.Trash2.ToSlug());
    }

    [Fact]
    public void LucideIconName_Share2_SlugPreservesNumericSuffix()
    {
        Assert.Equal("share-2", LucideIconName.Share2.ToSlug());
    }

    [Fact]
    public void LucideIconName_ArrowDownUp_SlugIsKebabCase()
    {
        // Three-word PascalCase slug — sort-icon common case.
        Assert.Equal("arrow-down-up", LucideIconName.ArrowDownUp.ToSlug());
    }

    // ----------------------------------------------------------------------
    // 4. Parameter surface: LucideIcon must expose the Lucide-wrapper-shaped
    //    parameters (Name / NameString / Size / AriaLabel).
    // ----------------------------------------------------------------------

    [Fact]
    public void LucideIcon_HasName_Parameter()
    {
        var type = typeof(LucideIcon);
        var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(nameProp);
        Assert.NotNull(nameProp!.GetCustomAttribute<ParameterAttribute>());
        // Nullable enum — confirm the underlying type is LucideIconName.
        Assert.Equal(typeof(LucideIconName?), nameProp.PropertyType);
    }

    [Fact]
    public void LucideIcon_HasNameString_Parameter()
    {
        var type = typeof(LucideIcon);
        var prop = type.GetProperty("NameString", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetCustomAttribute<ParameterAttribute>());
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void LucideIcon_HasAriaLabel_Parameter()
    {
        var type = typeof(LucideIcon);
        var prop = type.GetProperty("AriaLabel", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetCustomAttribute<ParameterAttribute>());
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void LucideIcon_ExposesExpectedParameters()
    {
        var type = typeof(LucideIcon);
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
    public void LucideIcon_CapturesUnmatchedAttributes()
    {
        var type = typeof(LucideIcon);
        var splat = type.GetProperty("AdditionalAttributes", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(splat);
        var attr = splat!.GetCustomAttribute<ParameterAttribute>();
        Assert.NotNull(attr);
        Assert.True(attr!.CaptureUnmatchedValues,
            "LucideIcon should splat unmatched attributes (class, style, data-*).");
    }

    [Fact]
    public void LucideIconName_HasNoDuplicateSlugs()
    {
        // Two enum members mapping to the same slug would mask a typo on one of them —
        // lock the starter set so any additions stay unique.
        var values = Enum.GetValues(typeof(LucideIconName)).Cast<LucideIconName>().ToList();
        var slugs = values.Select(v => v.ToSlug()).ToList();
        var duplicates = slugs.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }
}
