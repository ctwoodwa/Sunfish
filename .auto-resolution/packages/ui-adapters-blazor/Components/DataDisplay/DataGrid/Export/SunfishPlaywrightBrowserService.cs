using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;

namespace Sunfish.UIAdapters.Blazor;

// Singleton IHostedService that owns the Chromium browser process lifetime.
// Requires browser binaries: run `playwright install chromium` on each environment.
internal sealed class SunfishPlaywrightBrowserService : IHostedService, IAsyncDisposable
{
    internal SemaphoreSlim Semaphore { get; } = new(5, 5);
    internal IBrowser Browser { get; private set; } = null!;

    private IPlaywright? _playwright;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public Task StopAsync(CancellationToken cancellationToken) => DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        Semaphore.Dispose();
    }
}
