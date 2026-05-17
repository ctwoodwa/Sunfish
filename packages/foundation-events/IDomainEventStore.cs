using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Events;

/// <summary>
/// Append-only durable store for domain events. PR 2 ships the
/// SQLite-backed implementation (<see cref="SqliteDomainEventStore"/>);
/// PR 4 wires the cursor-based reader and dispatcher host.
/// </summary>
/// <remarks>
/// <para>
/// <b>Append-only:</b> the store never issues <c>UPDATE</c> or
/// <c>DELETE</c> against the <c>domain_events</c> table (outside the
/// crypto-shred path, which is out of scope for v1). Per
/// <c>crdt-friendly-schema-conventions.md</c> §6 (posted-then-
/// immutable).
/// </para>
/// <para>
/// <b>Idempotency:</b> <see cref="AppendAsync"/> uses
/// <c>INSERT ... ON CONFLICT(tenant_id, idempotency_key) DO NOTHING</c>.
/// On conflict (duplicate
/// <see cref="DomainEventEnvelope{TPayload}.IdempotencyKey"/>), the
/// method returns the EXISTING row's event id (not the would-be-
/// inserted one) — callers detect dedup by comparing the returned
/// id to the envelope's <see cref="DomainEventEnvelope{TPayload}.EventId"/>.
/// </para>
/// </remarks>
public interface IDomainEventStore
{
    /// <summary>
    /// Append an envelope to the store. On idempotency-key conflict,
    /// returns the existing row's event id.
    /// </summary>
    Task<string> AppendAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read events strictly after <paramref name="afterEventId"/> in
    /// ULID (event-id) order, up to <paramref name="batchSize"/>.
    /// Tenant-scoped. Pass <c>null</c> for <paramref name="afterEventId"/>
    /// to read from the beginning.
    /// </summary>
    Task<IReadOnlyList<RawDomainEvent>> GetAfterCursorAsync(
        TenantId tenantId,
        string? afterEventId,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Look up an event by its idempotency key within the supplied
    /// tenant. Returns <c>null</c> when no row matches.
    /// </summary>
    Task<RawDomainEvent?> FindByIdempotencyKeyAsync(
        TenantId tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Untyped projection of a persisted event row. Carries the envelope
/// fields plus the store-side <see cref="RecordedAtUtc"/> +
/// <see cref="ProducerCluster"/> denormalization columns the SQLite
/// store materializes at insertion-time. Consumers deserialize
/// <see cref="PayloadJson"/> to their cluster-specific payload type at
/// dispatch time.
/// </summary>
public sealed record RawDomainEvent
{
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public required int SchemaVersion { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required DateTimeOffset RecordedAtUtc { get; init; }
    public required TenantId TenantId { get; init; }
    public required ReplicaId OriginatingReplicaId { get; init; }
    public required string IdempotencyKey { get; init; }
    public string? CausationId { get; init; }
    public string? CorrelationId { get; init; }
    public required string ProducerCluster { get; init; }

    /// <summary>JSON-serialized payload. Consumers deserialize per their event type.</summary>
    public required string PayloadJson { get; init; }
}
