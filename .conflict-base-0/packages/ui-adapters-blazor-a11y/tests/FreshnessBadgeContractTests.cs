using System;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.LocalFirst;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests;

/// <summary>
/// Plan 4B §5 — assert SunfishFreshnessBadge honours the ADR 0036 contract for the
/// three states it actually surfaces (Healthy / Stale / Offline; conflict and
/// quarantine are out of scope for a per-record freshness badge).
/// </summary>
public class FreshnessBadgeContractTests : IClassFixture<FreshnessBadgeContractTests.Ctx>
{
    private readonly Ctx _ctx;

    public FreshnessBadgeContractTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public void Badge_HasRoleStatusAndAriaLivePolite()
    {
        var rendered = _ctx.Bunit.Render<SunfishFreshnessBadge>(p => p
            .Add(c => c.LastSyncedAt, DateTimeOffset.UtcNow.AddMinutes(-2))
            .Add(c => c.NowProvider, () => DateTimeOffset.UtcNow));

        var span = rendered.Find("span");
        Assert.Equal("status", span.GetAttribute("role"));
        Assert.Equal("polite", span.GetAttribute("aria-live"));
        Assert.Equal("true", span.GetAttribute("aria-atomic"));
    }

    [Fact]
    public void Badge_AriaLabelCombinesCanonicalPhraseWithVisibleAge()
    {
        var now = new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);
        var rendered = _ctx.Bunit.Render<SunfishFreshnessBadge>(p => p
            .Add(c => c.LastSyncedAt, now.AddMinutes(-2))
            .Add(c => c.NowProvider, () => now));

        var span = rendered.Find("span");
        Assert.Equal("Synced with all peers: 2 minutes ago", span.GetAttribute("aria-label"));
    }

    [Fact]
    public void Badge_OfflineAriaLabel_UsesCanonicalLongFormOnly()
    {
        var rendered = _ctx.Bunit.Render<SunfishFreshnessBadge>();
        // No LastSyncedAt → Offline.

        var span = rendered.Find("span");
        Assert.Equal("Offline — saved locally", span.GetAttribute("aria-label"));
    }

    [Fact]
    public void Badge_StaleAriaLabel_CombinesStalePhraseAndAge()
    {
        var now = new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);
        var rendered = _ctx.Bunit.Render<SunfishFreshnessBadge>(p => p
            .Add(c => c.LastSyncedAt, now.AddHours(-2))
            .Add(c => c.StalenessThreshold, TimeSpan.FromMinutes(30))
            .Add(c => c.NowProvider, () => now));

        var span = rendered.Find("span");
        Assert.Equal("Last synced earlier: 2 hours ago", span.GetAttribute("aria-label"));
    }

    public sealed class Ctx : IDisposable
    {
        public BunitContext Bunit { get; }

        public Ctx()
        {
            Bunit = new BunitContext();
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());
        }

        public void Dispose() => Bunit.Dispose();
    }
}
