using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UIAdapters.Blazor.Components.LocalFirst;
using Sunfish.UIAdapters.Blazor.Internal.Interop;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components.LocalFirst;

public class SunfishNodeHealthBarTests : BunitContext
{
    public SunfishNodeHealthBarTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<ISunfishJsModuleLoader>(
            _ => Substitute.For<ISunfishJsModuleLoader>());
    }

    [Fact]
    public void RendersThreeIndicators()
    {
        var cut = Render<SunfishNodeHealthBar>(p => p
            .Add(x => x.NodeHealth,    SyncState.Healthy)
            .Add(x => x.LinkStatus,    SyncState.Stale)
            .Add(x => x.DataFreshness, SyncState.ConflictPending));

        Assert.Equal(3, cut.FindAll(".sf-sync-indicator").Count);
    }

    [Fact]
    public void IndicatorsAppearInPaperOrder_NodeLinkData()
    {
        var cut = Render<SunfishNodeHealthBar>(p => p
            .Add(x => x.NodeHealth,    SyncState.Healthy)
            .Add(x => x.LinkStatus,    SyncState.Offline)
            .Add(x => x.DataFreshness, SyncState.Quarantine));

        var indicators = cut.FindAll(".sf-sync-indicator");
        Assert.Contains("sf-sync--healthy",    indicators[0].GetAttribute("class"));
        Assert.Contains("sf-sync--offline",    indicators[1].GetAttribute("class"));
        Assert.Contains("sf-sync--quarantine", indicators[2].GetAttribute("class"));
    }

    [Fact]
    public void DefaultState_IsHealthy_ForAllThree()
    {
        var cut = Render<SunfishNodeHealthBar>();

        var indicators = cut.FindAll(".sf-sync-indicator");
        Assert.Equal(3, indicators.Count);
        foreach (var ind in indicators)
        {
            Assert.Contains("sf-sync--healthy", ind.GetAttribute("class"));
        }
    }
}
