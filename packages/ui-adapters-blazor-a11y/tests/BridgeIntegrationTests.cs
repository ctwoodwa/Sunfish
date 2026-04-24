using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Sunfish.UIAdapters.Blazor.A11y.Tests.Fixtures;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests;

/// <summary>
/// End-to-end smoke for Plan 4 Tasks 1.2 + 1.4: render a Sunfish bUnit fragment,
/// host its markup in a Playwright chromium page via <see cref="PlaywrightPageHost"/>,
/// inject axe-core via <see cref="AxeRunner"/>, and assert the typed result.
/// </summary>
/// <remarks>
/// Requires Playwright's chromium browser to be available. CI installs via
/// <c>playwright.ps1 install chromium</c> after build; local dev uses the same
/// step. If chromium is missing, the test surfaces a Playwright initialization
/// failure rather than a silent skip — the bridge cannot validate without it.
/// </remarks>
public class BridgeIntegrationTests : IAsyncLifetime
{
    private BunitContext _ctx = null!;

    public Task InitializeAsync()
    {
        _ctx = new BunitContext();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _ctx.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SimpleTextFixture_RoundTrips_ZeroViolationsAtModeratePlus()
    {
        var rendered = _ctx.Render<SimpleTextFixture>(p => p
            .Add(c => c.Title, "Bridge Integration")
            .Add(c => c.Body, "Bridge runs end-to-end."));

        var host = await PlaywrightPageHost.GetAsync();
        var page = await host.NewPageAsync(new CultureInfo("en-US"));
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);

            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();

            Assert.Empty(moderatePlus);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task WrapInHtmlDocument_ProducesValidHtml5()
    {
        const string fragment = "<button>OK</button>";
        var html = AxeRunner.WrapInHtmlDocument(fragment);

        Assert.Contains("<!doctype html>", html);
        Assert.Contains("<meta charset=\"utf-8\">", html);
        Assert.Contains(fragment, html);
        Assert.Contains("</html>", html);
    }

    [Fact]
    public async Task WrapInHtmlDocument_InjectsInlineThemeCss()
    {
        const string fragment = "<button>OK</button>";
        const string inlineCss = "button { color: red; }";
        var html = AxeRunner.WrapInHtmlDocument(fragment, inlineCss);

        Assert.Contains($"<style>{inlineCss}</style>", html);
        Assert.DoesNotContain("<link rel=\"stylesheet\"", html);
    }

    [Fact]
    public async Task WrapInHtmlDocument_InjectsThemeStylesheetUrl()
    {
        const string fragment = "<button>OK</button>";
        const string themeUrl = "https://cdn.example.com/theme.css";
        var html = AxeRunner.WrapInHtmlDocument(fragment, themeUrl);

        Assert.Contains($"<link rel=\"stylesheet\" href=\"{themeUrl}\">", html);
        Assert.DoesNotContain("<style>", html);
    }
}
