using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Kernel.Events;

/// <summary>
/// A kernel-level event about an entity. Published through
/// <see cref="IEventBus"/> inside a
/// <see cref="Sunfish.Foundation.Crypto.SignedOperation{T}"/> envelope so every
/// event on the bus is Ed25519-authenticated end-to-end (spec §3.6).
/// </summary>
/// <remarks>
/// The event payload is a free-form key/value map rather than a closed
/// discriminated union so blocks can evolve their event schemas without
/// changing the kernel contract. Payload shape per <c>Kind</c> is the
/// publisher's responsibility; subscribers that care about structure should
/// validate against a schema registered in the kernel schema registry
/// (spec §3.4, gap G2).
/// </remarks>
/// <param name="Id">Unique event id. Distinct from the envelope nonce — the nonce is used for idempotent delivery deduplication at the bus layer; the <see cref="EventId"/> is the canonical event identifier that checkpoints advance against.</param>
/// <param name="EntityId">The entity this event is about. Used as the partition key for per-entity ordering in <see cref="InMemoryEventBus"/>.</param>
/// <param name="Kind">Short string discriminator (e.g. <c>entity.created</c>, <c>entity.updated</c>, <c>workorder.completed</c>). Used by <see cref="EventSubscription.KindFilter"/>.</param>
/// <param name="OccurredAt">Wall-clock time at which the business event occurred (not the time at which it was published — those may differ if events are buffered).</param>
/// <param name="Payload">Kind-specific body. Callers should treat unknown keys as tolerable rather than error out — forward-compatibility across block versions depends on it.</param>
public sealed record KernelEvent(
    EventId Id,
    EntityId EntityId,
    string Kind,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, object?> Payload);

/// <summary>
/// Canonical identifier for a <see cref="KernelEvent"/>. Opaque to the bus;
/// consumers use it as the position marker for <see cref="IEventBus.AdvanceCheckpointAsync"/>.
/// </summary>
/// <param name="Value">Underlying GUID. New ids should be minted via <see cref="NewId"/>.</param>
public readonly record struct EventId(Guid Value)
{
    /// <summary>Mints a fresh <see cref="EventId"/> from a newly-generated GUID.</summary>
    public static EventId NewId() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
