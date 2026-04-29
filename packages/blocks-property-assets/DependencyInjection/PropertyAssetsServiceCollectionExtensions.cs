using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.PropertyAssets.Data;
using Sunfish.Blocks.PropertyAssets.Services;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.PropertyAssets.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish property-assets services.
/// </summary>
public static class PropertyAssetsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory property-assets surface:
    /// <list type="bullet">
    ///   <item><see cref="IAssetLifecycleEventStore"/> → <see cref="InMemoryAssetLifecycleEventStore"/></item>
    ///   <item><see cref="IAssetRepository"/> → <see cref="InMemoryAssetRepository"/> (depends on the event store for soft-delete emission)</item>
    ///   <item><see cref="ISunfishEntityModule"/> → <see cref="PropertyAssetsEntityModule"/></item>
    /// </list>
    /// Suitable for testing, prototyping, and kitchen-sink demos. Replace
    /// with a persistence-backed <see cref="IAssetRepository"/> in
    /// production hosts.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryPropertyAssets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAssetLifecycleEventStore, InMemoryAssetLifecycleEventStore>();
        services.AddSingleton<IAssetRepository, InMemoryAssetRepository>();
        services.AddSingleton<ISunfishEntityModule, PropertyAssetsEntityModule>();

        return services;
    }
}
