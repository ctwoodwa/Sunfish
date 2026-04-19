using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Leases.Services;

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
        return services;
    }
}
