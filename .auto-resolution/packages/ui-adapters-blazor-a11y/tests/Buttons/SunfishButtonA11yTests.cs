using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Buttons;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Buttons;

/// <summary>
/// Wave 1 Plan 4 cluster A — bUnit-axe a11y harness for <see cref="SunfishButton"/>.
/// Renders the component with realistic parameters via bUnit, hosts the markup in a
/// Playwright page through <see cref="AxeRunner"/>, and asserts zero moderate+ axe
/// violations across the canonical state matrix (variant × disabled).
/// </summary>
public class SunfishButtonA11yTests : IClassFixture<SunfishButtonA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishButtonA11yTests(Ctx ctx) => _ctx = ctx;

    [Theory]
    [InlineData(ButtonVariant.Primary, true)]
    [InlineData(ButtonVariant.Secondary, true)]
    [InlineData(ButtonVariant.Danger, true)]
    [InlineData(ButtonVariant.Primary, false)]  // disabled
    public async Task SunfishButton_HasNoAxeViolations(ButtonVariant variant, bool enabled)
    {
        var rendered = _ctx.Bunit.Render<SunfishButton>(p => p
            .Add(c => c.Variant, variant)
            .Add(c => c.Enabled, enabled)
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

    public sealed class Ctx : IAsyncLifetime, IDisposable
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

        public Task InitializeAsync() => Task.CompletedTask;
        public Task DisposeAsync() => Task.CompletedTask;
        public void Dispose() => Bunit.Dispose();
    }
}
