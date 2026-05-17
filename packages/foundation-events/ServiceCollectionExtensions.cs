using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Sunfish.Foundation.Events;

/// <summary>
/// DI conveniences for <c>Sunfish.Foundation.Events</c>. Per the
/// foundation-events hand-off PR 6.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the canonical cross-cluster event-bus substrate:
    /// <see cref="SqliteDomainEventStore"/>,
    /// <see cref="DefaultDomainEventPublisher"/>,
    /// <see cref="SqliteEventReader"/>,
    /// <see cref="InProcessEventDispatcher"/>, and the
    /// <see cref="EventDispatcherHost"/> background drain loop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Consumer clusters (e.g., <c>blocks-financial-periods</c>,
    /// <c>blocks-financial-tax</c>) resolve
    /// <see cref="IDomainEventPublisher"/> via DI after this method
    /// runs. The clusters' local <c>TryAddSingleton</c> fallbacks
    /// (Noop publishers) are removed by PR 6's sweep — the canonical
    /// is the only registration after this call.
    /// </para>
    /// <para>
    /// <b>Prerequisites the host composition root supplies:</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description>A singleton <see cref="Microsoft.Data.Sqlite.SqliteConnection"/>
    /// already opened.</description></item>
    /// <item><description><see cref="TimeProvider"/> — registered as
    /// <see cref="TimeProvider.System"/> via <c>TryAddSingleton</c>
    /// when not already wired.</description></item>
    /// </list>
    /// <para>
    /// After registration, the host calls
    /// <see cref="ApplyFoundationEventsMigrationsAsync"/> once
    /// post-build to create the <c>domain_events</c> +
    /// <c>event_handler_cursors</c> + <c>event_handler_failures</c>
    /// tables.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddFoundationEvents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        // Store + concrete-type alias so the host can resolve
        // SqliteDomainEventStore directly for ApplyMigrationsAsync.
        services.AddSingleton<SqliteDomainEventStore>();
        services.AddSingleton<IDomainEventStore>(sp => sp.GetRequiredService<SqliteDomainEventStore>());

        // Dispatcher + concrete-type alias so consumers can
        // Subscribe() against the InProcessEventDispatcher directly.
        services.AddSingleton<InProcessEventDispatcher>();
        services.AddSingleton<IEventDispatcher>(sp => sp.GetRequiredService<InProcessEventDispatcher>());

        // Reader + concrete-type alias so the host wires the
        // EventDispatcherHost against the concrete reader (which
        // exposes DrainOnceAsync beyond the IEventReader surface).
        services.AddSingleton<SqliteEventReader>();
        services.AddSingleton<IEventReader>(sp => sp.GetRequiredService<SqliteEventReader>());

        // Canonical publisher.
        services.AddSingleton<IDomainEventPublisher, DefaultDomainEventPublisher>();

        // Background drain loop.
        services.AddHostedService<EventDispatcherHost>();

        return services;
    }

    /// <summary>
    /// Apply foundation-events SQLite migrations once at host
    /// startup. The host invokes this AFTER the
    /// <see cref="Microsoft.Data.Sqlite.SqliteConnection"/> is opened
    /// + BEFORE any cluster registers an event publisher.
    /// </summary>
    public static async Task ApplyFoundationEventsMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        var store = services.GetRequiredService<SqliteDomainEventStore>();
        await store.ApplyMigrationsAsync(cancellationToken).ConfigureAwait(false);
    }
}
