using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Buttons;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Buttons;

/// <summary>
/// Wave 1 Plan 4 cluster A — bUnit-axe a11y harness for <see cref="SunfishButtonGroup"/>.
/// Button groups host <see cref="ButtonGroupButton"/> / <see cref="ButtonGroupToggleButton"/>
/// children inside <c>role="group"</c>; axe must accept the populated container.
/// </summary>
public class SunfishButtonGroupA11yTests : IClassFixture<SunfishButtonGroupA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishButtonGroupA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishButtonGroup_WithChildren_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishButtonGroup>(p => p
            .AddChildContent<ButtonGroupButton>(b => b.Add(x => x.Text, "Cut"))
            .AddChildContent<ButtonGroupButton>(b => b.Add(x => x.Text, "Copy"))
            .AddChildContent<ButtonGroupButton>(b => b.Add(x => x.Text, "Paste")));

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
