using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.UIAdapters.Blazor;

/// <summary>
/// Extension methods for registering Playwright-based PDF export with SunfishDataGrid.
/// </summary>
public static class SunfishPdfServiceExtensions
{
    /// <summary>
    /// Registers the Playwright PDF export service and its singleton Chromium browser
    /// lifecycle. Call once in your application's DI setup to enable PDF export on all
    /// SunfishDataGrid instances.
    /// </summary>
    /// <remarks>
    /// Requires Chromium browser binaries to be installed on the host machine.
    /// Run <c>playwright install chromium</c> (or
    /// <c>dotnet tool run playwright install chromium</c>) once per environment.
    /// </remarks>
    public static IServiceCollection AddSunfishPlaywrightPdf(this IServiceCollection services)
    {
        services.AddSingleton<SunfishPlaywrightBrowserService>();
        services.AddHostedService(sp => sp.GetRequiredService<SunfishPlaywrightBrowserService>());
        services.AddScoped<Components.DataDisplay.IPdfExportWriter,
                           Components.DataDisplay.PlaywrightPdfExportWriter>();
        return services;
    }
}
