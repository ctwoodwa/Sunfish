using System;
using System.Globalization;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.LocalFirst;

/// <summary>
/// SunfishTeamSwitcher requires three injected services from the multi-team kernel
/// (ITeamContextFactory, IActiveTeamAccessor, INotificationAggregator) plus realistic
/// team data. Deferred to a dedicated multi-team workspace fixture.
/// </summary>
public class SunfishTeamSwitcherA11yTests : IClassFixture<SunfishTeamSwitcherA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishTeamSwitcherA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact(Skip = "Requires complex fixture - tracked: TeamSwitcher needs ITeamContextFactory + IActiveTeamAccessor + INotificationAggregator")]
    public Task SunfishTeamSwitcher_HasNoAxeViolations() => Task.CompletedTask;

    public sealed class Ctx : IDisposable
    {
        public BunitContext Bunit { get; }

        public Ctx()
        {
            Bunit = new BunitContext();
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());
            Bunit.JSInterop.Mode = JSRuntimeMode.Loose;
        }

        public async Task<Microsoft.Playwright.IPage> NewPageAsync()
        {
            var host = await PlaywrightPageHost.GetAsync();
            return await host.NewPageAsync(new CultureInfo("en-US"));
        }

        public void Dispose() => Bunit.Dispose();
    }
}
