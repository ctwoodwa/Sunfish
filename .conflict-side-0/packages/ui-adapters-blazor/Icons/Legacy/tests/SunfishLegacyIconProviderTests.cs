using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Enums;
using Sunfish.Icons.Legacy;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.Icons.Legacy.Tests;

public class SunfishLegacyIconProviderTests
{
    private static ISunfishIconProvider Resolve()
    {
        var services = new ServiceCollection();
        services.AddSunfishIconsLegacy();
        return services.BuildServiceProvider().GetRequiredService<ISunfishIconProvider>();
    }

    [Fact] public void Registers() => Assert.IsType<SunfishLegacyIconProvider>(Resolve());
    [Fact] public void LibraryName_IsLegacy() => Assert.Equal("Legacy", Resolve().LibraryName);
    [Fact] public void RenderMode_IsSvgSprite() => Assert.Equal(IconRenderMode.SvgSprite, Resolve().RenderMode);

    [Fact]
    public void GetIcon_AutoPrefixesWithSf()
    {
        var m = Resolve().GetIcon("search");
        Assert.Contains("#sf-search", m);
        Assert.DoesNotContain("marilo-search", m);
    }

    [Fact]
    public void GetIcon_RespectsExistingSfPrefix()
    {
        var m = Resolve().GetIcon("sf-search");
        Assert.Contains("#sf-search", m);
        Assert.DoesNotContain("sf-sf-", m);
    }

    [Fact]
    public void SpriteUrl_IsRclContentPath() =>
        Assert.Equal("_content/Sunfish.Icons.Legacy/icons/sprite.svg", Resolve().GetIconSpriteUrl());

    [Fact]
    public void ProviderType_IsMarkedObsolete() =>
        Assert.NotEmpty(typeof(SunfishLegacyIconProvider)
            .GetCustomAttributes(typeof(ObsoleteAttribute), false));

    [Fact]
    public void ExtensionMethod_IsMarkedObsolete()
    {
        var method = typeof(SunfishIconsLegacyServiceExtensions)
            .GetMethod(nameof(SunfishIconsLegacyServiceExtensions.AddSunfishIconsLegacy))!;
        Assert.NotEmpty(method.GetCustomAttributes(typeof(ObsoleteAttribute), false));
    }
}
