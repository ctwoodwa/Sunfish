using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sunfish.Compat.Octicons;
using Xunit;

namespace Sunfish.Compat.Octicons.Tests;

/// <summary>
/// Reflection-based tests that exercise the public compat surface without spinning up a
/// full bUnit render cycle. We validate (a) public type shape, (b) the
/// <see cref="OcticonName"/> starter-set contract, and (c) the kebab-case slug map
/// exposed by <see cref="OcticonNameExtensions.ToSlug"/>.
/// </summary>
public class OcticonTests
{
    // ----------------------------------------------------------------------
    // 1. Public-surface locks: type must be public, in root namespace,
    //    and inherit from ComponentBase.
    // ----------------------------------------------------------------------

    [Fact]
    public void Octicon_TypesInNamespace()
    {
        var iconType = typeof(Octicon);
        Assert.True(iconType.IsPublic);
        Assert.Equal("Sunfish.Compat.Octicons", iconType.Namespace);
        Assert.True(typeof(ComponentBase).IsAssignableFrom(iconType));

        var enumType = typeof(OcticonName);
        Assert.True(enumType.IsPublic);
        Assert.Equal("Sunfish.Compat.Octicons", enumType.Namespace);
        Assert.True(enumType.IsEnum);

        var extType = typeof(OcticonNameExtensions);
        Assert.True(extType.IsPublic);
        Assert.Equal("Sunfish.Compat.Octicons", extType.Namespace);
        Assert.True(extType.IsAbstract && extType.IsSealed, "OcticonNameExtensions should be a static class.");
    }

    // ----------------------------------------------------------------------
    // 2. Starter-set contract: at least 50 enum values.
    // ----------------------------------------------------------------------

    [Fact]
    public void OcticonName_Has50_StarterValues()
    {
        int count = Enum.GetValues(typeof(OcticonName)).Length;
        Assert.True(count >= 50, $"OcticonName has {count} members; expected at least 50.");
    }

    // ----------------------------------------------------------------------
    // 3. Slug map: every enum value must map to a non-empty, non-whitespace
    //    kebab-case slug. This guards against a new OcticonName entry landing
    //    without a corresponding ToSlug arm.
    // ----------------------------------------------------------------------

    [Fact]
    public void OcticonName_Extensions_HandlesEveryValue()
    {
        foreach (OcticonName value in Enum.GetValues(typeof(OcticonName)))
        {
            string slug = value.ToSlug();
            Assert.False(string.IsNullOrWhiteSpace(slug),
                $"OcticonName.{value} produced an empty slug.");
            // Sanity: Octicons slugs are lowercase ASCII kebab-case.
            foreach (char c in slug)
            {
                Assert.True(
                    (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-',
                    $"OcticonName.{value} slug '{slug}' contains non-kebab-case character '{c}'.");
            }
            Assert.DoesNotContain("--", slug);
            Assert.False(slug.StartsWith('-'), $"Slug '{slug}' must not start with '-'.");
            Assert.False(slug.EndsWith('-'), $"Slug '{slug}' must not end with '-'.");
        }
    }

    [Fact]
    public void OcticonName_Repo_SlugIsRepo()
    {
        // Spot-check a trivial mapping to lock the ToSlug contract.
        Assert.Equal("repo", OcticonName.Repo.ToSlug());
    }

    [Fact]
    public void OcticonName_X_SlugIsX()
    {
        // Single-letter slug — guard against a naive case transform adding stray
        // hyphens.
        Assert.Equal("x", OcticonName.X.ToSlug());
    }

    [Fact]
    public void OcticonName_MarkGithub_SlugIsKebabCase()
    {
        // Spot-check the flagship GitHub-branded slug — guards against a naive
        // PascalCase→kebab transform producing "markgithub" or similar.
        Assert.Equal("mark-github", OcticonName.MarkGithub.ToSlug());
    }

    [Fact]
    public void OcticonName_IssueOpened_SlugIsKebabCase()
    {
        Assert.Equal("issue-opened", OcticonName.IssueOpened.ToSlug());
    }

    [Fact]
    public void OcticonName_StarFill_SlugIsKebabCase()
    {
        // Fill-variant suffix — verify the `-fill` tail is preserved.
        Assert.Equal("star-fill", OcticonName.StarFill.ToSlug());
    }

    [Fact]
    public void OcticonName_PlusCircle_SlugIsKebabCase()
    {
        Assert.Equal("plus-circle", OcticonName.PlusCircle.ToSlug());
    }

    [Fact]
    public void OcticonName_GitPullRequest_SlugIsKebabCase()
    {
        // Three-word PascalCase slug — one of the signature GitHub-branded icons.
        Assert.Equal("git-pull-request", OcticonName.GitPullRequest.ToSlug());
    }

    // ----------------------------------------------------------------------
    // 4. Parameter surface: Octicon must expose the Octicons-wrapper-shaped
    //    parameters (Name / NameString / Size / AriaLabel).
    // ----------------------------------------------------------------------

    [Fact]
    public void Octicon_HasName_Parameter()
    {
        var type = typeof(Octicon);
        var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(nameProp);
        Assert.NotNull(nameProp!.GetCustomAttribute<ParameterAttribute>());
        // Nullable enum — confirm the underlying type is OcticonName.
        Assert.Equal(typeof(OcticonName?), nameProp.PropertyType);
    }

    [Fact]
    public void Octicon_HasNameString_Parameter()
    {
        var type = typeof(Octicon);
        var prop = type.GetProperty("NameString", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetCustomAttribute<ParameterAttribute>());
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void Octicon_HasAriaLabel_Parameter()
    {
        var type = typeof(Octicon);
        var prop = type.GetProperty("AriaLabel", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetCustomAttribute<ParameterAttribute>());
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void Octicon_ExposesExpectedParameters()
    {
        var type = typeof(Octicon);
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
    public void Octicon_CapturesUnmatchedAttributes()
    {
        var type = typeof(Octicon);
        var splat = type.GetProperty("AdditionalAttributes", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(splat);
        var attr = splat!.GetCustomAttribute<ParameterAttribute>();
        Assert.NotNull(attr);
        Assert.True(attr!.CaptureUnmatchedValues,
            "Octicon should splat unmatched attributes (class, style, data-*).");
    }

    [Fact]
    public void OcticonName_HasNoDuplicateSlugs()
    {
        // Two enum members mapping to the same slug would mask a typo on one of them —
        // lock the starter set so any additions stay unique.
        var values = Enum.GetValues(typeof(OcticonName)).Cast<OcticonName>().ToList();
        var slugs = values.Select(v => v.ToSlug()).ToList();
        var duplicates = slugs.GroupBy(s => s).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }
}
