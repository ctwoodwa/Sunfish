using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.MultiTenancy;

/// <summary>DI conveniences for <see cref="Sunfish.Foundation.MultiTenancy"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="InMemoryTenantCatalog"/> as a singleton and exposes it
    /// as both <see cref="ITenantCatalog"/> and <see cref="ITenantResolver"/>.
    /// </summary>
    public static IServiceCollection AddSunfishTenantCatalog(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryTenantCatalog>();
        services.AddSingleton<ITenantCatalog>(sp => sp.GetRequiredService<InMemoryTenantCatalog>());
        services.AddSingleton<ITenantResolver>(sp => sp.GetRequiredService<InMemoryTenantCatalog>());
        return services;
    }
}
