using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.DataDisplay;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.DataDisplay.Gauge;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for <see cref="SunfishCircularGauge"/>.
/// </summary>
public class SunfishCircularGaugeA11yTests : IClassFixture<SunfishCircularGaugeA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishCircularGaugeA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishCircularGauge_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishCircularGauge>(p => p
            .Add(c => c.Value, 67)
            .Add(c => c.AriaLabel, "CPU utilization"));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.Empty(moderatePlus);
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
