using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Hierarchy;
using Sunfish.Foundation.Assets.Postgres.Audit;
using Sunfish.Foundation.Assets.Postgres.Entities;
using Sunfish.Foundation.Assets.Postgres.Hierarchy;
using Sunfish.Foundation.Assets.Postgres.Versions;
using Sunfish.Foundation.Assets.Versions;

namespace Sunfish.Foundation.Assets.Postgres.DependencyInjection;

/// <summary>DI registration helpers for the Postgres asset-store backend.</summary>
public static class PostgresAssetStoreExtensions
{
    /// <summary>
    /// Registers the Postgres-backed <see cref="IEntityStore"/>, <see cref="IVersionStore"/>,
    /// <see cref="IAuditLog"/>, and <see cref="IHierarchyService"/> against the supplied
    /// Npgsql <paramref name="connectionString"/>.
    /// </summary>
    /// <remarks>
    /// Registers an <see cref="IDbContextFactory{TContext}"/> for
    /// <see cref="AssetStoreDbContext"/> so each store gets a fresh unit-of-work per call.
    /// Null-object defaults are registered for the three extensibility seams
    /// (<see cref="IEntityValidator"/>, <see cref="IVersionObserver"/>,
    /// <see cref="IAuditContextProvider"/>); consumers can override them via
    /// <c>services.Replace(...)</c>.
    /// </remarks>
    public static IServiceCollection AddSunfishAssetsPostgres(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        services.AddDbContextFactory<AssetStoreDbContext>(opt => opt.UseNpgsql(connectionString));

        services.TryAddSingleton<IEntityValidator>(NullEntityValidator.Instance);
        services.TryAddSingleton<IVersionObserver>(NullVersionObserver.Instance);
        services.TryAddSingleton<IAuditContextProvider>(NullAuditContextProvider.Instance);

        services.TryAddSingleton<IEntityStore>(sp => new PostgresEntityStore(
            sp.GetRequiredService<IDbContextFactory<AssetStoreDbContext>>(),
            sp.GetService<IEntityValidator>(),
            sp.GetService<IVersionObserver>()));

        services.TryAddSingleton<IVersionStore>(sp => new PostgresVersionStore(
            sp.GetRequiredService<IDbContextFactory<AssetStoreDbContext>>()));

        services.TryAddSingleton<IAuditLog>(sp => new PostgresAuditLog(
            sp.GetRequiredService<IDbContextFactory<AssetStoreDbContext>>()));

        services.TryAddSingleton<IHierarchyService>(sp => new PostgresHierarchyService(
            sp.GetRequiredService<IDbContextFactory<AssetStoreDbContext>>()));

        services.TryAddSingleton(sp => new HierarchyOperations(
            sp.GetRequiredService<IEntityStore>(),
            sp.GetRequiredService<IHierarchyService>(),
            sp.GetRequiredService<IAuditLog>()));

        return services;
    }
}
