using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Inspections.Services;

namespace Sunfish.Blocks.Inspections.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish inspection-management services.
/// </summary>
public static class InspectionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IInspectionsService"/> as a singleton backed by
    /// <see cref="InMemoryInspectionsService"/>.
    /// Suitable for development, testing, and demo scenarios.
    /// Replace with a persistence-backed implementation for production.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryInspections(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IInspectionsService, InMemoryInspectionsService>();
        return services;
    }
}
