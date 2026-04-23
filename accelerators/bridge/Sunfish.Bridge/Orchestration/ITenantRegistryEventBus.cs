using Sunfish.Bridge.Data.Entities;

namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Payload raised on <see cref="ITenantRegistryEventBus"/> when
/// <c>TenantRegistry</c> (Wave 5.1 + Wave 5.2.B) commits a lifecycle mutation
/// — create, suspend, resume, cancel. Uses the control-plane
/// <see cref="TenantStatus"/> enum (NOT <see cref="TenantProcessState"/>):
/// this is a DB-state transition, not a process-state transition.
/// </summary>
/// <param name="TenantId">Identity of the tenant whose DB row transitioned.</param>
/// <param name="Previous">Status observed on the row before the mutation.</param>
/// <param name="Current">Status the row holds after the mutation.</param>
/// <param name="OccurredAt">UTC wall-clock instant the mutation was committed.</param>
/// <param name="Reason">Optional operator-supplied context — e.g.
/// "billing non-payment" or "operator cancel via admin UI". For structured
/// logging only; not a machine-readable discriminator. <see langword="null"/>
/// when no reason was supplied.</param>
public sealed record TenantLifecycleEvent(
    Guid TenantId,
    TenantStatus Previous,
    TenantStatus Current,
    DateTimeOffset OccurredAt,
    string? Reason);

/// <summary>
/// In-process pub/sub between <c>TenantRegistry</c> (Wave 5.2.B, producer)
/// and <c>TenantLifecycleCoordinator</c> (Wave 5.2.C, consumer). Pinned up
/// front in Wave 5.2.A per <c>_shared/product/wave-5.2-decomposition.md</c>
/// §7 anti-pattern #7 "Delegation without contracts" — without this, 5.2.B
/// and 5.2.C would renegotiate the contract at integration time.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a distributed bus.</b> The default implementation
/// (<see cref="InMemoryTenantRegistryEventBus"/>) runs in the Bridge control
/// plane's single Blazor Server process. Wolverine / RabbitMQ are used for
/// the broader Bridge message mesh; this bus is intentionally
/// in-process-only because the consumer is always co-located with the
/// producer. If Bridge ever scales horizontally (Wave 5.5+), swap the
/// implementation — the contract is stable.
/// </para>
/// <para>
/// <b>Delivery semantics.</b> Best-effort, fire-and-forget. Subscribers that
/// need durability should persist their own side of the handoff; the bus
/// does not buffer undelivered events.
/// </para>
/// </remarks>
public interface ITenantRegistryEventBus
{
    /// <summary>
    /// Fan out <paramref name="event"/> to every current subscriber on the
    /// calling thread, synchronously. Implementations MUST NOT throw on
    /// subscriber exceptions — a misbehaving subscriber must not block
    /// other subscribers from seeing the event.
    /// </summary>
    void Publish(TenantLifecycleEvent @event);

    /// <summary>
    /// Register <paramref name="handler"/> to receive every subsequent
    /// <see cref="Publish"/>. Returns a disposable that, when disposed,
    /// removes the handler from the subscriber set; disposed handlers do
    /// not fire on subsequent publishes.
    /// </summary>
    IDisposable Subscribe(Action<TenantLifecycleEvent> handler);
}

/// <summary>
/// Default in-process implementation of <see cref="ITenantRegistryEventBus"/>.
/// Registered as a singleton by
/// <see cref="ServiceCollectionExtensions.AddBridgeOrchestration"/>. Thread-safe.
/// </summary>
/// <remarks>
/// <para>
/// <b>Concurrency model.</b> Subscriber set is copied under a lock on every
/// <see cref="Publish"/> and <see cref="Subscribe"/>, then iterated outside
/// the lock. This admits the "publish from within a handler" case (a handler
/// may call <see cref="Subscribe"/> or <see cref="Publish"/> without
/// deadlocking) at the cost of ignoring subscribers added mid-iteration —
/// they first fire on the NEXT publish.
/// </para>
/// <para>
/// <b>Exception handling.</b> Exceptions thrown by a subscriber are caught
/// and swallowed so subsequent subscribers still fire. The bus does not log
/// — logging is a wrapper's responsibility. This keeps the bus dependency-free
/// (no <c>ILogger</c> reference from a contracts-only assembly).
/// </para>
/// </remarks>
public sealed class InMemoryTenantRegistryEventBus : ITenantRegistryEventBus
{
    private readonly object _gate = new();
    private readonly List<Action<TenantLifecycleEvent>> _subscribers = [];

    /// <inheritdoc />
    public void Publish(TenantLifecycleEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Action<TenantLifecycleEvent>[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _subscribers];
        }

        foreach (var handler in snapshot)
        {
            try
            {
                handler(@event);
            }
            catch
            {
                // Per remarks: a misbehaving subscriber must not block other
                // subscribers. Bus stays dependency-free — no ILogger wiring.
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<TenantLifecycleEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            _subscribers.Add(handler);
        }
        return new Subscription(this, handler);
    }

    private void Unsubscribe(Action<TenantLifecycleEvent> handler)
    {
        lock (_gate)
        {
            _subscribers.Remove(handler);
        }
    }

    private sealed class Subscription(InMemoryTenantRegistryEventBus owner, Action<TenantLifecycleEvent> handler) : IDisposable
    {
        private InMemoryTenantRegistryEventBus? _owner = owner;

        public void Dispose()
        {
            var o = Interlocked.Exchange(ref _owner, null);
            o?.Unsubscribe(handler);
        }
    }
}
