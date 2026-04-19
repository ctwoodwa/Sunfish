using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Maintenance.Services;

namespace Sunfish.Blocks.Maintenance.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish maintenance-management services.
/// </summary>
public static class MaintenanceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMaintenanceService"/> as a singleton backed by
    /// <see cref="InMemoryMaintenanceService"/>.
    /// Suitable for development, testing, and demo scenarios.
    /// Replace with a persistence-backed implementation for production.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryMaintenance(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IMaintenanceService, InMemoryMaintenanceService>();
        return services;
    }
}
