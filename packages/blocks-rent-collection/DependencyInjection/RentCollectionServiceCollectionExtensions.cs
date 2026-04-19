using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.RentCollection.Services;

namespace Sunfish.Blocks.RentCollection.DependencyInjection;

/// <summary>
/// Extension methods for registering rent-collection services in a
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class RentCollectionServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="InMemoryRentCollectionService"/> as the
    /// <see cref="IRentCollectionService"/> implementation.
    /// Suitable for testing, prototyping, and kitchen-sink demos.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInMemoryRentCollection(this IServiceCollection services)
    {
        services.AddSingleton<IRentCollectionService, InMemoryRentCollectionService>();
        return services;
    }
}
