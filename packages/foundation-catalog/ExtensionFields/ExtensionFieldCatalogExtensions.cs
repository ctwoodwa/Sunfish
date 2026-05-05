using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Extensibility;
using Sunfish.Foundation.FeatureManagement;
using Sunfish.Foundation.Migration;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Generic and DI conveniences for <see cref="IExtensionFieldCatalog"/>.
/// </summary>
public static class ExtensionFieldCatalogExtensions
{
    /// <summary>Registers a field scoped to <typeparamref name="TEntity"/>.</summary>
    public static void Register<TEntity>(this IExtensionFieldCatalog catalog, ExtensionFieldSpec spec)
        where TEntity : IHasExtensionData
        => catalog.Register(typeof(TEntity), spec);

    /// <summary>Returns every field registered for <typeparamref name="TEntity"/>.</summary>
#pragma warning disable CS0618 // sync GetFields is the consumer of this generic helper; obsolete warning is overly broad here.
    public static IReadOnlyList<ExtensionFieldSpec> GetFields<TEntity>(this IExtensionFieldCatalog catalog)
        where TEntity : IHasExtensionData
        => catalog.GetFields(typeof(TEntity));
#pragma warning restore CS0618

    /// <summary>Registers <see cref="ExtensionFieldCatalog"/> as a singleton <see cref="IExtensionFieldCatalog"/>.</summary>
    public static IServiceCollection AddSunfishExtensionFieldCatalog(this IServiceCollection services)
    {
        services.AddSingleton<IExtensionFieldCatalog, ExtensionFieldCatalog>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="ExtensionFieldCatalog"/> wired with feature-gate
    /// evaluation, audit emission, sequestration, and capability-graph
    /// dependencies (all resolved as optional via <c>GetService</c>). Per
    /// ADR 0075 §Lazy-DI optionality — null dependencies leave the
    /// corresponding gate path inactive.
    /// </summary>
    public static IServiceCollection AddExtensionFieldCatalogWithFeatureGating(
        this IServiceCollection services)
    {
        services.AddSingleton<IExtensionFieldCatalog>(sp => new ExtensionFieldCatalog(
            featureEvaluator: sp.GetService<IFeatureEvaluator>(),
            auditTrail: sp.GetService<IAuditTrail>(),
            sequestrationStore: sp.GetService<ISequestrationStore>(),
            capabilityGraph: sp.GetService<ICapabilityGraph>(),
            signer: sp.GetService<IOperationSigner>(),
            clock: sp.GetService<TimeProvider>()));
        return services;
    }
}
