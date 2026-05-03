using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.TenantAdmin.Data;
using Sunfish.Blocks.TenantAdmin.Services;
using Sunfish.Foundation.Localization;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.TenantAdmin.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish tenant-admin services and its
/// ADR-0015 entity module.
/// </summary>
public static class TenantAdminServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ITenantAdminService"/> as a singleton backed by
    /// <see cref="InMemoryTenantAdminService"/>, and the
    /// <see cref="TenantAdminEntityModule"/> so Bridge's shared DbContext picks
    /// up the block's entity configurations per ADR 0015.
    /// Suitable for development, testing, and demo scenarios.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryTenantAdmin(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ITenantAdminService, InMemoryTenantAdminService>();
        services.AddSingleton<ISunfishEntityModule, TenantAdminEntityModule>();

        // Wave 2 Cluster C — Plan 2 Task 3.5: register the open-generic Sunfish localizer
        // so consumers can resolve IStringLocalizer-equivalents against this block's
        // SharedResource bundle. Idempotent via TryAddSingleton.
        services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));

        return services;
    }
}
