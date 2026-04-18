using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Federation.Common.Extensions;

/// <summary>
/// DI extension methods for registering Sunfish federation services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FederationOptions"/>, the federation startup-check hosted service,
    /// an in-memory peer registry, and the in-memory sync transport. Production deployments swap
    /// the transport with the HTTP transport registered by Task D-3.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="configure">Callback to configure <see cref="FederationOptions"/>.</param>
    public static IServiceCollection AddSunfishFederation(
        this IServiceCollection services,
        Action<FederationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddHostedService<FederationStartupChecks>();
        services.AddSingleton<IPeerRegistry, InMemoryPeerRegistry>();
        services.AddSingleton<ISyncTransport, InMemorySyncTransport>();
        return services;
    }
}
