using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Buttons;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Buttons;

/// <summary>
/// Wave 1 Plan 4 cluster A — bUnit-axe a11y harness for <see cref="ButtonGroupToggleButton"/>.
/// Stand-alone render exercises the <c>aria-pressed</c> path without depending on a
/// parent <see cref="SunfishButtonGroup"/> selection cascade.
/// </summary>
public class ButtonGroupToggleButtonA11yTests : IClassFixture<ButtonGroupToggleButtonA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public ButtonGroupToggleButtonA11yTests(Ctx ctx) => _ctx = ctx;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ButtonGroupToggleButton_HasNoAxeViolations(bool selected)
    {
        var rendered = _ctx.Bunit.Render<ButtonGroupToggleButton>(p => p
            .Add(c => c.Text, "Bold")
            .Add(c => c.Selected, selected));

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
