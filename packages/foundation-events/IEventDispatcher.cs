namespace Sunfish.Foundation.Events;

/// <summary>
/// In-process broadcast surface — fan-out push-style delivery of
/// freshly persisted events to subscribers in the same process.
/// Complements the cursor-driven pull from <see cref="IEventReader"/>
/// (PR 4): the dispatcher is <b>best-effort</b> low-latency; the
/// cursor walk is the <b>durable</b> at-least-once delivery path.
/// </summary>
/// <remarks>
/// <para>
/// Subscribers can tolerate event loss — use this for live-feed UI
/// observers, hot-path projections that already have a separate
/// rebuild-from-store path, etc.
/// </para>
/// <para>
/// Cluster handlers that need guaranteed at-least-once delivery
/// register via <see cref="IEventReader"/> instead (PR 4).
/// </para>
/// </remarks>
public interface IEventDispatcher
{
    /// <summary>
    /// Subscribe a handler. Handlers run via fan-out on
    /// <see cref="DispatchAsync"/>; failures in one subscriber do
    /// NOT block siblings (failures are isolated + logged).
    /// </summary>
    void Subscribe(Func<RawDomainEvent, CancellationToken, Task> handler);

    /// <summary>
    /// Notify all subscribed handlers of a freshly persisted event.
    /// </summary>
    Task DispatchAsync(RawDomainEvent evt, CancellationToken cancellationToken = default);
}
