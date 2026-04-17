using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Enums;
using Sunfish.Icons.Tabler;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.Icons.Tabler.Tests;

public class SunfishTablerIconProviderTests
{
    private static ISunfishIconProvider Resolve()
    {
        var services = new ServiceCollection();
        services.AddSunfishIconsTabler();
        return services.BuildServiceProvider().GetRequiredService<ISunfishIconProvider>();
    }

    [Fact] public void Registers() => Assert.IsType<SunfishTablerIconProvider>(Resolve());
    [Fact] public void LibraryName_IsTabler() => Assert.Equal("Tabler", Resolve().LibraryName);
    [Fact] public void RenderMode_IsSvgSprite() => Assert.Equal(IconRenderMode.SvgSprite, Resolve().RenderMode);

    [Fact]
    public void GetIcon_ReturnsNonEmptyMarkup()
    {
        var m = Resolve().GetIcon("home");
        Assert.False(string.IsNullOrWhiteSpace(m));
        Assert.Contains("<svg", m);
        Assert.Contains("tabler-home", m);
    }

    [Fact]
    public void GetIcon_RespectsExistingPrefix()
    {
        var m = Resolve().GetIcon("tabler-home");
        Assert.Contains("#tabler-home", m);
        Assert.DoesNotContain("tabler-tabler-", m);
    }

    [Theory]
    [InlineData(IconSize.Small, "16"), InlineData(IconSize.Medium, "20")]
    [InlineData(IconSize.Large, "24"), InlineData(IconSize.ExtraLarge, "32")]
    public void GetIcon_MapsSizeToPixels(IconSize size, string px)
    {
        var m = Resolve().GetIcon("home", size);
        Assert.Contains($"width=\"{px}\"", m);
        Assert.Contains($"height=\"{px}\"", m);
    }

    [Fact]
    public void SpriteUrl_IsRclContentPath() =>
        Assert.Equal("_content/Sunfish.Icons.Tabler/icons/tabler-sprite.svg", Resolve().GetIconSpriteUrl());

    [Fact]
    public void GetIcon_UsesSfIconCssClass()
    {
        var m = Resolve().GetIcon("home");
        Assert.Contains("sf-icon", m);
        Assert.DoesNotContain("mar-icon", m);
    }
}
