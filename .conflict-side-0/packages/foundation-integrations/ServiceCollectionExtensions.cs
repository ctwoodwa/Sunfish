using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.Integrations;

/// <summary>DI conveniences for <see cref="Sunfish.Foundation.Integrations"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default in-memory provider registry, webhook dispatcher,
    /// and sync-cursor store. Provider adapter packages register themselves
    /// into these services at startup.
    /// </summary>
    public static IServiceCollection AddSunfishIntegrations(this IServiceCollection services)
    {
        services.AddSingleton<IProviderRegistry, InMemoryProviderRegistry>();
        services.AddSingleton<IWebhookEventDispatcher, InMemoryWebhookEventDispatcher>();
        services.AddSingleton<ISyncCursorStore, InMemorySyncCursorStore>();
        return services;
    }
}
