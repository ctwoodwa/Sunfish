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
using Sunfish.UIAdapters.Blazor.Components.DataDisplay.Spreadsheet;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.DataDisplay.Spreadsheet;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for <see cref="SunfishSpreadsheet"/>.
/// Renders with the default 10x6 grid and an aria-label so axe can validate the
/// role="grid" surface's accessible-name pathway.
/// </summary>
public class SunfishSpreadsheetA11yTests : IClassFixture<SunfishSpreadsheetA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishSpreadsheetA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishSpreadsheet_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishSpreadsheet>(p => p
            .Add(c => c.AriaLabel, "Budget worksheet")
            .Add(c => c.RowCount, 4)
            .Add(c => c.ColumnCount, 3));

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
