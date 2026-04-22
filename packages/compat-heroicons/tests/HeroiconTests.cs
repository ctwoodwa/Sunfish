using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sunfish.Compat.Heroicons;
using Xunit;

namespace Sunfish.Compat.Heroicons.Tests;

/// <summary>
/// Reflection-based tests that exercise the public compat surface without spinning up
/// a full bUnit render cycle. We validate (a) public type shape, (b) the
/// <see cref="HeroiconVariant"/> and <see cref="HeroiconName"/> contracts, and
/// (c) the kebab-case slug map exposed by <see cref="HeroiconNameExtensions.ToSlug"/>.
/// </summary>
public class HeroiconTests
{
    // ----------------------------------------------------------------------
    // 1. Public-surface locks: types must be public, in root namespace,
    //    and inherit from ComponentBase where applicable.
    // ----------------------------------------------------------------------

    [Fact]
    public void Heroicon_TypesInNamespace()
    {
        var iconType = typeof(Heroicon);
        Assert.True(iconType.IsPublic);
        Assert.Equal("Sunfish.Compat.Heroicons", iconType.Namespace);
        Assert.True(typeof(ComponentBase).IsAssignableFrom(iconType));

        var variantType = typeof(HeroiconVariant);
        Assert.True(variantType.IsPublic);
        Assert.Equal("Sunfish.Compat.Heroicons", variantType.Namespace);
        Assert.True(variantType.IsEnum);

        var enumType = typeof(HeroiconName);
        Assert.True(enumType.IsPublic);
        Assert.Equal("Sunfish.Compat.Heroicons", enumType.Namespace);
        Assert.True(enumType.IsEnum);

        var extType = typeof(HeroiconNameExtensions);
        Assert.True(extType.IsPublic);
        Assert.Equal("Sunfish.Compat.Heroicons", extType.Namespace);
        Assert.True(extType.IsAbstract && extType.IsSealed, "HeroiconNameExtensions should be a static class.");
    }

    // ----------------------------------------------------------------------
    // 2. HeroiconVariant enum — three required members with Outline as default.
    // ----------------------------------------------------------------------

    [Fact]
    public void HeroiconVariant_HasOutlineSolidMiniWithOutlineDefault()
    {
        var type = typeof(HeroiconVariant);
        var names = Enum.GetNames(type).ToHashSet();
        Assert.Contains("Outline", names);
        Assert.Contains("Solid", names);
        Assert.Contains("Mini", names);

        // Outline must be value 0 so an unparameterized <Heroicon /> defaults to
        // Outline — matches Heroicons' own documented default.
        Assert.Equal(default(HeroiconVariant), HeroiconVariant.Outline);
    }

    // ----------------------------------------------------------------------
    // 3. HeroiconName — 50-member starter-set.
    // ----------------------------------------------------------------------

    [Fact]
    public void HeroiconName_Has50_StarterValues()
    {
        int count = Enum.GetValues(typeof(HeroiconName)).Length;
        Assert.True(count >= 50, $"HeroiconName has {count} members; expected at least 50.");
    }

    // ----------------------------------------------------------------------
    // 4. Slug map: every enum value must map to a non-empty, non-whitespace
    //    kebab-case slug. This guards against a new HeroiconName entry landing
    //    without a corresponding ToSlug arm.
    // ----------------------------------------------------------------------

    [Fact]
    public void HeroiconName_Extensions_HandlesEveryValue()
    {
        foreach (HeroiconName value in Enum.GetValues(typeof(HeroiconName)))
        {
            string slug = value.ToSlug();
            Assert.False(string.IsNullOrWhiteSpace(slug),
                $"HeroiconName.{value} produced an empty slug.");
            // Sanity: Heroicons slugs are lowercase ASCII kebab-case (digits allowed for
            // names like "cog-6-tooth" and "squares-2x2").
            foreach (char c in slug)
            {
                Assert.True(
                    (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-',
                    $"HeroiconName.{value} slug '{slug}' contains non-kebab-case character '{c}'.");
            }
            Assert.DoesNotContain("--", slug);
            Assert.False(slug.StartsWith('-'), $"Slug '{slug}' must not start with '-'.");
            Assert.False(slug.EndsWith('-'), $"Slug '{slug}' must not end with '-'.");
        }
    }

    [Fact]
    public void HeroiconName_MagnifyingGlass_SlugIsKebabCase()
    {
        // Spot-check a multi-word mapping — guards against a naive
        // PascalCase→kebab transform diverging from Heroicons' canonical name.
        Assert.Equal("magnifying-glass", HeroiconName.MagnifyingGlass.ToSlug());
    }

    [Fact]
    public void HeroiconName_Cog6Tooth_SlugIsKebabCaseWithDigit()
    {
        // Spot-check a PascalCase-with-digit mapping — Heroicons embeds numeric
        // tokens mid-name (cog-6-tooth, bars-3) that must survive the ToSlug
        // transform.
        Assert.Equal("cog-6-tooth", HeroiconName.Cog6Tooth.ToSlug());
    }

    [Fact]
    public void HeroiconName_Squares2x2_PreservesCompoundNumericSegment()
    {
        // Spot-check a compound numeric segment (squares-2x2) — the "2x2" token
        // must not be split into "2-x-2" by the mapping.
        Assert.Equal("squares-2x2", HeroiconName.Squares2x2.ToSlug());
    }

    [Fact]
    public void HeroiconName_HasNoDuplicateSlugs()
    {
        // Two enum members mapping to the same slug would mask a typo on one of
        // them — lock the starter set so any additions stay unique.
        var values = Enum.GetValues(typeof(HeroiconName)).Cast<HeroiconName>().ToList();
        var slugs = values.Select(v => v.ToSlug()).ToList();
        var duplicates = slugs.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void HeroiconVariant_ToSlug_MapsAllVariants()
    {
        Assert.Equal("outline", HeroiconVariant.Outline.ToSlug());
        Assert.Equal("solid", HeroiconVariant.Solid.ToSlug());
        Assert.Equal("mini", HeroiconVariant.Mini.ToSlug());
    }

    // ----------------------------------------------------------------------
    // 5. Parameter surface: Heroicon must expose the Blazor.Heroicons-shaped
    //    parameters (Name / NameString / Variant / Size / AriaLabel / splat).
    // ----------------------------------------------------------------------

    [Fact]
    public void Heroicon_HasName_Parameter()
    {
        var type = typeof(Heroicon);
        var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(nameProp);
        Assert.NotNull(nameProp!.GetCustomAttribute<ParameterAttribute>());
        // Nullable enum — confirm the underlying type is HeroiconName.
        Assert.Equal(typeof(HeroiconName?), nameProp.PropertyType);
    }

    [Fact]
    public void Heroicon_ExposesExpectedParameters()
    {
        var type = typeof(Heroicon);
        var paramNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("Name", paramNames);
        Assert.Contains("NameString", paramNames);
        Assert.Contains("Variant", paramNames);
        Assert.Contains("Size", paramNames);
        Assert.Contains("AriaLabel", paramNames);
        Assert.Contains("AdditionalAttributes", paramNames);
    }

    [Fact]
    public void Heroicon_CapturesUnmatchedAttributes()
    {
        var type = typeof(Heroicon);
        var splat = type.GetProperty("AdditionalAttributes", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(splat);
        var attr = splat!.GetCustomAttribute<ParameterAttribute>();
        Assert.NotNull(attr);
        Assert.True(attr!.CaptureUnmatchedValues,
            "Heroicon should splat unmatched attributes (class, style, data-*).");
    }
}
