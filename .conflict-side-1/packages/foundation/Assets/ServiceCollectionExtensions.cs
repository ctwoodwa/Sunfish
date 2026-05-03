using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Hierarchy;
using Sunfish.Foundation.Assets.Versions;

namespace Sunfish.Foundation.Assets;

/// <summary>
/// DI registration helpers for the Sunfish asset-modeling kernel primitives.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory backend: shared <see cref="InMemoryAssetStorage"/>, plus the
    /// four primitive services (<see cref="IEntityStore"/>, <see cref="IVersionStore"/>,
    /// <see cref="IAuditLog"/>, <see cref="IHierarchyService"/>) and
    /// <see cref="HierarchyOperations"/> as singletons.
    /// </summary>
    /// <remarks>
    /// Null-object defaults are registered for the three extensibility seams
    /// (<see cref="IEntityValidator"/>, <see cref="IVersionObserver"/>,
    /// <see cref="IAuditContextProvider"/>); consumers can override them via
    /// <c>services.Replace(...)</c> or direct <c>TryAddSingleton</c> / <c>AddSingleton</c>.
    /// </remarks>
    public static IServiceCollection AddSunfishAssetsInMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<InMemoryAssetStorage>();
        services.TryAddSingleton<IEntityValidator>(NullEntityValidator.Instance);
        services.TryAddSingleton<IVersionObserver>(NullVersionObserver.Instance);
        services.TryAddSingleton<IAuditContextProvider>(NullAuditContextProvider.Instance);

        services.TryAddSingleton<IEntityStore>(sp => new InMemoryEntityStore(
            sp.GetRequiredService<InMemoryAssetStorage>(),
            sp.GetService<IEntityValidator>(),
            sp.GetService<IVersionObserver>()));

        services.TryAddSingleton<IVersionStore>(sp => new InMemoryVersionStore(
            sp.GetRequiredService<InMemoryAssetStorage>()));

        services.TryAddSingleton<IAuditLog>(sp => new InMemoryAuditLog(
            sp.GetRequiredService<InMemoryAssetStorage>()));

        services.TryAddSingleton<IHierarchyService>(_ => new InMemoryHierarchyService());

        services.TryAddSingleton(sp => new HierarchyOperations(
            sp.GetRequiredService<IEntityStore>(),
            sp.GetRequiredService<IHierarchyService>(),
            sp.GetRequiredService<IAuditLog>()));

        return services;
    }
}
