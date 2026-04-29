using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.Taxonomy.Services;

namespace Sunfish.Foundation.Taxonomy.DependencyInjection;

/// <summary>
/// DI registration for the foundation-tier taxonomy substrate (ADR 0056).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory <see cref="ITaxonomyRegistry"/> + <see cref="ITaxonomyResolver"/>
    /// reference implementations. Both are singletons; the resolver reads
    /// from the registry. Production hosts should override
    /// <see cref="ITaxonomyRegistry"/> with a durable implementation when one
    /// ships (Phase 2+).
    /// </summary>
    /// <remarks>
    /// Callers must also register an <see cref="Sunfish.Kernel.Audit.IAuditTrail"/>
    /// implementation (typically via <c>AddSunfishKernelAudit</c>); the registry
    /// emits audit records for each lifecycle operation per ADR 0049.
    /// </remarks>
    public static IServiceCollection AddInMemoryTaxonomy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<InMemoryTaxonomyRegistry>();
        services.TryAddSingleton<ITaxonomyRegistry>(sp => sp.GetRequiredService<InMemoryTaxonomyRegistry>());
        services.TryAddSingleton<ITaxonomyResolver>(sp => new InMemoryTaxonomyResolver(sp.GetRequiredService<InMemoryTaxonomyRegistry>()));
        return services;
    }
}
