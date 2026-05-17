using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.People.Foundation.Migration;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.People.Foundation.DependencyInjection;

/// <summary>
/// DI registration helpers for the people-foundation cluster. Apps include
/// the cluster via <c>services.AddBlocksPeopleFoundation()</c>; tests usually
/// new up <see cref="InMemoryPartyRepository"/> directly.
/// </summary>
public static class PeopleFoundationServiceCollectionExtensions
{
    /// <summary>
    /// Register the in-memory Party substrate. One concrete repository
    /// instance backs both <see cref="IPartyReadModel"/> and
    /// <see cref="IPartyWriteService"/> — registration uses a singleton +
    /// two factory bindings so the read/write split is observable in
    /// downstream consumers without duplicating state.
    ///
    /// <para>
    /// Registers <see cref="NoopDomainEventPublisher"/> as the default
    /// <see cref="IDomainEventPublisher"/> binding via
    /// <c>TryAddSingleton</c> — hosts that wire a real publisher upstream
    /// (foundation-events SqliteEventReader, etc.) are unaffected.
    /// </para>
    /// </summary>
    public static IServiceCollection AddBlocksPeopleFoundation(this IServiceCollection services)
    {
        services.TryAddSingleton<IDomainEventPublisher, NoopDomainEventPublisher>();
        services.AddSingleton<InMemoryPartyRepository>();
        services.AddSingleton<IPartyReadModel>(sp => sp.GetRequiredService<InMemoryPartyRepository>());
        services.AddSingleton<IPartyWriteService>(sp => sp.GetRequiredService<InMemoryPartyRepository>());
        services.AddSingleton<IErpnextPartyImporter, ErpnextPartyImporter>();
        return services;
    }
}
