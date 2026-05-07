using System;
using System.Collections.Generic;
using System.Threading;
using Sunfish.Foundation.Channels;

namespace Sunfish.Anchor.Services;

/// <summary>
/// W#59 Phase 3 — pub/sub bridge between
/// <see cref="IChannelProvider.ListenAsync"/> + the crew-comms presence
/// stream on one end, and Blazor UI components on the other. Mirrors the
/// hand-off contract:
/// <list type="bullet">
///   <item><see cref="InboundInvitations"/> — every <see cref="IChannelInvitation"/>
///   pulled from <c>ListenAsync</c> by the
///   <see cref="CrewCommsListenerHostedService"/>.</item>
///   <item><see cref="PresenceUpdates"/> — emit once per crew-presence
///   change as observed by the Anchor host (TODO Phase 4 wiring;
///   currently writers may push but no internal source produces yet).</item>
/// </list>
/// <para>
/// Subscriptions are tracked per Blazor component lifecycle: components
/// implement <see cref="IDisposable"/> on the subscription handle from
/// <c>Subscribe()</c>, the bus removes the observer from its list at
/// dispose.
/// </para>
/// </summary>
public interface ICrewCommsInvitationBus
{
    /// <summary>Inbound channel invitations forwarded from
    /// <see cref="IChannelProvider.ListenAsync"/>.</summary>
    IObservable<IChannelInvitation> InboundInvitations { get; }

    /// <summary>Crew-presence change notifications.</summary>
    IObservable<CrewPresence> PresenceUpdates { get; }
}

/// <summary>
/// Writer surface for the bus — distinct interface so consumers (UI) can
/// inject <see cref="ICrewCommsInvitationBus"/> without gaining publish
/// rights, while producers (the listener hosted service) inject
/// <see cref="ICrewCommsInvitationBusWriter"/>.
/// </summary>
public interface ICrewCommsInvitationBusWriter
{
    void PublishInvitation(IChannelInvitation invitation);
    void PublishPresence(CrewPresence presence);
}

/// <summary>
/// In-memory <see cref="ICrewCommsInvitationBus"/> implementation.
/// Thread-safe; observer dispose is idempotent.
/// </summary>
public sealed class CrewCommsInvitationBus : ICrewCommsInvitationBus, ICrewCommsInvitationBusWriter
{
    private readonly Subject<IChannelInvitation> _invitations = new();
    private readonly Subject<CrewPresence> _presence = new();

    /// <inheritdoc />
    public IObservable<IChannelInvitation> InboundInvitations => _invitations;

    /// <inheritdoc />
    public IObservable<CrewPresence> PresenceUpdates => _presence;

    /// <inheritdoc />
    public void PublishInvitation(IChannelInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        _invitations.Publish(invitation);
    }

    /// <inheritdoc />
    public void PublishPresence(CrewPresence presence)
    {
        ArgumentNullException.ThrowIfNull(presence);
        _presence.Publish(presence);
    }

    /// <summary>
    /// Minimal in-house <see cref="IObservable{T}"/> — the codebase has no
    /// System.Reactive dependency and the MVP demo pub/sub fits in a
    /// thread-safe observer list. Errors thrown by observers are swallowed
    /// (logged would be ideal but the bus is dependency-free); a faulty
    /// observer must not poison the publish path for siblings.
    /// </summary>
    private sealed class Subject<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _observers = new();
        private readonly object _lock = new();

        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            lock (_lock)
            {
                _observers.Add(observer);
            }
            return new Subscription(this, observer);
        }

        public void Publish(T value)
        {
            IObserver<T>[] snapshot;
            lock (_lock)
            {
                snapshot = _observers.ToArray();
            }
            foreach (var observer in snapshot)
            {
                try
                {
                    observer.OnNext(value);
                }
                catch
                {
                    // Defensive: don't let one observer's exception cancel
                    // delivery to siblings.
                }
            }
        }

        private void Unsubscribe(IObserver<T> observer)
        {
            lock (_lock)
            {
                _observers.Remove(observer);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Subject<T> _bus;
            private IObserver<T>? _observer;

            public Subscription(Subject<T> bus, IObserver<T> observer)
            {
                _bus = bus;
                _observer = observer;
            }

            public void Dispose()
            {
                var observer = Interlocked.Exchange(ref _observer, null);
                if (observer is null)
                {
                    return;
                }
                _bus.Unsubscribe(observer);
            }
        }
    }
}
