using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.LocalFirst;

/// <summary>DI conveniences for <see cref="Sunfish.Foundation.LocalFirst"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory <see cref="IOfflineStore"/>, <see cref="IOfflineQueue"/>,
    /// and the default <see cref="LastWriterWinsConflictResolver"/>. Sync engine,
    /// export, and import services are bundle / accelerator concerns and are
    /// not registered here.
    /// </summary>
    public static IServiceCollection AddSunfishLocalFirst(this IServiceCollection services)
    {
        services.AddSingleton<IOfflineStore, InMemoryOfflineStore>();
        services.AddSingleton<IOfflineQueue, InMemoryOfflineQueue>();
        services.AddSingleton<ISyncConflictResolver, LastWriterWinsConflictResolver>();
        return services;
    }
}
