using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// Internal helper for building <see cref="DomainEventEnvelope{TPayload}"/>
/// instances with consistent identity / provenance / idempotency
/// metadata. Keeps the per-event-type idempotency-key shape in one
/// place so the four publishing call-sites can't drift.
///
/// <para>
/// PR 5 scope note: the helper hard-codes
/// <see cref="TenantId.System"/> + <see cref="ReplicaId.System"/>
/// since the in-memory stores have no tenant/replica context yet.
/// When the SQLite-backed implementations land (separate hand-off)
/// they'll pass real tenant/replica values through; this helper's
/// signature is shaped to take them.
/// </para>
/// </summary>
internal static class DomainEventEnvelopeFactory
{
    /// <summary>
    /// Build an envelope around <paramref name="payload"/>. <paramref name="idempotencyKey"/>
    /// follows the convention <c>{eventType}|{tenantId}|{entityId}|{stateOrTransition}</c>
    /// (see <see cref="DomainEventEnvelope{TPayload}"/> remarks).
    /// </summary>
    public static DomainEventEnvelope<TPayload> Build<TPayload>(
        string eventType,
        TPayload payload,
        string idempotencyKey,
        TenantId? tenantId = null,
        ReplicaId? replicaId = null,
        string? causationId = null,
        string? correlationId = null) =>
        new DomainEventEnvelope<TPayload>
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            TenantId = tenantId ?? TenantId.System,
            OriginatingReplicaId = replicaId ?? ReplicaId.System,
            IdempotencyKey = idempotencyKey,
            CausationId = causationId,
            CorrelationId = correlationId,
            Payload = payload!,
        };
}
