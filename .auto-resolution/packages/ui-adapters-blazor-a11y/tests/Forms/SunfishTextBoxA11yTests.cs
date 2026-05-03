using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave-1 Plan 4 Cluster C — bUnit-axe a11y harness for <c>SunfishTextBox</c>.
/// Renders the textbox bound to a sample value with placeholder + accessible label
/// supplied via <c>aria-label</c>, hosts the markup in the Playwright bridge, and
/// asserts axe-core surfaces zero moderate+ violations.
/// </summary>
/// <remarks>
/// Per the brief, Forms components must render with "input bound to a value" — the
/// <see cref="SunfishTextBox.Value"/> parameter satisfies that. <c>aria-label</c> is
/// passed via <c>AdditionalAttributes</c> so axe's <c>label</c> rule has an
/// accessible name without requiring an external <c>&lt;label for=...&gt;</c>.
/// </remarks>
public class SunfishTextBoxA11yTests : IClassFixture<SunfishTextBoxA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishTextBoxA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishTextBox_BoundValueWithAriaLabel_ZeroAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishTextBox>(p => p
            .Add(c => c.Value, "jane.doe@example.com")
            .Add(c => c.Placeholder, "Enter email address")
            .Add(c => c.InputType, "email")
            .AddUnmatched("aria-label", "Email address"));

        var page = await _ctx.Host.NewPageAsync(new CultureInfo("en-US"));
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();

            Assert.True(moderatePlus.Count == 0,
                $"SunfishTextBox surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
