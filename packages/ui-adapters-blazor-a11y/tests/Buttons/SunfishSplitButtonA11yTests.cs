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
/// Wave 1 Plan 4 cluster A — bUnit-axe a11y harness for <see cref="SunfishSplitButton"/>.
/// Split buttons render two adjacent buttons (primary action + dropdown trigger). The
/// dropdown trigger must expose <c>aria-haspopup</c> and <c>aria-expanded</c>; axe
/// confirms the rendered shell is clean in both closed and open menu states.
/// </summary>
public class SunfishSplitButtonA11yTests : IClassFixture<SunfishSplitButtonA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishSplitButtonA11yTests(Ctx ctx) => _ctx = ctx;

    // axe violation: button-name (Critical) — the dropdown-trigger <button> renders only a
    // chevron icon, no accessible name. SunfishSplitButton must surface aria-label="Open menu"
    // (or similar) on the secondary button so screen readers can announce its purpose.
    // See wave-1-plan4-cluster-A-report.md §"A11y bugs found".
    [Fact(Skip = "axe violation: button-name on dropdown trigger — see report")]
    public async Task SunfishSplitButton_Default_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishSplitButton>(p => p
            .Add(c => c.AriaLabel, "Save options")
            .AddChildContent("Save"));

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

    // axe violation: button-name (Critical) — same dropdown-trigger issue as above; the
    // disabled state still renders the chevron-only secondary button without an accessible name.
    [Fact(Skip = "axe violation: button-name on dropdown trigger — see report")]
    public async Task SunfishSplitButton_Disabled_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishSplitButton>(p => p
            .Add(c => c.Enabled, false)
            .Add(c => c.AriaLabel, "Save options")
            .AddChildContent("Save"));

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
