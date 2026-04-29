using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.Foundation.Recovery.TenantKey;

/// <summary>DI registration for <see cref="ITenantKeyProvider"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the W#20 Phase 0 in-memory <see cref="ITenantKeyProvider"/> stub. ADR 0046 Stage 06 will replace.</summary>
    public static IServiceCollection AddInMemoryTenantKeyProvider(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ITenantKeyProvider, InMemoryTenantKeyProvider>();
        return services;
    }
}
