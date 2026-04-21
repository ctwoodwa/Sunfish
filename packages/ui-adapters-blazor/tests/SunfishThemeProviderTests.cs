using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UIAdapters.Blazor.Components.Utility;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests;

public class SunfishThemeProviderTests : BunitContext
{
    public SunfishThemeProviderTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
    }

    [Fact]
    public void SunfishThemeProvider_RendersChildContent()
    {
        var cut = Render<SunfishThemeProvider>(p => p
            .AddChildContent("<span>hello</span>"));

        Assert.Contains("hello", cut.Markup);
    }

    [Fact]
    public void SunfishThemeProvider_HasSfThemeProviderClass()
    {
        var cut = Render<SunfishThemeProvider>();
        Assert.Contains("sf-theme-provider", cut.Markup);
    }

    [Fact]
    public void SunfishThemeProvider_HasDataSfThemeAttribute()
    {
        var cut = Render<SunfishThemeProvider>();
        Assert.Contains("data-sf-theme", cut.Markup);
    }
}
