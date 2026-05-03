using System;
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

public class SunfishFreshnessBadgeTests : BunitContext
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 4, 22, 12, 0, 0, TimeSpan.Zero);

    public SunfishFreshnessBadgeTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<ISunfishJsModuleLoader>(
            _ => Substitute.For<ISunfishJsModuleLoader>());
    }

    [Fact]
    public void NullLastSyncedAt_RendersOfflineState()
    {
        var cut = Render<SunfishFreshnessBadge>(p => p
            .Add(x => x.LastSyncedAt, (DateTimeOffset?)null)
            .Add(x => x.NowProvider, () => FixedNow));

        Assert.Contains("sf-sync--offline", cut.Markup);
        Assert.Contains("Offline", cut.Markup);
    }

    [Fact]
    public void WithinThreshold_RendersHealthyState()
    {
        var cut = Render<SunfishFreshnessBadge>(p => p
            .Add(x => x.LastSyncedAt, FixedNow - TimeSpan.FromMinutes(2))
            .Add(x => x.StalenessThreshold, TimeSpan.FromMinutes(5))
            .Add(x => x.NowProvider, () => FixedNow));

        Assert.Contains("sf-sync--healthy", cut.Markup);
        Assert.DoesNotContain("sf-sync--stale", cut.Markup);
    }

    [Fact]
    public void BeyondThreshold_RendersStaleState()
    {
        var cut = Render<SunfishFreshnessBadge>(p => p
            .Add(x => x.LastSyncedAt, FixedNow - TimeSpan.FromMinutes(10))
            .Add(x => x.StalenessThreshold, TimeSpan.FromMinutes(5))
            .Add(x => x.NowProvider, () => FixedNow));

        Assert.Contains("sf-sync--stale", cut.Markup);
        Assert.DoesNotContain("sf-sync--healthy", cut.Markup);
    }

    [Fact]
    public void PrefixProp_PrependsToLabel()
    {
        var cut = Render<SunfishFreshnessBadge>(p => p
            .Add(x => x.LastSyncedAt, FixedNow - TimeSpan.FromMinutes(3))
            .Add(x => x.StalenessThreshold, TimeSpan.FromMinutes(15))
            .Add(x => x.Prefix, "As of ")
            .Add(x => x.NowProvider, () => FixedNow));

        Assert.Contains("As of 3 minutes ago", cut.Markup);
    }

    [Fact]
    public void StalenessMath_FormatsMinutesCorrectly()
    {
        Assert.Equal("just now",       SunfishFreshnessBadge.FormatAge(TimeSpan.FromSeconds(30)));
        Assert.Equal("1 minute ago",   SunfishFreshnessBadge.FormatAge(TimeSpan.FromMinutes(1)));
        Assert.Equal("7 minutes ago",  SunfishFreshnessBadge.FormatAge(TimeSpan.FromMinutes(7)));
        Assert.Equal("1 hour ago",     SunfishFreshnessBadge.FormatAge(TimeSpan.FromHours(1)));
        Assert.Equal("3 hours ago",    SunfishFreshnessBadge.FormatAge(TimeSpan.FromHours(3)));
        Assert.Equal("1 day ago",      SunfishFreshnessBadge.FormatAge(TimeSpan.FromDays(1)));
        Assert.Equal("2 days ago",     SunfishFreshnessBadge.FormatAge(TimeSpan.FromDays(2)));
    }

    [Fact]
    public void StateProperty_Derives_FromInputs()
    {
        var cut = Render<SunfishFreshnessBadge>(p => p
            .Add(x => x.LastSyncedAt, FixedNow - TimeSpan.FromMinutes(10))
            .Add(x => x.StalenessThreshold, TimeSpan.FromMinutes(5))
            .Add(x => x.NowProvider, () => FixedNow));

        Assert.Equal(SyncState.Stale, cut.Instance.State);
    }
}
