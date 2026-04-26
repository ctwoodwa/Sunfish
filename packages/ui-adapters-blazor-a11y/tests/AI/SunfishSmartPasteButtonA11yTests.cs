using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.AI;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.AI;

/// <summary>
/// Wave 1 Plan 4 cluster B — bUnit-axe a11y harness for <see cref="SunfishSmartPasteButton"/>.
/// </summary>
public class SunfishSmartPasteButtonA11yTests : IClassFixture<SunfishSmartPasteButtonA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishSmartPasteButtonA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task DefaultLabelHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishSmartPasteButton>();
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "default");
    }

    [Fact]
    public async Task CustomLabelHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishSmartPasteButton>(p => p
            .Add(c => c.Label, "Paste from clipboard"));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "custom-label");
    }

    [Fact]
    public async Task DisabledHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishSmartPasteButton>(p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.Label, "Paste"));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "disabled");
    }

    private async Task AssertNoModerateAxeViolationsAsync(string markup, string scenario)
    {
        var page = await _ctx.Host.NewPageAsync(new CultureInfo("en-US"));
        try
        {
            var result = await AxeRunner.RunAxeAsync(markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SmartPasteButton[{scenario}] surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
                string.Join(", ", moderatePlus.Select(v => v.Id)));
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public sealed class Ctx : IAsyncLifetime
    {
        public BunitContext Bunit { get; }
        public PlaywrightPageHost Host { get; private set; } = null!;

        public Ctx()
        {
            Bunit = new BunitContext();
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());
            // SunfishSmartPasteButton uses [Inject] IJSRuntime and invokes
            // navigator.clipboard.readText on click — Loose mode lets the click
            // handler resolve to a no-op stub even though we only render here.
            Bunit.JSInterop.Mode = JSRuntimeMode.Loose;
        }

        public async Task InitializeAsync()
        {
            Host = await PlaywrightPageHost.GetAsync();
        }

        public Task DisposeAsync()
        {
            Bunit.Dispose();
            return Task.CompletedTask;
        }
    }
}
