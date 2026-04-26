using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Overlays;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Overlays;

/// <summary>
/// Wave-1 Plan 4 Cluster C — bUnit-axe a11y harness for <c>WindowActionButton</c>.
/// Renders the action button with a realistic <c>Name</c> + <c>Title</c> pairing
/// (matching the title-bar Close/Minimize/Maximize affordances), hosts the markup
/// in the Playwright bridge, and asserts axe-core surfaces zero moderate+ violations.
/// </summary>
/// <remarks>
/// <c>WindowActionButton</c>'s <c>ParentWindow</c> cascade is nullable-tolerant — the
/// component renders a <c>&lt;button&gt;</c> standalone. Its accessible name comes
/// from <c>aria-label="@(Title ?? Name)"</c>, so the test asserts that contract via
/// axe by passing both <c>Name</c> and <c>Title</c>.
/// </remarks>
public class WindowActionButtonA11yTests : IClassFixture<WindowActionButtonA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public WindowActionButtonA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task WindowActionButton_VisibleWithTitle_ZeroAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<WindowActionButton>(p => p
            .Add(c => c.Name, "Close")
            .Add(c => c.Title, "Close window")
            .Add(c => c.Hidden, false));

        var page = await _ctx.Host.NewPageAsync(new CultureInfo("en-US"));
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();

            Assert.True(moderatePlus.Count == 0,
                $"WindowActionButton surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
                string.Join(", ", moderatePlus.Select(v => v.Id)));
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public sealed class Ctx : IAsyncLifetime, IDisposable
    {
        public BunitContext Bunit { get; } = new();
        public PlaywrightPageHost Host { get; private set; } = null!;

        public Ctx()
        {
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());
        }

        public async Task InitializeAsync() => Host = await PlaywrightPageHost.GetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        public void Dispose() => Bunit.Dispose();
    }
}
