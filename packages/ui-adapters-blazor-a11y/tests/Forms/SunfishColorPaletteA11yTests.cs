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
/// Wave 4 cascade extension — bUnit-axe a11y harness for <see cref="SunfishColorPalette"/>.
/// Renders the grid-roled palette with a small swatch set; asserts axe surfaces zero
/// moderate+ violations.
/// </summary>
public class SunfishColorPaletteA11yTests : IClassFixture<SunfishColorPaletteA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishColorPaletteA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishColorPalette_DefaultRender_HasNoModeratePlusAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishColorPalette>(p => p
            .Add(c => c.Value, "#ff0000")
            .Add(c => c.Colors, new[] { "#ff0000", "#00ff00", "#0000ff", "#ffff00" })
            .Add(c => c.Columns, 4));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishColorPalette surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
