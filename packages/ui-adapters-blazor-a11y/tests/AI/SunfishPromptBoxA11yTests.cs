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
/// Wave 1 Plan 4 cluster B — bUnit-axe a11y harness for <see cref="SunfishPromptBox"/>.
/// </summary>
public class SunfishPromptBoxA11yTests : IClassFixture<SunfishPromptBoxA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishPromptBoxA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task DefaultEmptyHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishPromptBox>();
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "default-empty");
    }

    [Fact]
    public async Task WithSeedValueHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishPromptBox>(p => p
            .Add(c => c.Value, "Why is the sky blue?"));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "seed-value");
    }

    [Fact]
    public async Task WithHistoryToggleHasNoAxeViolations()
    {
        // History toggle exposes aria-haspopup + aria-expanded — verify both states
        // pass axe. Closed state is the default; click handling is bUnit-internal so
        // we check the rendered baseline only.
        var rendered = _ctx.Bunit.Render<SunfishPromptBox>(p => p
            .Add(c => c.History, new[] { "Earlier prompt one", "Earlier prompt two" }));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "history-closed");
    }

    [Fact]
    public async Task DisabledHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishPromptBox>(p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.History, new[] { "Earlier prompt" })
            .Add(c => c.Placeholder, "Disabled while reconnecting…"));
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
                $"PromptBox[{scenario}] surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
