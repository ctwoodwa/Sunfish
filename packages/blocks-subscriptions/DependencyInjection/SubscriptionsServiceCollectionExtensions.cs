using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Subscriptions.Data;
using Sunfish.Blocks.Subscriptions.Services;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.Subscriptions.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish subscription-management services.
/// </summary>
public static class SubscriptionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISubscriptionService"/> as a singleton backed by
    /// <see cref="InMemorySubscriptionService"/> and contributes the block's
    /// <see cref="ISunfishEntityModule"/> (<see cref="SubscriptionsEntityModule"/>)
    /// so Bridge can apply the EF Core entity configurations.
    /// Suitable for development, testing, and demo scenarios.
    /// Replace the <see cref="ISubscriptionService"/> registration with a
    /// persistence-backed implementation for production.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemorySubscriptions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISubscriptionService, InMemorySubscriptionService>();
        services.AddSingleton<ISunfishEntityModule, SubscriptionsEntityModule>();
        return services;
    }
}
