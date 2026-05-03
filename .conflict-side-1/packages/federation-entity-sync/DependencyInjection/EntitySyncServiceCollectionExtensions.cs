using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Federation.EntitySync.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish federation entity-sync services.
/// </summary>
public static class EntitySyncServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IChangeStore"/> (in-memory) and <see cref="IEntitySyncer"/> (in-memory)
    /// as singletons. Requires <c>AddSunfishFederation</c> (from
    /// <c>Sunfish.Federation.Common</c>) to have been called first so that <c>ISyncTransport</c>
    /// and the Foundation signer/verifier are in the container.
    /// </summary>
    public static IServiceCollection AddSunfishEntitySync(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IChangeStore, InMemoryChangeStore>();
        services.AddSingleton<IEntitySyncer, InMemoryEntitySyncer>();
        return services;
    }
}
