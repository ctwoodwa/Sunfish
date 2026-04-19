using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Kernel.Events;

/// <summary>
/// In-memory default backend for <see cref="IEventBus"/>. Uses
/// <see cref="System.Threading.Channels"/> with a per-subscriber bounded
/// channel for fan-out, a per-entity lock for publish ordering, and an
/// in-process nonce set for idempotent delivery. See
/// <c>icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md</c> G3 for
/// the shipping contract.
/// </summary>
/// <remarks>
/// <para>
/// <b>Per-entity ordering:</b> <see cref="PublishAsync"/> serializes writes
/// for the same <see cref="EntityId"/> behind a per-entity <see cref="SemaphoreSlim"/>,
/// so every subscriber receives events for a given entity in the order the
/// publisher invoked <c>PublishAsync</c>. Events for <i>different</i> entities
/// may interleave arbitrarily — no global total order is offered, which
/// matches the spec §3.6 guarantee and leaves headroom for sharded backends.
/// </para>
/// <para>
/// <b>Fan-out model:</b> each active <see cref="SubscribeAsync"/> call owns a
/// bounded <see cref="Channel{T}"/> (default capacity
/// <see cref="DefaultSubscriberCapacity"/>). <c>PublishAsync</c> fans out to
/// every subscriber channel whose filter matches. Subscribers that fall
/// behind will block the publisher once their channel fills up — this is
/// intentional: an unbounded channel would let a slow subscriber leak
/// unbounded memory. A follow-up can expose capacity on an options bag.
/// </para>
/// <para>
/// <b>Idempotency:</b> duplicates are detected via
/// <see cref="SignedOperation{T}.Nonce"/>. The first publish for a nonce
/// proceeds; subsequent publishes with the same nonce are logged at Debug
/// and return successfully without fanning out. The seen-nonce set grows
/// unbounded in this reference implementation; production backends must bound
/// it (e.g. time-windowed or persisted-set), which is a follow-up.
/// </para>
/// <para>
/// <b>Checkpoints:</b> stored in an in-memory dictionary keyed by
/// subscriber id. Lost on process restart — durable subscribers that need to
/// survive restarts should pair this bus with a persistent checkpoint store
/// in a follow-up. <see cref="EventSubscription.ResumeFrom"/> is accepted for
/// forward compatibility but has no observable effect here, as the bus
/// retains no replay buffer.
/// </para>
/// </remarks>
public sealed class InMemoryEventBus : IEventBus
{
    /// <summary>Default bounded capacity for each subscriber's channel.</summary>
    internal const int DefaultSubscriberCapacity = 1000;

    private readonly IOperationVerifier _verifier;
    private readonly ILogger<InMemoryEventBus> _logger;

    // Per-entity publish serialization. Lazily allocated on first publish for
    // that entity. Bounded in count by the number of distinct EntityIds seen
    // — no explicit eviction (follow-up).
    private readonly ConcurrentDictionary<EntityId, SemaphoreSlim> _entityLocks = new();

    // Seen-nonce set for idempotency. ConcurrentDictionary used as a set via
    // TryAdd; the byte value is unused.
    private readonly ConcurrentDictionary<Guid, byte> _seenNonces = new();

    // Durable checkpoints keyed by SubscriberId.
    private readonly ConcurrentDictionary<string, EventCheckpoint> _checkpoints = new();

    // Active subscribers. Each entry has its own channel; PublishAsync fans
    // out to the ones whose filter matches the event. The list is rebuilt on
    // snapshot rather than locked — subscribes/unsubscribes are comparatively
    // rare versus publishes.
    private readonly object _subscribersGate = new();
    private readonly List<Subscriber> _subscribers = new();

    /// <summary>Creates a new <see cref="InMemoryEventBus"/>.</summary>
    /// <param name="verifier">Ed25519 verifier used on every <see cref="PublishAsync"/>. Resolved from DI via <c>AddSunfishDecentralization</c> or manually-registered <see cref="Ed25519Verifier"/>.</param>
    /// <param name="logger">Optional logger; a no-op logger is used when null.</param>
    public InMemoryEventBus(IOperationVerifier verifier, ILogger<InMemoryEventBus>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        _verifier = verifier;
        _logger = logger ?? NullLogger<InMemoryEventBus>.Instance;
    }

    /// <inheritdoc />
    public async ValueTask PublishAsync(SignedOperation<KernelEvent> signedEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(signedEvent);
        ct.ThrowIfCancellationRequested();

        // 1. Signature verification. Catch any verifier-internal exception
        //    (e.g. malformed public-key bytes) so callers see a single
        //    bus-level exception type rather than a pass-through crypto error.
        bool signatureValid;
        try
        {
            signatureValid = _verifier.Verify(signedEvent);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidEventException(
                $"Event signature could not be evaluated: {ex.Message}", ex);
        }

        if (!signatureValid)
        {
            throw new InvalidEventException(
                $"Event signature failed verification (issuer '{signedEvent.IssuerId}', nonce {signedEvent.Nonce:D}).");
        }

        // 2. Idempotent delivery. TryAdd returns false when the nonce is
        //    already present — treat as a silent no-op and log at Debug.
        if (!_seenNonces.TryAdd(signedEvent.Nonce, 0))
        {
            _logger.LogDebug(
                "Discarded duplicate event: nonce {Nonce} already seen for entity {EntityId}.",
                signedEvent.Nonce,
                signedEvent.Payload.EntityId);
            return;
        }

        // 3. Serialize fan-out per entity so concurrent publishers for the
        //    same entity produce a deterministic per-entity order.
        var entityLock = _entityLocks.GetOrAdd(signedEvent.Payload.EntityId, _ => new SemaphoreSlim(1, 1));
        await entityLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Snapshot subscribers under the subscribers gate; filter match
            // decisions are made off the snapshot so a concurrent subscribe
            // doesn't race with fan-out.
            Subscriber[] snapshot;
            lock (_subscribersGate)
            {
                snapshot = _subscribers.ToArray();
            }

            foreach (var subscriber in snapshot)
            {
                if (!subscriber.Matches(signedEvent.Payload))
                {
                    continue;
                }

                // Bounded channel — if the subscriber is slow this WriteAsync
                // will await until there is capacity (back-pressure). That is
                // intentional; see class-level remarks.
                await subscriber.Channel.Writer.WriteAsync(signedEvent, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            entityLock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SignedOperation<KernelEvent>> SubscribeAsync(
        EventSubscription subscription,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        var channel = Channel.CreateBounded<SignedOperation<KernelEvent>>(
            new BoundedChannelOptions(DefaultSubscriberCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            });

        var subscriber = new Subscriber(subscription, channel);

        lock (_subscribersGate)
        {
            _subscribers.Add(subscriber);
        }

        try
        {
            // ReadAllAsync yields items FIFO and cooperates with ct.
            await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return evt;
            }
        }
        finally
        {
            lock (_subscribersGate)
            {
                _subscribers.Remove(subscriber);
            }
            // Complete the writer side so any in-flight PublishAsync awaiting
            // capacity on a removed subscriber will observe completion rather
            // than hang. (In practice PublishAsync snapshots before writing,
            // so a removed subscriber would have been dropped already; this
            // is defence-in-depth.)
            channel.Writer.TryComplete();
        }
    }

    /// <inheritdoc />
    public ValueTask<EventCheckpoint?> GetCheckpointAsync(string subscriberId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriberId);
        ct.ThrowIfCancellationRequested();

        return _checkpoints.TryGetValue(subscriberId, out var cp)
            ? new ValueTask<EventCheckpoint?>(cp)
            : new ValueTask<EventCheckpoint?>((EventCheckpoint?)null);
    }

    /// <inheritdoc />
    public ValueTask AdvanceCheckpointAsync(string subscriberId, EventId lastProcessed, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriberId);
        ct.ThrowIfCancellationRequested();

        // Overwrite unconditionally — callers drive checkpoint monotonicity
        // at a higher layer. We record the write time as AdvancedAt for
        // diagnostic / ordering purposes only.
        var cp = new EventCheckpoint(subscriberId, lastProcessed, DateTimeOffset.UtcNow);
        _checkpoints[subscriberId] = cp;
        return default;
    }

    /// <summary>
    /// Internal pairing of an <see cref="EventSubscription"/> with its
    /// dedicated fan-out <see cref="Channel{T}"/>. Owned by a single
    /// <see cref="SubscribeAsync"/> invocation.
    /// </summary>
    private sealed class Subscriber
    {
        public Subscriber(EventSubscription subscription, Channel<SignedOperation<KernelEvent>> channel)
        {
            Subscription = subscription;
            Channel = channel;
        }

        public EventSubscription Subscription { get; }
        public Channel<SignedOperation<KernelEvent>> Channel { get; }

        /// <summary>Returns true iff <paramref name="evt"/> passes both filters.</summary>
        public bool Matches(KernelEvent evt)
        {
            if (Subscription.EntityFilter is { } entityFilter && !entityFilter.Equals(evt.EntityId))
            {
                return false;
            }
            if (Subscription.KindFilter is { } kindFilter && !string.Equals(kindFilter, evt.Kind, StringComparison.Ordinal))
            {
                return false;
            }
            return true;
        }
    }
}
