using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Charts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Charts;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for the dispatcher
/// <see cref="SunfishGauge"/> that lives under Components/Charts (distinct from the
/// nested DataDisplay/Gauge family covered in PR #113).
/// </summary>
public class SunfishGaugeA11yTests : IClassFixture<SunfishGaugeA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishGaugeA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishGauge_DefaultRender_HasNoModeratePlusAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishGauge>(p => p
            .Add(c => c.Value, 42.0)
            .Add(c => c.Min, 0.0)
            .Add(c => c.Max, 100.0));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishGauge surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
