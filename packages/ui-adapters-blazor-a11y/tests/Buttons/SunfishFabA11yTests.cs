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
/// Wave 1 Plan 4 cluster A — bUnit-axe a11y harness for <see cref="SunfishFab"/>.
/// FABs are icon-led floating buttons with a fixed/absolute position; axe must accept
/// both the icon-only and icon+text renderings.
/// </summary>
public class SunfishFabA11yTests : IClassFixture<SunfishFabA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishFabA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishFab_IconOnly_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishFab>(p => p
            .Add(c => c.Icon, "add")
            .AddUnmatched("aria-label", "Create new"));

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

    [Fact]
    public async Task SunfishFab_WithText_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishFab>(p => p
            .Add(c => c.Icon, "add")
            .AddChildContent("Create"));

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
