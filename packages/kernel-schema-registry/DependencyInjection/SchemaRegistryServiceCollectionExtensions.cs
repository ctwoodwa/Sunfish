using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Sunfish.Kernel.Schema;
using Sunfish.Kernel.SchemaRegistry.Epochs;
using Sunfish.Kernel.SchemaRegistry.Lenses;
using Sunfish.Kernel.SchemaRegistry.Upcasters;

namespace Sunfish.Kernel.Schema.DependencyInjection;

/// <summary>
/// DI extensions for registering the Sunfish kernel schema registry (spec §3.4) and
/// the schema-migration surface added in paper §7.3 / §7.4 (bidirectional lenses,
/// upcasters, epoch coordinator).
/// </summary>
public static class SchemaRegistryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory schema registry backend as a singleton
    /// <see cref="ISchemaRegistry"/>, along with the migration primitives required by
    /// paper §7 (a <see cref="LensGraph"/>, an <see cref="UpcasterChain"/>, and an
    /// <see cref="IEpochCoordinator"/>). Does NOT register <c>IBlobStore</c> — the
    /// caller must have already registered one (typically via <c>AddSunfish</c> or an
    /// explicit <c>services.AddSingleton&lt;IBlobStore, FileSystemBlobStore&gt;(...)</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lens graph, upcaster chain, and epoch coordinator are registered as
    /// shared singletons so downstream components (migrators, rehydrators,
    /// sync-daemon cut-over watchers) can depend on them directly without resolving
    /// the full <see cref="ISchemaRegistry"/>.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishKernelSchemaRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSunfishSchemaMigration();
        services.TryAddSingleton<ISchemaRegistry, InMemorySchemaRegistry>();
        return services;
    }

    /// <summary>
    /// Registers the schema-migration primitives (lens graph, upcaster chain, epoch
    /// coordinator) without registering the <see cref="ISchemaRegistry"/> itself.
    /// Intended for callers who want to wire a custom registry implementation but
    /// reuse the standard migration surface.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishSchemaMigration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<LensGraph>();
        services.TryAddSingleton<UpcasterChain>();
        services.TryAddSingleton<IEpochCoordinator, EpochCoordinator>();
        return services;
    }
}
