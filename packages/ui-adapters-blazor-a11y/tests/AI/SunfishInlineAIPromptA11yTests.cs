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
/// Wave 1 Plan 4 cluster B — bUnit-axe a11y harness for <see cref="SunfishInlineAIPrompt"/>.
/// The component renders nothing when <c>Show=false</c>; all scenarios open the popover.
/// </summary>
public class SunfishInlineAIPromptA11yTests : IClassFixture<SunfishInlineAIPromptA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishInlineAIPromptA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task ShownWithSeedTextHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishInlineAIPrompt>(p => p
            .Add(c => c.Show, true)
            .Add(c => c.Value, "The quick brown fox jumps over the lazy dog."));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "shown-with-text");
    }

    [Fact]
    public async Task ShownWithSuggestionsToolbarHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishInlineAIPrompt>(p => p
            .Add(c => c.Show, true)
            .Add(c => c.Value, "Make this paragraph clearer.")
            .Add(c => c.Suggestions, new[] { "Shorten", "Fix grammar", "Make formal" }));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "with-suggestions");
    }

    [Fact]
    public async Task ShownEmptyValueHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishInlineAIPrompt>(p => p
            .Add(c => c.Show, true));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "shown-empty");
    }

    [Fact]
    public async Task HiddenStateHasNoAxeViolations()
    {
        // Show=false → component renders no markup; axe should report nothing.
        var rendered = _ctx.Bunit.Render<SunfishInlineAIPrompt>();
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "hidden");
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
                $"InlineAIPrompt[{scenario}] surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
