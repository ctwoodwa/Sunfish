using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Default in-process <see cref="IStandingOrderEventStream"/> backed
/// by a list of past events + a list of subscribers. Structurally
/// mirrors <c>Sunfish.Kernel.Audit.InMemoryAuditEventStream</c> per
/// ADR 0065-A1 §A1.4 (lock + list + subscriber-snapshot pattern).
/// </summary>
/// <remarks>
/// <b>Internal-sealed:</b> hidden from package consumers — code
/// outside <c>foundation-wayfinder</c> resolves the
/// <see cref="IStandingOrderEventStream"/> abstraction.
/// <see cref="DefaultStandingOrderIssuer"/> takes the public
/// abstraction in its constructor and casts to this concrete type
/// internally (mirroring
/// <c>Sunfish.Kernel.Audit.EventLogBackedAuditTrail</c>'s pattern
/// at <c>EventLogBackedAuditTrail.cs</c>) so the publish surface
/// (the <see cref="Publish"/> method) is reachable only from
/// <c>foundation-wayfinder</c>'s assembly. Per ADR 0065-A1 §A1.4
/// + §A1.5 — "only the issuer publishes."
/// </remarks>
internal sealed class InMemoryStandingOrderEventStream : IStandingOrderEventStream
{
    private readonly object _gate = new();
    private readonly List<StandingOrderAppliedEvent> _events = new();
    private readonly List<Action<StandingOrderAppliedEvent>> _subscribers = new();

    /// <inheritdoc />
    public IReadOnlyList<StandingOrderAppliedEvent> ReplayAll()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<StandingOrderAppliedEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            _subscribers.Add(handler);
        }
        return new Subscription(this, handler);
    }

    /// <summary>
    /// Append an event to the in-process replay buffer + fan it out
    /// to all currently-registered subscribers. Subscriber callbacks
    /// run on the publishing thread under no lock; the snapshot
    /// pattern means a subscriber added or removed mid-publish does
    /// not race with the fanout.
    /// </summary>
    internal void Publish(StandingOrderAppliedEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        Action<StandingOrderAppliedEvent>[] snapshot;
        lock (_gate)
        {
            _events.Add(evt);
            snapshot = _subscribers.ToArray();
        }
        foreach (var handler in snapshot)
        {
            handler(evt);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly InMemoryStandingOrderEventStream _owner;
        private readonly Action<StandingOrderAppliedEvent> _handler;
        private bool _disposed;

        internal Subscription(
            InMemoryStandingOrderEventStream owner,
            Action<StandingOrderAppliedEvent> handler)
        {
            _owner = owner;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            lock (_owner._gate)
            {
                _owner._subscribers.Remove(_handler);
            }
        }
    }
}
