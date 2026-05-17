using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Events;

/// <summary>
/// Canonical cross-cluster domain-event envelope. Every event emitted
/// by any <c>blocks-*</c> cluster carries this shape. Per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §1.
/// </summary>
/// <typeparam name="TPayload">
/// The cluster-specific event payload (e.g.,
/// <c>JournalEntryPostedPayload</c>,
/// <c>PeriodSoftClosedPayload</c>).
/// </typeparam>
/// <remarks>
/// <para>
/// <b>Append-only:</b> envelopes are immutable after construction
/// (<see langword="record"/> type + <see langword="init"/>-only
/// setters). They are persisted to the <c>domain_events</c> SQLite
/// table by <see cref="IDomainEventStore"/> (PR 2) and never updated.
/// </para>
/// <para>
/// <b>Idempotency:</b> the <see cref="IdempotencyKey"/> is the unique
/// dedup mechanism. Per
/// <c>cross-cluster-event-bus-design.md</c> §4, every event type
/// defines a deterministic derivation of its idempotency key from its
/// semantic identity. Two emissions with the same
/// <c>(TenantId, IdempotencyKey)</c> tuple collapse into one row at
/// the <see cref="IDomainEventStore"/> level.
/// </para>
/// <para>
/// <b>Causation vs Correlation:</b> <see cref="CausationId"/> chains
/// upstream → downstream (handler emits event B in reaction to event
/// A → <c>B.CausationId = A.EventId</c>). <see cref="CorrelationId"/>
/// marks a logical workflow across many events (lease execution chain
/// shares one correlation id).
/// </para>
/// <para>
/// <b>Store-side denormalization columns (NOT envelope fields):</b>
/// the SQLite <c>domain_events</c> table adds <c>recorded_at_utc</c>
/// (write-time), <c>producer_cluster</c>, and
/// <c>producer_entity_kind</c>/<c>producer_entity_id</c> at insertion-
/// time. Producers never set them; the store layer computes them on
/// append. See <see cref="IDomainEventStore"/> + PR 2 schema.
/// </para>
/// </remarks>
public sealed record DomainEventEnvelope<TPayload>
{
    /// <summary>
    /// Sortable unique event identifier. Producers mint via
    /// <c>EventId.New()</c> which emits a ULID-format 26-char
    /// Crockford-base-32 string derived from UUIDv7 bytes. Primary key
    /// in the <c>domain_events</c> table.
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Cluster-qualified verb-past-tense name, e.g.
    /// <c>"Financial.JournalEntryPosted"</c>,
    /// <c>"Work.WorkOrderCreated"</c>. Per
    /// <c>cross-cluster-event-bus-design.md</c> §2.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Payload-shape version; bump when <typeparamref name="TPayload"/>
    /// gains / loses / renames fields. Consumers branch on
    /// <see cref="SchemaVersion"/> to handle prior shapes during
    /// rolling migrations. Per
    /// <c>cross-cluster-event-bus-design.md</c> §8.
    /// </summary>
    public required int SchemaVersion { get; init; }

    /// <summary>
    /// Wall-clock at the moment of event creation (may be backdated
    /// for synthetic events — e.g., a Tenant whose moveInDate is set
    /// in the past creates a TenantActivated with
    /// <see cref="OccurredAt"/> = moveInDate). Distinct from store-
    /// side <c>recorded_at_utc</c>.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Tenant scope. Cross-tenant events are FORBIDDEN per
    /// <c>crdt-friendly-schema-conventions.md</c> §14.
    /// </summary>
    public required TenantId TenantId { get; init; }

    /// <summary>
    /// Replica that originated this event. Provenance for audit +
    /// cross-replica de-duplication.
    /// </summary>
    public required ReplicaId OriginatingReplicaId { get; init; }

    /// <summary>
    /// Deterministic dedup key. UNIQUE per
    /// <c>(TenantId, IdempotencyKey)</c> in the
    /// <c>domain_events</c> table — duplicate appends collapse into
    /// the existing row.
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// Optional. <see cref="EventId"/> of the upstream event that
    /// caused this one. Enables debugging "why did this event fire?"
    /// </summary>
    public string? CausationId { get; init; }

    /// <summary>
    /// Optional. Workflow correlation id; events sharing this value
    /// are part of the same logical workflow.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>Cluster-specific typed payload.</summary>
    public required TPayload Payload { get; init; }
}
