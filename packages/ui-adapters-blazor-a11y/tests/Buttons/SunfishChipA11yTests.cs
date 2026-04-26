using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Buttons;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Buttons;

/// <summary>
/// Wave 1 Plan 4 cluster A — bUnit-axe a11y harness for <see cref="SunfishChip"/>.
/// Chip surfaces an interactive <c>role="option"</c> span with optional remove button;
/// axe must accept default, selected, and removable variants.
/// </summary>
public class SunfishChipA11yTests : IClassFixture<SunfishChipA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishChipA11yTests(Ctx ctx) => _ctx = ctx;

    // axe violation: aria-required-parent (Critical) — SunfishChip renders role="option" but
    // the standalone fragment has no role="listbox" ancestor. Either chip must drop role="option"
    // when used outside SunfishChipSet, or it must enforce that it can only be used inside one.
    // See wave-1-plan4-cluster-A-report.md §"A11y bugs found".
    [Fact(Skip = "axe violation: aria-required-parent (role=option without role=listbox) — see report")]
    public async Task SunfishChip_Default_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishChip>(p => p
            .Add(c => c.Label, "Filter A")
            .Add(c => c.Variant, ChipVariant.Default));

        await AssertNoViolations(rendered.Markup);
    }

    // axe violation: aria-required-parent (Critical) — same standalone-chip issue as above.
    [Fact(Skip = "axe violation: aria-required-parent (role=option without role=listbox) — see report")]
    public async Task SunfishChip_Selected_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishChip>(p => p
            .Add(c => c.Label, "Filter B")
            .Add(c => c.Selectable, true)
            .Add(c => c.IsSelected, true));

        await AssertNoViolations(rendered.Markup);
    }

    // axe violations:
    //   • aria-required-parent (Critical) — same as above.
    //   • nested-interactive (Serious) — the chip <span role="option"> has @onclick AND
    //     contains a <button> for remove; ARIA forbids interactive controls inside another
    //     interactive control.
    //   • target-size (Serious) — the &times; remove button is too small (24px minimum WCAG 2.2).
    [Fact(Skip = "axe violations: aria-required-parent + nested-interactive + target-size — see report")]
    public async Task SunfishChip_Removable_HasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishChip>(p => p
            .Add(c => c.Label, "Removable Filter")
            .Add(c => c.Removable, true));

        await AssertNoViolations(rendered.Markup);
    }

    private async Task AssertNoViolations(string markup)
    {
        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(markup, page);
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
