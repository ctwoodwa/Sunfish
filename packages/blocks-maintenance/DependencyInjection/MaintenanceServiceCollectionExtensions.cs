using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Localization;

namespace Sunfish.Blocks.Maintenance.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish maintenance-management services.
/// </summary>
public static class MaintenanceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMaintenanceService"/> as a singleton backed by
    /// <see cref="InMemoryMaintenanceService"/>.
    /// Also contributes the open-generic <see cref="ISunfishLocalizer{T}"/> binding
    /// so consumers can resolve the maintenance <c>SharedResource</c> bundle. Caller
    /// is responsible for wiring <c>services.AddLocalization()</c> in the composition
    /// root (matches the cluster-A sentinel pattern; class libraries don't take a
    /// hard PackageReference on <c>Microsoft.Extensions.Localization</c>).
    /// Suitable for development, testing, and demo scenarios.
    /// Replace with a persistence-backed implementation for production.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryMaintenance(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IMaintenanceService, InMemoryMaintenanceService>();
        services.AddSingleton<IVendorContactService, InMemoryVendorContactService>();
        services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));
        return services;
    }
}
