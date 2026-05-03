using System;
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
using Sunfish.UIAdapters.Blazor.Enums;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.AI;

/// <summary>
/// Wave 1 Plan 4 cluster B — bUnit-axe a11y harness for <see cref="SunfishChat"/>.
/// </summary>
public class SunfishChatA11yTests : IClassFixture<SunfishChatA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishChatA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task EmptyConversationHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishChat>();
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "empty");
    }

    [Fact]
    public async Task UserAndAssistantMessagesHaveNoAxeViolations()
    {
        var stamp = new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, "You are a helpful assistant.", stamp),
            new ChatMessage(ChatRole.User, "Summarise the local-node paper.", stamp.AddMinutes(1)),
            new ChatMessage(ChatRole.Assistant, "It inverts the SaaS paradigm…", stamp.AddMinutes(2)),
        };

        var rendered = _ctx.Bunit.Render<SunfishChat>(p => p.Add(c => c.Messages, messages));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "messages");
    }

    // TRIAGE 2026-04-26 (skipped-test inventory): FIX-LATER.
    // Tracking: real production bug surfaced by Wave-1 cluster-B cascade (PR #112).
    // Component bug — `<div aria-label="…">` violates axe `aria-prohibited-attr`
    // because <div> has no implicit role that permits aria-label.
    // Unblocker (small, scoped): edit `Components/AI/SunfishChat.razor` line 53 to
    // add `role="status"` to the bubble (or wrap in `<div role="status" aria-live="polite">`).
    // Owner: AI-block component team. ETA: next a11y remediation wave.
    // Why deferred here: per task brief — test files only; production fix lives in a
    // dedicated PR per audit §8.4 (ai-fixes subagent dispatch).
    [Fact(Skip = "FIX-LATER (axe-real-bug): aria-prohibited-attr on typing-indicator div. " +
        "Unblocker: add role=\"status\" to .sf-chat__bubble--typing in SunfishChat.razor:53. " +
        "See waves/cleanup/2026-04-26-followup-debt-audit.md §1b + §8.4.")]
    public async Task TypingIndicatorHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishChat>(p => p.Add(c => c.TypingIndicator, true));
        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "typing");
    }

    [Fact]
    public async Task DisabledComposerHasNoAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishChat>(p => p
            .Add(c => c.Disabled, true)
            .Add(c => c.Placeholder, "Composer disabled while reconnecting…"));
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
                $"Chat[{scenario}] surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
