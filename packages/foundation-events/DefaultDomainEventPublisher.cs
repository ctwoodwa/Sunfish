using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Events;

/// <summary>
/// Default <see cref="IDomainEventPublisher"/>. Persists the envelope
/// to <see cref="IDomainEventStore"/> + notifies in-process
/// subscribers via <see cref="IEventDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dedup-and-suppress:</b> when the store reports dedup (the
/// returned event id differs from the envelope's id), the publisher
/// SKIPS the in-process dispatch — the original event already
/// dispatched on its first emit, and redelivering would double-fire
/// observers that aren't idempotent.
/// </para>
/// <para>
/// <b>Store failures propagate:</b> if
/// <see cref="IDomainEventStore.AppendAsync"/> throws, the
/// dispatcher is NOT called and the exception bubbles to the caller.
/// Producers wrap the entity write + publish in the same transaction
/// when they need atomicity (per cross-cluster-event-bus-design.md
/// §9).
/// </para>
/// </remarks>
public sealed class DefaultDomainEventPublisher : IDomainEventPublisher
{
    private readonly IDomainEventStore _store;
    private readonly IEventDispatcher _dispatcher;
    private readonly JsonSerializerOptions _jsonOptions;

    public DefaultDomainEventPublisher(
        IDomainEventStore store,
        IEventDispatcher dispatcher)
    {
        _store      = store      ?? throw new ArgumentNullException(nameof(store));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    /// <inheritdoc />
    public async Task PublishAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var persistedEventId = await _store.AppendAsync(envelope, cancellationToken).ConfigureAwait(false);

        // Dedup-and-suppress: when the store reports the existing
        // row's id (not the envelope's id), the original event
        // already dispatched on its first emit. Skip redelivery.
        if (!string.Equals(persistedEventId, envelope.EventId, StringComparison.Ordinal))
            return;

        // Project to the untyped RawDomainEvent shape the dispatcher
        // expects. RecordedAtUtc is best-effort (we don't get the
        // canonical store-side timestamp back from AppendAsync);
        // hot-path observers tolerant of event loss can also tolerate
        // a slightly-out-of-band RecordedAtUtc here. The durable
        // cursor walk in PR 4 reads the canonical value from the
        // store directly.
        var raw = new RawDomainEvent
        {
            EventId              = envelope.EventId,
            EventType            = envelope.EventType,
            SchemaVersion        = envelope.SchemaVersion,
            OccurredAt           = envelope.OccurredAt,
            RecordedAtUtc        = envelope.OccurredAt,
            TenantId             = envelope.TenantId,
            OriginatingReplicaId = envelope.OriginatingReplicaId,
            IdempotencyKey       = envelope.IdempotencyKey,
            CausationId          = envelope.CausationId,
            CorrelationId        = envelope.CorrelationId,
            ProducerCluster      = DeriveProducerCluster(envelope.EventType),
            PayloadJson          = JsonSerializer.Serialize(envelope.Payload, _jsonOptions),
        };

        await _dispatcher.DispatchAsync(raw, cancellationToken).ConfigureAwait(false);
    }

    private static string DeriveProducerCluster(string eventType)
    {
        var dotIdx = eventType.IndexOf('.');
        // The store already validated the format on AppendAsync — if
        // we got past AppendAsync, the dot is present. Defensive
        // fallback to empty for any unparseable case.
        return dotIdx > 0 ? eventType[..dotIdx].ToLowerInvariant() : string.Empty;
    }
}
