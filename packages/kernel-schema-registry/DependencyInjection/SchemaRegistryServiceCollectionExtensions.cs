using Microsoft.Extensions.DependencyInjection;

using Sunfish.Kernel.Schema;

namespace Sunfish.Kernel.Schema.DependencyInjection;

/// <summary>
/// DI extensions for registering the Sunfish kernel schema registry (spec §3.4).
/// </summary>
public static class SchemaRegistryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory schema registry backend as a singleton
    /// <see cref="ISchemaRegistry"/>. Does NOT register <c>IBlobStore</c> —
    /// the caller must have already registered one (typically via
    /// <c>AddSunfish</c> or an explicit
    /// <c>services.AddSingleton&lt;IBlobStore, FileSystemBlobStore&gt;(...)</c>).
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishKernelSchemaRegistry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISchemaRegistry, InMemorySchemaRegistry>();
        return services;
    }
}
