using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.Extensions;

public class SunfishBuilder
{
    public IServiceCollection Services { get; }

    public SunfishBuilder(IServiceCollection services)
    {
        Services = services;
    }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Sunfish foundation services and applies optional configuration.
    /// Registers <see cref="ISunfishThemeService"/> and <see cref="ISunfishNotificationService"/>
    /// automatically. Call on the returned <see cref="SunfishBuilder"/> to add adapter-specific
    /// services (e.g., Blazor interop) via extension methods in the adapter packages.
    /// </summary>
    public static SunfishBuilder AddSunfish(this IServiceCollection services, Action<SunfishOptions>? configure = null)
    {
        var options = new SunfishOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        services.AddScoped<ISunfishNotificationService, SunfishNotificationService>();
        return new SunfishBuilder(services);
    }
}
