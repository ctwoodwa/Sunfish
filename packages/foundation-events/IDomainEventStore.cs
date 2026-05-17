using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Events;

/// <summary>
/// Append-only durable store for domain events. PR 2 ships the
/// SQLite-backed implementation (<c>SqliteDomainEventStore</c>); PR 1
/// defines the contract so callers can wire it before the
/// implementation lands.
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
/// On conflict (duplicate <see cref="DomainEventEnvelope{TPayload}.IdempotencyKey"/>),
/// the store returns the EXISTING row's <see cref="AppendResult.EventId"/>
/// (not the would-be-inserted one) — callers know dedup happened
/// without needing to query.
/// </para>
/// </remarks>
public interface IDomainEventStore
{
    /// <summary>
    /// Append an envelope to the store. On idempotency-key conflict,
    /// returns the existing row's id with
    /// <see cref="AppendResult.Deduped"/> = <c>true</c>.
    /// </summary>
    Task<AppendResult> AppendAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch a single event by id within the supplied tenant scope.
    /// Returns null when missing or when the stored row's tenant
    /// does not match (security gate — cross-tenant reads must fail
    /// closed).
    /// </summary>
    Task<StoredEvent?> GetByIdAsync(
        TenantId tenantId,
        string eventId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a <see cref="IDomainEventStore.AppendAsync"/> call.
/// </summary>
/// <param name="EventId">The canonical event id; equals the envelope's id on insert, OR the existing-row id on dedup.</param>
/// <param name="Deduped">True when the append was rejected because an existing row had the same <c>(TenantId, IdempotencyKey)</c>.</param>
public readonly record struct AppendResult(string EventId, bool Deduped);

/// <summary>
/// Stored-event projection returned by
/// <see cref="IDomainEventStore.GetByIdAsync"/> + the cursor-based
/// readers in PR 4. Carries the envelope fields plus the store-side
/// denormalization columns (<see cref="RecordedAtUtc"/>,
/// <see cref="ProducerCluster"/>) the SQLite store materializes at
/// insertion-time.
/// </summary>
public sealed record StoredEvent
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
    public string? ProducerEntityKind { get; init; }
    public string? ProducerEntityId { get; init; }

    /// <summary>JSON-serialized payload. Consumers deserialize per their event type.</summary>
    public required string PayloadJson { get; init; }
}
