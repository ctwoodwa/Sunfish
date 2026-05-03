using System;
using System.Collections.Generic;
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
/// Wave 1 Plan 4 cluster A — bUnit-axe a11y harness for <see cref="SunfishChipSet{TItem}"/>.
/// The chip set renders a <c>role="listbox"</c> container with per-item <c>option</c>
/// children. Generic on <see cref="string"/> here; axe must accept both single- and
/// multi-select variants.
/// </summary>
public class SunfishChipSetA11yTests : IClassFixture<SunfishChipSetA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishChipSetA11yTests(Ctx ctx) => _ctx = ctx;

    [Theory]
    [InlineData(GridSelectionMode.Single)]
    [InlineData(GridSelectionMode.Multiple)]
    [InlineData(GridSelectionMode.None)]
    public async Task SunfishChipSet_HasNoAxeViolations(GridSelectionMode mode)
    {
        var rendered = _ctx.Bunit.Render<SunfishChipSet<string>>(p => p
            .Add(c => c.AriaLabel, "Tag selector")
            .Add(c => c.SelectionMode, mode)
            .Add(c => c.Data, new List<string> { "Alpha", "Beta", "Gamma" }));

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
