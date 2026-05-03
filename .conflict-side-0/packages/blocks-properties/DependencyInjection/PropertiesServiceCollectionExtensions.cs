using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Properties.Data;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.Properties.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish properties services.
/// </summary>
public static class PropertiesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory properties surface:
    /// <list type="bullet">
    ///   <item><see cref="IPropertyRepository"/> → <see cref="InMemoryPropertyRepository"/></item>
    ///   <item><see cref="ISunfishEntityModule"/> → <see cref="PropertiesEntityModule"/></item>
    /// </list>
    /// Suitable for testing, prototyping, and kitchen-sink demos. Replace
    /// with a persistence-backed <see cref="IPropertyRepository"/> in
    /// production hosts.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryProperties(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPropertyRepository, InMemoryPropertyRepository>();
        services.AddSingleton<ISunfishEntityModule, PropertiesEntityModule>();

        return services;
    }
}
