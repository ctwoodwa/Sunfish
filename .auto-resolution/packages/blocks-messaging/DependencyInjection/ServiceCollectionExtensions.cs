using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.Messaging.Data;
using Sunfish.Blocks.Messaging.Services;
using Sunfish.Foundation.Integrations.Messaging;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.Messaging.DependencyInjection;

/// <summary>
/// DI registration for the in-memory messaging substrate. Registers
/// <see cref="IThreadStore"/>, the no-op <see cref="IMessagingGateway"/>,
/// and the <see cref="ISunfishEntityModule"/> contribution.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the in-memory messaging substrate (thread store + gateway stub + entity-module contribution).</summary>
    public static IServiceCollection AddInMemoryMessaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<InMemoryThreadStore>();
        services.TryAddSingleton<IThreadStore>(sp => sp.GetRequiredService<InMemoryThreadStore>());
        services.TryAddSingleton<IMessagingGateway, InMemoryMessagingGateway>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISunfishEntityModule, MessagingEntityModule>());

        return services;
    }
}
