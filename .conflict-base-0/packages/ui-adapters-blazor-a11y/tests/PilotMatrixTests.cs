using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Sunfish.UIAdapters.Blazor.A11y.Tests.Fixtures;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests;

/// <summary>
/// Plan 4 Task 1.7 — 36-scenario pilot matrix. Three fixture components × 2 LTR/RTL ×
/// 2 light/dark × 3 CVD modes (None / Deuteranopia / Protanopia) per spec §7. Each
/// scenario renders the component through bUnit, hosts the markup via the Playwright
/// bridge under the relevant emulation, runs axe-core, and asserts zero violations at
/// impact ≥ moderate.
/// </summary>
/// <remarks>
/// Razor pilot components (SunfishButton.razor / SunfishDialog.razor / etc.) are not
/// yet wired with the parameters.a11y.sunfish contract on the .NET side, so the matrix
/// runs against the same fixture components that exercised the determinism gate in
/// Task 1.3. The CONTRACT-driven assertions (focus / keyboard / RTL icon mirror) ride
/// on top of axe; for fixture components without declared contracts they're skipped.
/// When real pilots land, the matrix swaps fixtures for Razor components without
/// changing the matrix shape.
/// </remarks>
public class PilotMatrixTests : IClassFixture<PilotMatrixTests.MatrixFixture>
{
    private readonly MatrixFixture _fixture;

    public PilotMatrixTests(MatrixFixture fixture) => _fixture = fixture;

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Bridge_PilotMatrix_ZeroViolationsAtModeratePlus(
        string fixtureName,
        string locale,
        bool rtl,
        string theme,
        CvdMode cvd)
    {
        var markup = RenderFixtureMarkup(fixtureName);

        var page = await _fixture.Host.NewPageAsync(new CultureInfo(locale), rtl, theme, cvd);
        try
        {
            var result = await AxeRunner.RunAxeAsync(markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();

            // Surface the violation IDs in failure messages so the matrix run is debuggable.
            Assert.True(moderatePlus.Count == 0,
                $"Scenario [{fixtureName}, {locale}, rtl={rtl}, theme={theme}, cvd={cvd}] " +
                $"surfaced {moderatePlus.Count} moderate+ violation(s): " +
                string.Join(", ", moderatePlus.Select(v => v.Id)));
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private string RenderFixtureMarkup(string fixtureName) => fixtureName switch
    {
        "Simple" => _fixture.Ctx.Render<SimpleTextFixture>(p => p
            .Add(c => c.Title, "Pilot Matrix")
            .Add(c => c.Body, "Body text rendered through bUnit for the bridge matrix.")).Markup,

        "Attributed" => _fixture.Ctx.Render<AttributedFixture>(p => p
            .Add(c => c.Label, "Submit")
            .Add(c => c.Enabled, true)
            .Add(c => c.Pressed, false)
            .Add(c => c.Level, 1)
            .Add(c => c.Variant, AttributedFixture.FixtureVariant.Primary)).Markup,

        "ChildContent" => _fixture.Ctx.Render<ChildContentFixture>(p => p
            .Add(c => c.Id, "matrix-1")
            .Add(c => c.Heading, "Compositional Fixture")
            .Add(c => c.Footer, "Composition footer")
            .AddChildContent("<p>Composed content under the heading.</p>")).Markup,

        _ => throw new System.ArgumentException($"Unknown fixture name: {fixtureName}", nameof(fixtureName)),
    };

    /// <summary>
    /// Generates 36 scenarios: 3 fixtures × 2 LTR/RTL × 2 light/dark × 3 CVD modes.
    /// Locales: en-US for LTR, ar-SA for RTL — matches the spec's Arabic-as-canonical-RTL choice.
    /// </summary>
    public static IEnumerable<object[]> Scenarios()
    {
        var fixtures = new[] { "Simple", "Attributed", "ChildContent" };
        var directions = new (bool rtl, string locale)[] { (false, "en-US"), (true, "ar-SA") };
        var themes = new[] { "light", "dark" };
        var cvds = new[] { CvdMode.None, CvdMode.Deuteranopia, CvdMode.Protanopia };

        foreach (var fixture in fixtures)
            foreach (var (rtl, locale) in directions)
                foreach (var theme in themes)
                    foreach (var cvd in cvds)
                        yield return new object[] { fixture, locale, rtl, theme, cvd };
    }

    /// <summary>Shared bUnit + Playwright host across the matrix to amortise startup cost.</summary>
    public sealed class MatrixFixture : IAsyncLifetime
    {
        public BunitContext Ctx { get; } = new();
        public PlaywrightPageHost Host { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Host = await PlaywrightPageHost.GetAsync();
        }

        public Task DisposeAsync()
        {
            Ctx.Dispose();
            return Task.CompletedTask;
        }
    }
}
