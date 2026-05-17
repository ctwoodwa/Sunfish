using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Events;

/// <summary>
/// Per-handler cursor-based reader over the <c>domain_events</c>
/// table. PR 4 ships <see cref="SqliteEventReader"/> +
/// <see cref="EventDispatcherHost"/> that drive at-least-once
/// delivery via the cursor model.
/// </summary>
/// <remarks>
/// <para>
/// Each handler has its own cursor (per-tenant, per-handler-id) in
/// <c>event_handler_cursors</c>. Cursors are <em>not</em>
/// cross-replica synced — each replica drives its own dispatcher
/// independently per
/// <c>cross-cluster-event-bus-design.md</c> §5.
/// </para>
/// </remarks>
public interface IEventReader
{
    /// <summary>
    /// Read events of <paramref name="eventType"/> that are newer
    /// than the supplied <paramref name="afterEventId"/>, up to
    /// <paramref name="maxBatchSize"/>. Ordered by EventId ascending
    /// (ULIDs sort by mint-time).
    /// </summary>
    Task<IReadOnlyList<RawDomainEvent>> ReadAsync(
        TenantId tenantId,
        string eventType,
        string? afterEventId,
        int maxBatchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a handler for a specific event type. The reader walks
    /// the store from the handler's last cursor forward and invokes
    /// the handler on each new event (driven by
    /// <see cref="EventDispatcherHost"/>'s polling loop). Cursor
    /// advance is per-handler — slow or failing handlers do NOT
    /// block other handlers.
    /// </summary>
    /// <param name="handlerId">Stable id for cursor persistence (e.g., <c>"blocks-work.ProjectActualsUpserter"</c>).</param>
    /// <param name="eventType">Cluster-qualified event-type filter (e.g., <c>"Financial.JournalEntryPosted"</c>). Null matches all types.</param>
    /// <param name="handler">Async handler. Throwing does NOT advance the cursor; retry scheduled per backoff schedule.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RegisterHandlerAsync(
        string handlerId,
        string? eventType,
        IEventHandler<RawDomainEvent> handler,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Type-safe per-payload handler shape. Cluster code wires
/// implementations against this interface and the
/// <see cref="EventDispatcherHost"/> drives the per-handler cursor
/// walk.
/// </summary>
/// <typeparam name="TPayload">The payload shape this handler consumes.</typeparam>
public interface IEventHandler<TPayload>
{
    /// <summary>
    /// Handle a single envelope. Implementations MUST be idempotent
    /// — the dispatcher re-delivers on retry (failure pinned cursor),
    /// and the
    /// <see cref="DomainEventEnvelope{TPayload}.IdempotencyKey"/>
    /// is the dedup mechanism.
    /// </summary>
    Task HandleAsync(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default);
}
