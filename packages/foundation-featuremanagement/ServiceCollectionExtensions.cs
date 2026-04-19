using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>DI conveniences for <see cref="Sunfish.Foundation.FeatureManagement"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default feature-management stack: in-memory catalog +
    /// in-memory provider + no-op entitlement resolver + <see cref="DefaultFeatureEvaluator"/>.
    /// Callers replace individual services as needed.
    /// </summary>
    public static IServiceCollection AddSunfishFeatureManagement(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryFeatureCatalog>();
        services.AddSingleton<IFeatureCatalog>(sp => sp.GetRequiredService<InMemoryFeatureCatalog>());

        services.AddSingleton<InMemoryFeatureProvider>();
        services.AddSingleton<IFeatureProvider>(sp => sp.GetRequiredService<InMemoryFeatureProvider>());

        services.AddSingleton<IEntitlementResolver, NoOpEntitlementResolver>();
        services.AddSingleton<IFeatureEvaluator, DefaultFeatureEvaluator>();

        return services;
    }
}
