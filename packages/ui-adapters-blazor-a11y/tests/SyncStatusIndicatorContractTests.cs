using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.LocalFirst;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests;

/// <summary>
/// Plan 4B §5 — assert that the production SunfishSyncStatusIndicator honours the
/// ADR 0036 multimodal-encoding contract: ARIA role split (status vs alert) and
/// aria-live politeness (polite vs assertive) per state severity.
/// </summary>
/// <remarks>
/// Doesn't go through the Playwright bridge — this is a markup-level assertion on
/// bUnit's rendered output. Faster than the full bridge run and decouples
/// the contract conformance check from browser availability.
/// </remarks>
public class SyncStatusIndicatorContractTests : IClassFixture<SyncStatusIndicatorContractTests.Ctx>
{
    private readonly Ctx _ctx;

    public SyncStatusIndicatorContractTests(Ctx ctx) => _ctx = ctx;

    [Theory]
    [InlineData(SyncState.Healthy, "status", "polite")]
    [InlineData(SyncState.Stale, "status", "polite")]
    [InlineData(SyncState.Offline, "status", "polite")]
    [InlineData(SyncState.ConflictPending, "alert", "assertive")]
    [InlineData(SyncState.Quarantine, "alert", "assertive")]
    public void Indicator_HasCorrectRoleAndLivePerState(SyncState state, string expectedRole, string expectedLive)
    {
        var rendered = _ctx.Bunit.Render<SunfishSyncStatusIndicator>(p => p.Add(c => c.State, state));
        var btn = rendered.Find("button");

        Assert.Equal(expectedRole, btn.GetAttribute("role"));
        Assert.Equal(expectedLive, btn.GetAttribute("aria-live"));
        Assert.Equal("true", btn.GetAttribute("aria-atomic"));
    }

    [Theory]
    [InlineData(SyncState.Healthy, "Synced with all peers")]
    [InlineData(SyncState.Stale, "Last synced earlier")]
    [InlineData(SyncState.Offline, "Offline — saved locally")]
    [InlineData(SyncState.ConflictPending, "Review required — two versions diverged")]
    [InlineData(SyncState.Quarantine, "Can't sync — open diagnostics")]
    public void Indicator_DefaultsAriaLabelToCanonicalLongForm(SyncState state, string expected)
    {
        var rendered = _ctx.Bunit.Render<SunfishSyncStatusIndicator>(p => p.Add(c => c.State, state));
        var btn = rendered.Find("button");

        Assert.Equal(expected, btn.GetAttribute("aria-label"));
    }

    [Fact]
    public void Indicator_PrefersConsumerSuppliedLabelOverCanonicalLong()
    {
        var rendered = _ctx.Bunit.Render<SunfishSyncStatusIndicator>(p => p
            .Add(c => c.State, SyncState.Healthy)
            .Add(c => c.Label, "Custom label"));
        var btn = rendered.Find("button");

        Assert.Equal("Custom label", btn.GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData(SyncState.Healthy, "sf-sync--healthy")]
    [InlineData(SyncState.ConflictPending, "sf-sync--conflict")]
    public void Indicator_EmitsStateClassSuffix(SyncState state, string expectedClassFragment)
    {
        var rendered = _ctx.Bunit.Render<SunfishSyncStatusIndicator>(p => p.Add(c => c.State, state));
        var btn = rendered.Find("button");

        Assert.Contains(expectedClassFragment, btn.GetAttribute("class") ?? string.Empty);
    }

    public sealed class Ctx : System.IDisposable
    {
        public BunitContext Bunit { get; }

        public Ctx()
        {
            Bunit = new BunitContext();
            // SunfishComponentBase has an [Inject] dependency on ISunfishCssProvider.
            // NSubstitute returns string.Empty for all string-returning methods,
            // sufficient for SyncStatusIndicator which generates its own sf-sync--*
            // classes without consulting the provider.
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());
        }

        public void Dispose() => Bunit.Dispose();
    }
}
