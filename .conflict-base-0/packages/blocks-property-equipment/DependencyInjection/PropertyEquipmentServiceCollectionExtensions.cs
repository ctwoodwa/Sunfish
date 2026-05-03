using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.PropertyEquipment.Data;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.PropertyEquipment.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish property-equipment services.
/// </summary>
public static class PropertyEquipmentServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory property-equipment surface:
    /// <list type="bullet">
    ///   <item><see cref="IEquipmentLifecycleEventStore"/> → <see cref="InMemoryEquipmentLifecycleEventStore"/></item>
    ///   <item><see cref="IEquipmentRepository"/> → <see cref="InMemoryEquipmentRepository"/> (depends on the event store for soft-delete emission)</item>
    ///   <item><see cref="ISunfishEntityModule"/> → <see cref="PropertyEquipmentEntityModule"/></item>
    /// </list>
    /// Suitable for testing, prototyping, and kitchen-sink demos. Replace
    /// with a persistence-backed <see cref="IEquipmentRepository"/> in
    /// production hosts.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryPropertyEquipment(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IEquipmentLifecycleEventStore, InMemoryEquipmentLifecycleEventStore>();
        services.AddSingleton<IEquipmentRepository, InMemoryEquipmentRepository>();
        services.AddSingleton<ISunfishEntityModule, PropertyEquipmentEntityModule>();

        return services;
    }
}
