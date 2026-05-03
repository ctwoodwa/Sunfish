using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Services;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Components.Utility;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests;

/// <summary>
/// Regression coverage for the icon-rendering boundary bug.
/// </summary>
/// <remarks>
/// <see cref="ISunfishIconProvider.GetIcon"/> returns a raw HTML string because the
/// contract is framework-agnostic. In Blazor, <c>@IconProvider.GetIcon(...)</c>
/// HTML-encodes the SVG, rendering <c>&lt;svg&gt;...&lt;/svg&gt;</c> as visible text
/// instead of as DOM — the bug the user reported on <c>/icons</c>. The fix is the
/// <c>GetIconMarkup</c> extension, which wraps the string in
/// <see cref="Microsoft.AspNetCore.Components.MarkupString"/> so Razor renders it raw.
///
/// These tests lock the fix in: if anyone reverts back to <c>GetIcon(...)</c> or
/// forgets the <see cref="Microsoft.AspNetCore.Components.MarkupString"/> wrap inside
/// <see cref="SunfishIcon"/>, the assertions below fail because the rendered markup
/// will contain the encoded sentinel <c>&amp;lt;svg</c> instead of real <c>&lt;svg</c>.
/// </remarks>
public class SunfishIconRenderingTests : BunitContext
{
    /// <summary>
    /// Marker string embedded in the fake provider's output so tests can assert that
    /// the SVG payload reached the DOM without being HTML-encoded.
    /// </summary>
    private const string SentinelMarker = "data-sunfish-icon-sentinel=\"rendered\"";

    private sealed class FakeIconProvider : ISunfishIconProvider
    {
        public string LibraryName => "Fake";
        public IconRenderMode RenderMode => IconRenderMode.InlineSvg;
        public string GetIcon(string name, IconSize size = IconSize.Medium)
            => $"""<svg {SentinelMarker} data-name="{name}" data-size="{size}"><use href="#{name}"></use></svg>""";
        public string GetIconSpriteUrl() => "/fake-sprite.svg";
    }

    public SunfishIconRenderingTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, FakeIconProvider>();
    }

    [Fact]
    public void GetIconMarkup_RendersAsRawHtml_NotEncodedText()
    {
        var provider = new FakeIconProvider();

        var markup = provider.GetIconMarkup("home");

        Assert.Contains("<svg", markup.Value);
        Assert.Contains(SentinelMarker, markup.Value);
        Assert.DoesNotContain("&lt;svg", markup.Value);
    }

    [Fact]
    public void SunfishIcon_ByName_EmitsRealSvgElement_NotEscapedText()
    {
        var cut = Render<SunfishIcon>(parameters => parameters
            .Add(p => p.Name, "home"));

        Assert.Contains("<svg", cut.Markup);
        Assert.Contains(SentinelMarker, cut.Markup);
        Assert.Contains("data-name=\"home\"", cut.Markup);
        Assert.DoesNotContain("&lt;svg", cut.Markup);
        Assert.DoesNotContain("&amp;lt;", cut.Markup);
    }

    [Fact]
    public void SunfishIcon_ByName_RendersSvgAsDom_ResolvableBySelector()
    {
        var cut = Render<SunfishIcon>(parameters => parameters
            .Add(p => p.Name, "settings")
            .Add(p => p.Size, IconSize.Large));

        var svg = cut.Find("svg");
        Assert.NotNull(svg);
        Assert.Equal("settings", svg.GetAttribute("data-name"));
        Assert.Equal(IconSize.Large.ToString(), svg.GetAttribute("data-size"));
    }

    [Theory]
    [InlineData(IconSize.Small)]
    [InlineData(IconSize.Medium)]
    [InlineData(IconSize.Large)]
    [InlineData(IconSize.ExtraLarge)]
    public void SunfishIcon_AllSizes_RenderSvgAsDom(IconSize size)
    {
        var cut = Render<SunfishIcon>(parameters => parameters
            .Add(p => p.Name, "home")
            .Add(p => p.Size, size));

        var svg = cut.Find("svg");
        Assert.Equal(size.ToString(), svg.GetAttribute("data-size"));
        Assert.DoesNotContain("&lt;svg", cut.Markup);
    }
}
