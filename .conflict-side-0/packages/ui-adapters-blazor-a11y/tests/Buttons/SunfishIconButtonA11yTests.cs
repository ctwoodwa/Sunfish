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
/// Wave 1 Plan 4 cluster A — bUnit-axe a11y harness for <see cref="SunfishIconButton"/>.
/// Icon-only buttons are a high-risk a11y surface (no visible text → must lean on
/// accessible name from aria-label or icon title); the harness asserts moderate+ axe
/// violations stay at zero across enabled/disabled states.
/// </summary>
public class SunfishIconButtonA11yTests : IClassFixture<SunfishIconButtonA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishIconButtonA11yTests(Ctx ctx) => _ctx = ctx;

    [Theory]
    [InlineData(ButtonSize.Small, true)]
    [InlineData(ButtonSize.Medium, true)]
    [InlineData(ButtonSize.Large, true)]
    [InlineData(ButtonSize.Medium, false)]  // disabled
    public async Task SunfishIconButton_HasNoAxeViolations(ButtonSize size, bool enabled)
    {
        var rendered = _ctx.Bunit.Render<SunfishIconButton>(p => p
            .Add(c => c.Size, size)
            .Add(c => c.Enabled, enabled)
            .Add(c => c.Icon, "save")
            .AddUnmatched("aria-label", "Save document"));

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
