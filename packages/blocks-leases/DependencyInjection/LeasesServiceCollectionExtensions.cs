using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Foundation.Localization;

namespace Sunfish.Blocks.Leases.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish lease-management services.
/// </summary>
public static class LeasesServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ILeaseService"/> as a singleton backed by
    /// <see cref="InMemoryLeaseService"/>.
    /// Suitable for development, testing, and demo scenarios.
    /// Replace with a persistence-backed implementation for production.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryLeases(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ILeaseService, InMemoryLeaseService>();

        // Wave 2 Cluster C — Plan 2 Task 3.5: register the open-generic Sunfish localizer
        // so consumers can resolve IStringLocalizer-equivalents against this block's
        // SharedResource bundle. Idempotent via TryAddSingleton.
        services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));

        return services;
    }
}
