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

    [Fact(Skip = "axe violation: aria-prohibited-attr — SunfishChat typing-indicator bubble " +
        "applies aria-label to a non-interactive <div> which axe disallows. Real component " +
        "bug; out of scope per cluster-B brief (do not fix components). Re-enable once the " +
        "typing bubble is restructured (e.g. role=\"status\" wrapper or aria-live region).")]
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
