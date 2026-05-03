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
using Sunfish.UIAdapters.Blazor.Components.Editors;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Editors;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for the
/// <see cref="SunfishSpreadsheet"/> in the Editors namespace (distinct from the
/// DataDisplay/Spreadsheet SunfishSpreadsheet covered in PR #113).
/// </summary>
public class SunfishSpreadsheetA11yTests : IClassFixture<SunfishSpreadsheetA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishSpreadsheetA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishSpreadsheetEditors_DefaultRender_HasNoModeratePlusAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishSpreadsheet>(p => p
            .Add(c => c.RowCount, 5)
            .Add(c => c.ColumnCount, 5));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishSpreadsheet (Editors) surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
