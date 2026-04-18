using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Federation.CapabilitySync.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish federation capability-sync services.
/// </summary>
public static class CapabilitySyncServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ICapabilityOpStore"/> (in-memory) as a singleton. Note that
    /// <see cref="ICapabilitySyncer"/> is registered by the host, because the in-memory
    /// implementation needs an application-specific peer-store resolver; production wiring
    /// will land with the HTTP transport follow-up (D-4-http).
    /// </summary>
    public static IServiceCollection AddSunfishCapabilitySync(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ICapabilityOpStore, InMemoryCapabilityOpStore>();
        return services;
    }
}
