using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sunfish.Compat.SimpleIcons;
using Xunit;

namespace Sunfish.Compat.SimpleIcons.Tests;

/// <summary>
/// Reflection-based tests that exercise the public compat surface without spinning up
/// a full bUnit render cycle. Validates (a) public type shape, (b) the
/// <see cref="SimpleIconSlug"/> starter-set contract, and (c) the
/// lowercase-alphanumeric slug map exposed by
/// <see cref="SimpleIconSlugExtensions.ToSlug"/>.
/// </summary>
public class SimpleIconTests
{
    // ----------------------------------------------------------------------
    // 1. Public-surface locks: types must be public, in root namespace,
    //    and SimpleIcon must inherit from ComponentBase.
    // ----------------------------------------------------------------------

    [Fact]
    public void SimpleIcon_TypesInNamespace()
    {
        var iconType = typeof(SimpleIcon);
        Assert.True(iconType.IsPublic);
        Assert.Equal("Sunfish.Compat.SimpleIcons", iconType.Namespace);
        Assert.True(typeof(ComponentBase).IsAssignableFrom(iconType));

        var enumType = typeof(SimpleIconSlug);
        Assert.True(enumType.IsPublic);
        Assert.Equal("Sunfish.Compat.SimpleIcons", enumType.Namespace);
        Assert.True(enumType.IsEnum);

        var extType = typeof(SimpleIconSlugExtensions);
        Assert.True(extType.IsPublic);
        Assert.Equal("Sunfish.Compat.SimpleIcons", extType.Namespace);
        Assert.True(extType.IsAbstract && extType.IsSealed,
            "SimpleIconSlugExtensions should be a static class.");
    }

    // ----------------------------------------------------------------------
    // 2. Starter-set contract: at least 50 enum values.
    // ----------------------------------------------------------------------

    [Fact]
    public void SimpleIconSlug_Has50_StarterValues()
    {
        int count = Enum.GetValues(typeof(SimpleIconSlug)).Length;
        Assert.True(count >= 50,
            $"SimpleIconSlug has {count} members; expected at least 50.");
    }

    // ----------------------------------------------------------------------
    // 3. Slug map: every enum value must map to a non-empty,
    //    lowercase-alphanumeric slug. Simple Icons slugs — unlike Bootstrap
    //    Icons — contain NO hyphens, underscores, or uppercase characters.
    // ----------------------------------------------------------------------

    [Fact]
    public void SimpleIconSlug_Extensions_HandlesEveryValue()
    {
        foreach (SimpleIconSlug value in Enum.GetValues(typeof(SimpleIconSlug)))
        {
            string slug = value.ToSlug();
            Assert.False(string.IsNullOrWhiteSpace(slug),
                $"SimpleIconSlug.{value} produced an empty slug.");

            // Simple Icons slugs are lowercase ASCII alphanumeric — no hyphens,
            // no dots, no underscores, no spaces.
            foreach (char c in slug)
            {
                Assert.True(
                    (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'),
                    $"SimpleIconSlug.{value} slug '{slug}' contains non-alphanumeric "
                    + $"character '{c}' (Simple Icons slugs are lowercase alphanumeric; "
                    + "no hyphens or separators).");
            }
        }
    }

    [Fact]
    public void SimpleIconSlug_Github_SlugIsGithub()
    {
        // Spot-check a trivial mapping to lock the ToSlug contract.
        Assert.Equal("github", SimpleIconSlug.Github.ToSlug());
    }

    [Fact]
    public void SimpleIconSlug_Html5_SlugIsLowercaseWithNoSeparator()
    {
        // Spot-check a brand whose canonical slug merges alphanumerics without a
        // separator — guards against a naive PascalCase→kebab transform diverging
        // from Simple Icons' "flat token" convention.
        Assert.Equal("html5", SimpleIconSlug.Html5.ToSlug());
    }

    [Fact]
    public void SimpleIconSlug_Dotnet_SlugIsDotnet()
    {
        // .NET is normalized to "dotnet" — guards the hand-authored map against
        // a transform that would emit ".net" or "dot-net".
        Assert.Equal("dotnet", SimpleIconSlug.Dotnet.ToSlug());
    }

    [Fact]
    public void SimpleIconSlug_Twitter_MapsToLegacyAlias()
    {
        // Policy decision — Twitter/X legacy alias. See SimpleIconSlugExtensions
        // XML docs. Upstream also publishes "x" for the post-rebrand mark;
        // consumers who want that can pass SlugString="x".
        Assert.Equal("twitter", SimpleIconSlug.Twitter.ToSlug());
    }

    // ----------------------------------------------------------------------
    // 4. Slug uniqueness: no two enum members should map to the same slug.
    //    A duplicate would mask a typo on one side.
    // ----------------------------------------------------------------------

    [Fact]
    public void SimpleIconSlug_HasNoDuplicateSlugs()
    {
        var values = Enum.GetValues(typeof(SimpleIconSlug)).Cast<SimpleIconSlug>().ToList();
        var slugs = values.Select(v => v.ToSlug()).ToList();
        var duplicates = slugs.GroupBy(s => s)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.Empty(duplicates);
    }

    // ----------------------------------------------------------------------
    // 5. Parameter surface: SimpleIcon must expose Slug / SlugString / Color /
    //    Size / AriaLabel plus attribute splat.
    // ----------------------------------------------------------------------

    [Fact]
    public void SimpleIcon_HasSlug_Parameter()
    {
        var type = typeof(SimpleIcon);
        var slugProp = type.GetProperty("Slug", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(slugProp);
        Assert.NotNull(slugProp!.GetCustomAttribute<ParameterAttribute>());
        // Nullable enum — confirm the underlying type is SimpleIconSlug.
        Assert.Equal(typeof(SimpleIconSlug?), slugProp.PropertyType);
    }

    [Fact]
    public void SimpleIcon_ExposesExpectedParameters()
    {
        var type = typeof(SimpleIcon);
        var paramNames = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("Slug", paramNames);
        Assert.Contains("SlugString", paramNames);
        Assert.Contains("Color", paramNames);
        Assert.Contains("Size", paramNames);
        Assert.Contains("AriaLabel", paramNames);
    }

    [Fact]
    public void SimpleIcon_CapturesUnmatchedAttributes()
    {
        var type = typeof(SimpleIcon);
        var splat = type.GetProperty("AdditionalAttributes", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(splat);
        var attr = splat!.GetCustomAttribute<ParameterAttribute>();
        Assert.NotNull(attr);
        Assert.True(attr!.CaptureUnmatchedValues,
            "SimpleIcon should splat unmatched attributes (class, style, data-*).");
    }
}
