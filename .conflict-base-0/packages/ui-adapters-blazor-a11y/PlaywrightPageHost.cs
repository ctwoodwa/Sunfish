using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Sunfish.UIAdapters.Blazor.A11y;

/// <summary>
/// CVD (Color Vision Deficiency) emulation modes for axe testing per spec §7 +
/// <see href="https://chromedevtools.github.io/devtools-protocol/tot/Emulation/#method-setEmulatedVisionDeficiency"/>.
/// </summary>
public enum CvdMode
{
    None,
    Deuteranopia,
    Protanopia,
    Tritanopia,
    Achromatopsia,
}

/// <summary>
/// Shared Playwright host for the bUnit-to-axe bridge. Launches one chromium headless
/// browser per test assembly via <see cref="GetAsync"/>; tests obtain fresh
/// <see cref="IPage"/> instances per scenario through <see cref="NewPageAsync"/> so
/// CDP-level emulation (locale, RTL, color-scheme, CVD) is applied without mutating
/// shared state across parallel test workers.
/// </summary>
/// <remarks>
/// Implements <see cref="IAsyncDisposable"/>. Wire into xUnit via a
/// <c>[CollectionDefinition]</c> + <c>ICollectionFixture&lt;PlaywrightPageHost&gt;</c>
/// pattern so the singleton is constructed lazily on first use and torn down at
/// assembly shutdown.
/// </remarks>
public sealed class PlaywrightPageHost : IAsyncDisposable
{
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static PlaywrightPageHost? _instance;

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private PlaywrightPageHost() { }

    /// <summary>Lazy singleton accessor; builds on first call, returns cached afterward.</summary>
    public static async Task<PlaywrightPageHost> GetAsync()
    {
        if (_instance is not null) return _instance;
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_instance is not null) return _instance;
            var host = new PlaywrightPageHost();
            await host.InitializeAsync().ConfigureAwait(false);
            _instance = host;
            return host;
        }
        finally { _lock.Release(); }
    }

    private async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" },
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Create a fresh isolated browser context + page configured for the given scenario.
    /// </summary>
    /// <param name="culture">BCP-47 locale; sets the browser's <c>Accept-Language</c> + page direction inference.</param>
    /// <param name="rtl">Whether to set <c>dir="rtl"</c> on the document root after content is loaded.</param>
    /// <param name="theme"><c>"light"</c> or <c>"dark"</c>; maps to CSS <c>prefers-color-scheme</c>.</param>
    /// <param name="cvd">Color-vision deficiency emulation; <see cref="CvdMode.None"/> disables emulation.</param>
    public async Task<IPage> NewPageAsync(CultureInfo culture, bool rtl = false, string theme = "light", CvdMode cvd = CvdMode.None)
    {
        if (_browser is null)
            throw new InvalidOperationException("PlaywrightPageHost not initialized. Use GetAsync().");

        var contextOptions = new BrowserNewContextOptions
        {
            Locale = culture.Name,
            ColorScheme = theme.Equals("dark", StringComparison.OrdinalIgnoreCase) ? ColorScheme.Dark : ColorScheme.Light,
            ReducedMotion = ReducedMotion.NoPreference,
        };
        var context = await _browser.NewContextAsync(contextOptions).ConfigureAwait(false);

        if (cvd != CvdMode.None)
        {
            // CDP — chromium-only. Tritanopia/protanopia/deuteranopia/achromatopsia map
            // 1:1 to Emulation.setEmulatedVisionDeficiency type strings (lowercase enum
            // name). https://chromedevtools.github.io/devtools-protocol/tot/Emulation/#method-setEmulatedVisionDeficiency
            var session = await context.NewCDPSessionAsync(await context.NewPageAsync().ConfigureAwait(false)).ConfigureAwait(false);
            await session.SendAsync("Emulation.setEmulatedVisionDeficiency", new()
            {
                ["type"] = cvd.ToString().ToLowerInvariant(),
            }).ConfigureAwait(false);
        }

        var page = await context.NewPageAsync().ConfigureAwait(false);

        // The browser's Accept-Language drives `navigator.language`; setting `html[dir]`
        // is content-dependent and applied by callers (or by the AxeRunner HTML wrapper).
        if (rtl)
        {
            await page.AddInitScriptAsync(@"
                document.addEventListener('DOMContentLoaded', () => {
                    document.documentElement.setAttribute('dir', 'rtl');
                });
            ").ConfigureAwait(false);
        }

        return page;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync().ConfigureAwait(false);
        _playwright?.Dispose();
        _instance = null;
    }
}
