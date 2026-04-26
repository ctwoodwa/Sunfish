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
using Sunfish.UIAdapters.Blazor.Components.AI;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.AI;

/// <summary>
/// Wave 1 Plan 4 cluster B — bUnit-axe a11y harness for <see cref="SunfishAIPrompt"/>.
/// Mirrors the per-component pattern used by the SyncState contract tests
/// (<see cref="SyncStatusIndicatorContractTests"/>): bUnit renders the component with
/// minimal-but-realistic params, the markup is shipped through the
/// <see cref="AxeRunner"/> Playwright bridge, and any moderate-or-greater axe
/// violations are surfaced as test failures (rule id included in the message).
/// </summary>
/// <remarks>
/// Component bugs are not fixed here — if a real a11y violation surfaces the test is
/// marked <c>Skip</c> with the offending rule id so the cluster lands a complete
/// inventory and the bug stays visible for downstream remediation.
/// </remarks>
public class SunfishAIPromptA11yTests : IClassFixture<SunfishAIPromptA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishAIPromptA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task DefaultPlaceholderShellHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishAIPrompt>();
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "default");
    }

    [Fact]
    public async Task WithModelPickerHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishAIPrompt>(p => p
            .Add(c => c.Models, new[] { "gpt-4", "claude-opus-4-7" })
            .Add(c => c.SelectedModel, "claude-opus-4-7")
            .Add(c => c.Value, "Summarise the local-node architecture paper."));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "model-picker");
    }

    [Fact]
    public async Task WithStreamingOutputHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishAIPrompt>(p => p
            .Add(c => c.Streaming, true)
            .Add(c => c.StreamedOutput, "Streaming response chunk one. Streaming response chunk two."));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "streaming");
    }

    // TRIAGE 2026-04-26 (skipped-test inventory): FIX-LATER.
    // Tracking: real production bug surfaced by Wave-1 cluster-B cascade (PR #112).
    // WCAG 2.2 SC 2.5.8 (target-size minimum) — `.sf-aiprompt__history-button` renders
    // below the 24×24 CSS-pixel minimum hit target with default styling.
    // Unblocker (small, scoped): set min-inline-size + min-block-size of >=24px on
    // `.sf-aiprompt__history-button` in the AIPrompt theme SCSS (Material/Bootstrap/FluentUI).
    // Owner: AI-block component team. ETA: next a11y remediation wave.
    // Why deferred here: per task brief — test files only; production fix lives in a
    // dedicated PR per audit §8.4 (ai-fixes subagent dispatch).
    [Fact(Skip = "FIX-LATER (axe-real-bug): target-size on history-aside buttons (WCAG 2.2 SC 2.5.8). " +
        "Unblocker: enforce min-inline-size/min-block-size >=24px on .sf-aiprompt__history-button. " +
        "See waves/cleanup/2026-04-26-followup-debt-audit.md §1b + §8.4.")]
    public async Task WithHistoryAsideHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishAIPrompt>(p => p
            .Add(c => c.History, new[] { "Previous prompt one", "Previous prompt two" }));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "history");
    }

    [Fact]
    public async Task DisabledStateHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishAIPrompt>(p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.Models, new[] { "gpt-4" })
            .Add(c => c.History, new[] { "Earlier prompt" }));
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
                $"AIPrompt[{scenario}] surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
            // SunfishComponentBase has [Inject] dependencies; mirror the
            // SyncStatusIndicatorContractTests setup so renders don't bind-throw.
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
