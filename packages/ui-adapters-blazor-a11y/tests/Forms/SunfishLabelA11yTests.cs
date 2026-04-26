using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Forms.Containers;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave-1 Plan 4 Cluster C — bUnit-axe a11y harness for <c>SunfishLabel</c>.
/// Renders the label with a realistic <c>For</c>/<c>Text</c> pairing into the bUnit
/// markup pipeline, hosts that markup in the Playwright bridge, and asserts axe-core
/// surfaces zero moderate+ violations against the wcag2a/aa + best-practice rule pack.
/// </summary>
/// <remarks>
/// Pattern mirrors <see cref="PilotMatrixTests"/>: shared <see cref="PlaywrightPageHost"/>
/// fixture amortises browser startup cost across the per-component file. Render params
/// pair the label with a sibling input so axe's <c>label</c> rule has a target id to
/// validate (a naked label-without-target is a real, separate a11y bug we don't want
/// the harness to false-positive).
/// </remarks>
public class SunfishLabelA11yTests : IClassFixture<SunfishLabelA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishLabelA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishLabel_StandaloneWithFor_ZeroAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishLabel>(p => p
            .Add(c => c.Text, "Email address")
            .Add(c => c.For, "email-input"));

        // Pair the label with a target input so axe's label rule has something to
        // bind against in the hosted page; otherwise we'd surface a non-component
        // violation rooted in the test scaffolding.
        var markup = rendered.Markup +
            "<input id=\"email-input\" type=\"text\" />";

        var page = await _ctx.Host.NewPageAsync(new CultureInfo("en-US"));
        try
        {
            var result = await AxeRunner.RunAxeAsync(markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();

            Assert.True(moderatePlus.Count == 0,
                $"SunfishLabel surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
