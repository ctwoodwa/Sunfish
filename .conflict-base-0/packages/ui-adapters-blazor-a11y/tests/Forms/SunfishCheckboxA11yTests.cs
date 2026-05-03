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
/// Wave 4 cascade extension — bUnit-axe a11y harness for <see cref="SunfishCheckbox"/>.
/// Renders the checkbox bound to a value with a visible label, hosts the markup
/// in the Playwright bridge, and asserts axe-core surfaces zero moderate+ violations.
/// </summary>
public class SunfishCheckboxA11yTests : IClassFixture<SunfishCheckboxA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishCheckboxA11yTests(Ctx ctx) => _ctx = ctx;

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SunfishCheckbox_HasNoModeratePlusAxeViolations(bool value, bool enabled)
    {
        var rendered = _ctx.Bunit.Render<SunfishCheckbox>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.Enabled, enabled)
            .Add(c => c.Label, "Accept terms"));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishCheckbox surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
