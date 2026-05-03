using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Navigation;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Navigation.Breadcrumb;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for <see cref="SunfishBreadcrumb"/>.
/// Renders the breadcrumb with a representative trail and asserts axe surfaces zero
/// moderate+ violations against the nav>ol>li structure.
/// </summary>
public class SunfishBreadcrumbA11yTests : IClassFixture<SunfishBreadcrumbA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishBreadcrumbA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishBreadcrumb_DataDriven_HasNoModeratePlusAxeViolations()
    {
        var data = new List<object>
        {
            new { Text = "Home", Url = "/" },
            new { Text = "Library", Url = "/library" },
            new { Text = "Data" }
        };

        var rendered = _ctx.Bunit.Render<SunfishBreadcrumb>(p => p
            .Add(c => c.Data, data));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishBreadcrumb surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
