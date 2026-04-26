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

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.DataDisplay.DataGrid;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for <see cref="SunfishPivotGridContainer"/>.
/// </summary>
public class SunfishPivotGridContainerA11yTests : IClassFixture<SunfishPivotGridContainerA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishPivotGridContainerA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishPivotGridContainer_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishPivotGridContainer>(p => p
            .AddChildContent("<div>Pivot grid content placeholder</div>"));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishPivotGridContainer surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
