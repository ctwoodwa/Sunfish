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
/// Wave 4 cascade extension — bUnit-axe a11y harness for <see cref="SunfishTextArea"/>.
/// Renders the textarea bound to a sample value with an accessible name supplied via
/// <c>aria-label</c>; asserts axe surfaces zero moderate+ violations.
/// </summary>
public class SunfishTextAreaA11yTests : IClassFixture<SunfishTextAreaA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishTextAreaA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishTextArea_BoundValueWithAriaLabel_HasNoModeratePlusAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishTextArea>(p => p
            .Add(c => c.Value, "Sample comment text")
            .Add(c => c.Placeholder, "Enter a comment")
            .Add(c => c.Rows, 4)
            .AddUnmatched("aria-label", "Comment"));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishTextArea surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
                string.Join(", ", moderatePlus.Select(v => v.Id)));
        }
        finally { await page.CloseAsync(); }
    }

    public sealed class Ctx : IDisposable
    {
        public BunitContext Bunit { get; }

        public Ctx()
        {
            Bunit = new BunitContext();
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());
        }

        public async Task<Microsoft.Playwright.IPage> NewPageAsync()
        {
            var host = await PlaywrightPageHost.GetAsync();
            return await host.NewPageAsync(new CultureInfo("en-US"));
        }

        public void Dispose() => Bunit.Dispose();
    }
}
