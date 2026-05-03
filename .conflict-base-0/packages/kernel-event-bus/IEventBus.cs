using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Kernel.Events;

/// <summary>
/// Kernel primitive §3.6 — event bus. Publishes and subscribes to signed
/// <see cref="KernelEvent"/> envelopes with per-entity ordering, idempotent
/// delivery, and durable-subscriber checkpoints.
/// </summary>
/// <remarks>
/// <para>
/// This is the shipping contract for gap <b>G3 (in-proc half, Option B)</b>
/// in the platform gap analysis
/// (<c>icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md</c>).
/// The namespace <c>Sunfish.Kernel.Events</c> is preserved from the G1
/// reserved stub; the assembly that owns the type moved from
/// <c>Sunfish.Kernel</c> to <c>Sunfish.Kernel.EventBus</c> when the stub was
/// promoted (mirroring the G2 schema-registry pattern).
/// </para>
/// <para>
/// Distributed backends (MassTransit, Wolverine, Kafka, libp2p) are a
/// deliberate follow-up — the interface deliberately stays transport-neutral
/// so those backends can ship without changing downstream consumers.
/// </para>
/// </remarks>
public interface IEventBus
{
    /// <summary>
    /// Publishes <paramref name="signedEvent"/> to the bus after verifying its
    /// Ed25519 signature. Duplicate envelopes (same
    /// <see cref="SignedOperation{T}.Nonce"/>) are silently discarded so
    /// publishers can retry safely — delivery is idempotent at the nonce level.
    /// </summary>
    /// <param name="signedEvent">Ed25519-signed event envelope. Signature covers the canonical-JSON of the payload + issuer + issuedAt + nonce; see <see cref="IOperationVerifier"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidEventException">
    /// Thrown when the signature fails to verify, or when the envelope is
    /// structurally malformed (e.g. public-key bytes can't be imported).
    /// </exception>
    ValueTask PublishAsync(SignedOperation<KernelEvent> signedEvent, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to events matching <paramref name="subscription"/> and yields
    /// them to the caller as they arrive. The returned enumerator completes
    /// when <paramref name="ct"/> is cancelled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ordering guarantee:</b> within a single <see cref="EntityId"/>,
    /// events are yielded in publish order. Across different entities, events
    /// may interleave arbitrarily — there is no global total order.
    /// </para>
    /// <para>
    /// <b>Checkpoint resume:</b> <see cref="EventSubscription.ResumeFrom"/> is
    /// a no-op in the in-memory backend because the channel-based default
    /// does not retain a replay buffer — events published before a
    /// subscription started are not visible to it. Persistent backends (future
    /// work) will honour <c>ResumeFrom</c> by rewinding their underlying log.
    /// </para>
    /// </remarks>
    /// <param name="subscription">Filter + resume parameters.</param>
    /// <param name="ct">Cancellation token. Cancelling terminates the enumeration cleanly.</param>
    IAsyncEnumerable<SignedOperation<KernelEvent>> SubscribeAsync(EventSubscription subscription, CancellationToken ct = default);

    /// <summary>
    /// Loads the last-persisted checkpoint for <paramref name="subscriberId"/>,
    /// or <see langword="null"/> when no checkpoint has been advanced yet.
    /// </summary>
    /// <param name="subscriberId">Caller-chosen stable identifier for the subscriber.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<EventCheckpoint?> GetCheckpointAsync(string subscriberId, CancellationToken ct = default);

    /// <summary>
    /// Records that <paramref name="subscriberId"/> has processed through
    /// <paramref name="lastProcessed"/>. Subsequent calls to
    /// <see cref="GetCheckpointAsync"/> return the newly-advanced position.
    /// </summary>
    /// <param name="subscriberId">Caller-chosen stable identifier for the subscriber.</param>
    /// <param name="lastProcessed">The last <see cref="EventId"/> the subscriber finished processing.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask AdvanceCheckpointAsync(string subscriberId, EventId lastProcessed, CancellationToken ct = default);
}

/// <summary>
/// Subscription parameters passed to <see cref="IEventBus.SubscribeAsync"/>.
/// </summary>
/// <param name="SubscriberId">Stable caller-chosen identifier. Used as the key for <see cref="IEventBus.GetCheckpointAsync"/> / <see cref="IEventBus.AdvanceCheckpointAsync"/>.</param>
/// <param name="EntityFilter">When non-null, only events for this entity are yielded. When null, events for every entity are yielded.</param>
/// <param name="KindFilter">When non-null, only events whose <see cref="KernelEvent.Kind"/> matches (case-sensitive, exact) are yielded. When null, every kind is yielded.</param>
/// <param name="ResumeFrom">When non-null, a checkpoint to resume from. Honored only by persistent backends — see <see cref="IEventBus.SubscribeAsync"/> remarks.</param>
public sealed record EventSubscription(
    string SubscriberId,
    EntityId? EntityFilter = null,
    string? KindFilter = null,
    EventCheckpoint? ResumeFrom = null);

/// <summary>
/// Durable checkpoint recording how far a subscriber has processed.
/// </summary>
/// <param name="SubscriberId">Identifier of the subscriber this checkpoint belongs to.</param>
/// <param name="LastProcessed">The <see cref="EventId"/> most-recently marked processed by the subscriber.</param>
/// <param name="AdvancedAt">Wall-clock time at which the checkpoint was recorded.</param>
public sealed record EventCheckpoint(
    string SubscriberId,
    EventId LastProcessed,
    DateTimeOffset AdvancedAt);
