using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Navigation;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Navigation.Menu;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for <see cref="SunfishMenu"/>.
/// Renders the menu in its open state with simple child content and asserts axe
/// surfaces zero moderate+ violations against the role=menu surface.
/// </summary>
public class SunfishMenuA11yTests : IClassFixture<SunfishMenuA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishMenuA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishMenu_OpenWithChildContent_HasNoModeratePlusAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishMenu>(p => p
            .Add(c => c.IsOpen, true)
            .AddChildContent("<button role=\"menuitem\">Open</button>" +
                "<button role=\"menuitem\">Save</button>"));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishMenu surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
