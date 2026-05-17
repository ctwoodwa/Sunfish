using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Events;

/// <summary>
/// Per-handler cursor-based reader over the <c>domain_events</c>
/// table. PR 4 ships <c>SqliteEventReader</c> + the
/// <c>EventDispatcherHost</c> that drives at-least-once delivery via
/// the cursor model. PR 1 defines the contract so the cluster
/// migration sweep (PR 6) can compile-check consumers against it.
/// </summary>
/// <remarks>
/// <para>
/// Each handler has its own cursor (per-tenant, per-event-type, per-
/// handler-name) in <c>event_handler_cursors</c>. Cursors are
/// <em>not</em> cross-replica synced — each replica drives its own
/// dispatcher independently per
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
}

/// <summary>
/// Type-safe per-payload handler shape. Cluster code wires
/// implementations against this interface and the
/// <c>EventDispatcherHost</c> (PR 4) routes events by
/// <see cref="DomainEventEnvelope{TPayload}.EventType"/> + payload
/// type registration.
/// </summary>
/// <typeparam name="TPayload">The payload shape this handler consumes.</typeparam>
public interface IEventHandler<TPayload>
{
    /// <summary>
    /// Handle a single envelope. Implementations MUST be idempotent
    /// — the dispatcher may re-deliver the same envelope on retry,
    /// and the <see cref="DomainEventEnvelope{TPayload}.IdempotencyKey"/>
    /// is the dedup mechanism the dispatcher uses.
    /// </summary>
    Task HandleAsync(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default);
}
