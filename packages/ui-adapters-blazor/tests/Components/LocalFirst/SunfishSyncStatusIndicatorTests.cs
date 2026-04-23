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

public class SunfishSyncStatusIndicatorTests : BunitContext
{
    public SunfishSyncStatusIndicatorTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<ISunfishJsModuleLoader>(
            _ => Substitute.For<ISunfishJsModuleLoader>());
    }

    [Theory]
    [InlineData(SyncState.Healthy,         "sf-sync--healthy")]
    [InlineData(SyncState.Stale,           "sf-sync--stale")]
    [InlineData(SyncState.Offline,         "sf-sync--offline")]
    [InlineData(SyncState.ConflictPending, "sf-sync--conflict")]
    [InlineData(SyncState.Quarantine,      "sf-sync--quarantine")]
    public void EmitsCorrectClass_ForEachState(SyncState state, string expectedClass)
    {
        var cut = Render<SunfishSyncStatusIndicator>(p => p
            .Add(x => x.State, state));

        Assert.Contains(expectedClass, cut.Markup);
    }

    [Fact]
    public void LabelProperty_RendersInMarkup()
    {
        var cut = Render<SunfishSyncStatusIndicator>(p => p
            .Add(x => x.State, SyncState.Healthy)
            .Add(x => x.Label, "All good"));

        Assert.Contains("All good", cut.Markup);
    }

    [Fact]
    public void Click_FiresOnStateChanged_WithCurrentState()
    {
        SyncState? received = null;
        var cut = Render<SunfishSyncStatusIndicator>(p => p
            .Add(x => x.State, SyncState.ConflictPending)
            .Add(x => x.OnStateChanged,
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<SyncState>(
                    this, s => received = s)));

        cut.Find("button").Click();

        Assert.Equal(SyncState.ConflictPending, received);
    }

    [Fact]
    public void TitleProperty_RendersAsTooltip()
    {
        var cut = Render<SunfishSyncStatusIndicator>(p => p
            .Add(x => x.State, SyncState.Stale)
            .Add(x => x.Title, "Data is stale"));

        var button = cut.Find("button");
        Assert.Equal("Data is stale", button.GetAttribute("title"));
    }
}
