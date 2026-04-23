using System.Collections.Concurrent;

namespace Sunfish.Kernel.Sync.Discovery;

/// <summary>
/// In-process test harness for <see cref="IPeerDiscovery"/>. Multiple
/// instances constructed with the same <see cref="InMemoryPeerDiscoveryBroker"/>
/// (or the shared default broker) see each other as if they were on the same
/// multicast segment.
/// </summary>
/// <remarks>
/// <para>
/// No networking is performed. <see cref="StartAsync"/> registers this node's
/// advertisement with the broker; the broker fans the event out to every
/// other subscriber. <see cref="StopAsync"/> unregisters and emits
/// <see cref="IPeerDiscovery.PeerLost"/> on the other side.
/// </para>
/// <para>
/// A <c>TTL</c> sweep is not modelled — in-process clocks diverge from wall
/// clock in tests. If a test needs eviction semantics, call
/// <see cref="StopAsync"/> on the departing instance.
/// </para>
/// </remarks>
public sealed class InMemoryPeerDiscovery : IPeerDiscovery
{
    private readonly InMemoryPeerDiscoveryBroker _broker;
    private readonly PeerDiscoveryOptions _options;
    private readonly ConcurrentDictionary<string, PeerAdvertisement> _known = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private PeerAdvertisement? _self;
    private bool _disposed;

    public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;
    public event EventHandler<PeerLostEventArgs>? PeerLost;

    /// <summary>Create an instance bound to the process-wide default broker.</summary>
    public InMemoryPeerDiscovery()
        : this(InMemoryPeerDiscoveryBroker.Shared, new PeerDiscoveryOptions())
    {
    }

    /// <summary>Create an instance with an explicit broker and options.</summary>
    public InMemoryPeerDiscovery(InMemoryPeerDiscoveryBroker broker, PeerDiscoveryOptions options)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IReadOnlyCollection<PeerAdvertisement> KnownPeers => _known.Values.ToList();

    public async Task StartAsync(PeerAdvertisement self, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(self);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_self is not null)
            {
                return; // idempotent start
            }
            _self = self;

            // Subscribe first so we don't miss our own announcement race.
            _broker.PeerAnnounced += OnBrokerPeerAnnounced;
            _broker.PeerWithdrawn += OnBrokerPeerWithdrawn;

            // Seed with everything the broker already knows.
            foreach (var existing in _broker.CurrentAdvertisements())
            {
                if (IsSelf(existing)) continue;
                if (!PassesFilter(existing)) continue;
                if (_known.TryAdd(existing.NodeId, existing))
                {
                    PeerDiscovered?.Invoke(this, new PeerDiscoveredEventArgs(existing));
                }
            }

            _broker.Announce(self);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_self is null)
            {
                return;
            }

            _broker.PeerAnnounced -= OnBrokerPeerAnnounced;
            _broker.PeerWithdrawn -= OnBrokerPeerWithdrawn;
            _broker.Withdraw(_self);
            _self = null;

            // Clear our view and fire PeerLost for each — symmetric with
            // MdnsPeerDiscovery's behaviour on stop.
            foreach (var peer in _known.Values.ToList())
            {
                if (_known.TryRemove(peer.NodeId, out _))
                {
                    PeerLost?.Invoke(this, new PeerLostEventArgs(peer.NodeId));
                }
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            /* swallow — DisposeAsync should not throw */
        }
        _lifecycleLock.Dispose();
    }

    private bool IsSelf(PeerAdvertisement ad) =>
        _self is not null && string.Equals(_self.NodeId, ad.NodeId, StringComparison.Ordinal);

    private bool PassesFilter(PeerAdvertisement ad)
    {
        if (!_options.FilterByTeamId) return true;
        if (_self is null) return true;
        return string.Equals(_self.TeamId, ad.TeamId, StringComparison.Ordinal);
    }

    private void OnBrokerPeerAnnounced(object? sender, PeerAdvertisement ad)
    {
        if (_self is null) return;
        if (IsSelf(ad)) return;
        if (!PassesFilter(ad)) return;

        if (_known.TryAdd(ad.NodeId, ad))
        {
            PeerDiscovered?.Invoke(this, new PeerDiscoveredEventArgs(ad));
        }
    }

    private void OnBrokerPeerWithdrawn(object? sender, PeerAdvertisement ad)
    {
        if (_self is null) return;
        if (IsSelf(ad)) return;

        if (_known.TryRemove(ad.NodeId, out _))
        {
            PeerLost?.Invoke(this, new PeerLostEventArgs(ad.NodeId));
        }
    }
}

/// <summary>
/// Process-wide broker that routes <see cref="PeerAdvertisement"/> events
/// between <see cref="InMemoryPeerDiscovery"/> instances. Tests construct
/// their own broker to isolate from the process-wide
/// <see cref="Shared"/> instance.
/// </summary>
public sealed class InMemoryPeerDiscoveryBroker
{
    /// <summary>Process-wide default broker. Prefer an explicit instance in tests.</summary>
    public static InMemoryPeerDiscoveryBroker Shared { get; } = new();

    private readonly ConcurrentDictionary<string, PeerAdvertisement> _advertisements =
        new(StringComparer.Ordinal);

    internal event EventHandler<PeerAdvertisement>? PeerAnnounced;
    internal event EventHandler<PeerAdvertisement>? PeerWithdrawn;

    internal IEnumerable<PeerAdvertisement> CurrentAdvertisements() => _advertisements.Values.ToList();

    internal void Announce(PeerAdvertisement ad)
    {
        _advertisements[ad.NodeId] = ad;
        PeerAnnounced?.Invoke(this, ad);
    }

    internal void Withdraw(PeerAdvertisement ad)
    {
        if (_advertisements.TryRemove(ad.NodeId, out var removed))
        {
            PeerWithdrawn?.Invoke(this, removed);
        }
    }
}
