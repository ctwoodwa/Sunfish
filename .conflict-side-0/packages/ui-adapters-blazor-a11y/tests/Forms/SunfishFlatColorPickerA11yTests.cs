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
using Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for <see cref="SunfishFlatColorPicker"/>.
/// Renders the default picker shape with a bound value and the palette view selected
/// (avoids the gradient view's deeper JS interop surface). Loose JS interop satisfies
/// any module loads that occur during render.
/// </summary>
public class SunfishFlatColorPickerA11yTests : IClassFixture<SunfishFlatColorPickerA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishFlatColorPickerA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishFlatColorPicker_PaletteView_HasNoModeratePlusAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishFlatColorPicker>(p => p
            .Add(c => c.Value, "#ff0000")
            .Add(c => c.View, Sunfish.Foundation.Enums.ColorPickerView.Palette)
            .Add(c => c.Views, new[] { Sunfish.Foundation.Enums.ColorPickerView.Palette }));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishFlatColorPicker surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
