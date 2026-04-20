using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.UIAdapters.Blazor.Components.Utility;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

public class UtilityTests : BunitContext
{
    public UtilityTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
    }

    [Fact]
    public void SunfishIcon_RendersWithoutThrowing()
    {
        var cut = Render<SunfishIcon>(p => p.Add(x => x.Name, "home"));
        Assert.NotNull(cut.Markup);
        Assert.Contains("<span", cut.Markup);
    }
}
