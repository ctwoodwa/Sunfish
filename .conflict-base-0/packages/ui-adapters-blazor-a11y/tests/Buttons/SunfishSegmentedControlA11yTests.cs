using System;
using System.Collections.Generic;
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
/// Wave 1 Plan 4 cluster A — bUnit-axe a11y harness for <see cref="SunfishSegmentedControl"/>.
/// Segmented controls expose ARIA <c>radiogroup</c> + per-item <c>radio</c>; axe checks the
/// composite roles parent/child relationship is well-formed.
/// </summary>
public class SunfishSegmentedControlA11yTests : IClassFixture<SunfishSegmentedControlA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishSegmentedControlA11yTests(Ctx ctx) => _ctx = ctx;

    [Theory]
    [InlineData("Day")]
    [InlineData("Week")]
    [InlineData("Month")]
    public async Task SunfishSegmentedControl_HasNoAxeViolations(string selected)
    {
        var rendered = _ctx.Bunit.Render<SunfishSegmentedControl>(p => p
            .Add(c => c.Items, new List<string> { "Day", "Week", "Month" })
            .Add(c => c.Value, selected));

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
