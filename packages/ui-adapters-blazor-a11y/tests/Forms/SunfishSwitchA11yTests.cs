using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for <see cref="SunfishSwitch"/>.
/// Renders the switch with on/off labels, hosts the markup in the Playwright bridge,
/// and asserts axe-core surfaces zero moderate+ violations.
/// </summary>
public class SunfishSwitchA11yTests : IClassFixture<SunfishSwitchA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishSwitchA11yTests(Ctx ctx) => _ctx = ctx;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SunfishSwitch_HasNoModeratePlusAxeViolations(bool value)
    {
        var rendered = _ctx.Bunit.Render<SunfishSwitch>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.OnLabel, "On")
            .Add(c => c.OffLabel, "Off")
            .AddUnmatched("aria-label", "Notifications enabled"));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishSwitch surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
                string.Join(", ", moderatePlus.Select(v => v.Id)));
        }
        finally { await page.CloseAsync(); }
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

        public async Task<Microsoft.Playwright.IPage> NewPageAsync()
        {
            var host = await PlaywrightPageHost.GetAsync();
            return await host.NewPageAsync(new CultureInfo("en-US"));
        }

        public void Dispose() => Bunit.Dispose();
    }
}
