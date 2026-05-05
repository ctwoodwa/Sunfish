using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    /// <summary>
    /// Registers the full feature-management stack with
    /// <see cref="WayfinderFeatureProvider"/> as the active
    /// <see cref="IFeatureProvider"/>. Requires <c>AddSunfishWayfinder()</c>
    /// (or another <see cref="Wayfinder.IAtlasProjector"/> registration) on
    /// the same <see cref="IServiceCollection"/>. Per ADR 0009 §A1.4.
    /// </summary>
    /// <remarks>
    /// Microsoft DI resolves the last <see cref="IFeatureProvider"/>
    /// registration when the consumer asks for a single instance — this
    /// method calls <see cref="AddSunfishFeatureManagement"/> first (which
    /// installs <see cref="InMemoryFeatureProvider"/>) and then re-registers
    /// <see cref="WayfinderFeatureProvider"/>, so the latter wins.
    /// </remarks>
    public static IServiceCollection AddSunfishFeatureManagementWithWayfinder(
        this IServiceCollection services)
    {
        services.AddSunfishFeatureManagement();
        services.Replace(ServiceDescriptor.Singleton<IFeatureProvider, WayfinderFeatureProvider>());
        return services;
    }

    /// <summary>
    /// Registers <see cref="WayfinderFeatureProvider"/> as the
    /// <see cref="IFeatureProvider"/>. Use when
    /// <see cref="AddSunfishFeatureManagement"/> was already called and only
    /// the provider needs to be swapped. Per ADR 0009 §A1.4.
    /// </summary>
    public static IServiceCollection AddWayfinderFeatureProvider(
        this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IFeatureProvider, WayfinderFeatureProvider>());
        return services;
    }
}
