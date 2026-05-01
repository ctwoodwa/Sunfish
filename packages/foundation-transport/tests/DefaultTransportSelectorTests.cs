using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;
using Xunit;

namespace Sunfish.Foundation.Transport.Tests;

public sealed class DefaultTransportSelectorTests
{
    private static readonly PeerId PeerA = new("peer-a");
    private static readonly PeerId PeerB = new("peer-b");
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SelectAsync_T1_FastPath_ReturnsTier1WithoutTouchingT2OrT3()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: true);
        var t2 = new StubMeshAdapter("headscale", resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var selector = new DefaultTransportSelector(new IPeerTransport[] { t1, t2, t3 }, time: new FakeTimeProvider(Now));

        var picked = await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Same(t1, picked);
        Assert.Equal(1, t1.ResolveCallCount);
        Assert.Equal(0, t2.ResolveCallCount);
        Assert.Equal(0, t3.ResolveCallCount);
    }

    [Fact]
    public async Task SelectAsync_T1FailsT2Succeeds_ReturnsTier2()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false);
        var t2 = new StubMeshAdapter("headscale", resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var selector = new DefaultTransportSelector(new IPeerTransport[] { t1, t2, t3 }, time: new FakeTimeProvider(Now));

        var picked = await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Same(t2, picked);
        Assert.Equal(1, t1.ResolveCallCount);
        Assert.Equal(1, t2.ResolveCallCount);
        Assert.Equal(0, t3.ResolveCallCount);
    }

    [Fact]
    public async Task SelectAsync_T1AndT2Fail_FallsThroughToT3()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false);
        var t2 = new StubMeshAdapter("headscale", resolves: false);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: false);
        var selector = new DefaultTransportSelector(new IPeerTransport[] { t1, t2, t3 }, time: new FakeTimeProvider(Now));

        var picked = await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Same(t3, picked);
        Assert.Equal(1, t1.ResolveCallCount);
        Assert.Equal(1, t2.ResolveCallCount);
        Assert.Equal(1, t3.ResolveCallCount);
    }

    [Fact]
    public async Task SelectAsync_MultipleMeshAdapters_IteratesInRegistrationOrder_StoppingAtFirstSuccess()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false);
        // Registration order is the operator-set "config priority" per
        // ADR 0061 §"Tier selection algorithm" + A2. Selector tries
        // tailscale (idx 0, fails) → headscale (idx 1, fails) →
        // netbird (idx 2, succeeds) → never reaches the post-netbird
        // adapter. Lexicographic is the tie-break only when two
        // adapters tie on registration-priority (impossible in this
        // setup since each is registered at a distinct index).
        var tailscale = new StubMeshAdapter("tailscale", resolves: false);
        var headscale = new StubMeshAdapter("headscale", resolves: false);
        var netbird = new StubMeshAdapter("netbird", resolves: true);
        var unreachedAdapter = new StubMeshAdapter("wireguard-manual", resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var selector = new DefaultTransportSelector(
            new IPeerTransport[] { t1, tailscale, headscale, netbird, unreachedAdapter, t3 },
            time: new FakeTimeProvider(Now));

        var picked = await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Same(netbird, picked);
        Assert.Equal(1, tailscale.ResolveCallCount);
        Assert.Equal(1, headscale.ResolveCallCount);
        Assert.Equal(1, netbird.ResolveCallCount);
        Assert.Equal(0, unreachedAdapter.ResolveCallCount); // post-success adapter never tried
    }

    [Fact]
    public async Task SelectAsync_MeshAdapterUnavailable_SkipsWithoutCalling()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false);
        var headscale = new StubMeshAdapter("headscale", resolves: true) { IsAvailableValue = false };
        var netbird = new StubMeshAdapter("netbird", resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var selector = new DefaultTransportSelector(
            new IPeerTransport[] { t1, headscale, netbird, t3 },
            time: new FakeTimeProvider(Now));

        var picked = await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Same(netbird, picked);
        Assert.Equal(0, headscale.ResolveCallCount); // skipped — IsAvailable false
        Assert.Equal(1, netbird.ResolveCallCount);
    }

    [Fact]
    public async Task SelectAsync_CacheHit_DoesNotReinvokeTransports()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var time = new FakeTimeProvider(Now);
        var selector = new DefaultTransportSelector(new IPeerTransport[] { t1, t3 }, time: time);

        var first = await selector.SelectAsync(PeerA, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(15)); // within 30s TTL
        var second = await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(1, t1.ResolveCallCount); // only the first SelectAsync touched the transport
    }

    [Fact]
    public async Task SelectAsync_CacheExpiry_ReResolvesAfterTtl()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var time = new FakeTimeProvider(Now);
        var selector = new DefaultTransportSelector(new IPeerTransport[] { t1, t3 }, time: time);

        await selector.SelectAsync(PeerA, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(31)); // past 30s TTL
        await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Equal(2, t1.ResolveCallCount);
    }

    [Fact]
    public async Task SelectAsync_PerPeerBestTier_DifferentPeersDifferentTiers()
    {
        // Peer A reachable via T1, Peer B only via T3 — different tiers
        // selected within the same selector instance, neither selection
        // downgrades the other (per A2 partial-failure contract).
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: peer => peer == PeerA);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var selector = new DefaultTransportSelector(new IPeerTransport[] { t1, t3 }, time: new FakeTimeProvider(Now));

        var pickedA = await selector.SelectAsync(PeerA, CancellationToken.None);
        var pickedB = await selector.SelectAsync(PeerB, CancellationToken.None);

        Assert.Same(t1, pickedA);
        Assert.Same(t3, pickedB);
    }

    [Fact]
    public async Task SelectAsync_T1Throws_FallsThroughGracefully()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false) { ThrowOnResolve = true };
        var t2 = new StubMeshAdapter("headscale", resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var selector = new DefaultTransportSelector(new IPeerTransport[] { t1, t2, t3 }, time: new FakeTimeProvider(Now));

        var picked = await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Same(t2, picked); // T1 threw → fall through, not propagate
    }

    [Fact]
    public async Task SelectAsync_PerTierBudgetExhausted_FallsThrough()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: true) { ResolveDelay = TimeSpan.FromSeconds(10) };
        var t2 = new StubMeshAdapter("headscale", resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var selector = new DefaultTransportSelector(
            new IPeerTransport[] { t1, t2, t3 },
            time: new FakeTimeProvider(Now),
            tier1Budget: TimeSpan.FromMilliseconds(50));

        var picked = await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Same(t2, picked); // T1 cancelled by budget → fall through
    }

    [Fact]
    public async Task SelectAsync_NoTier3Registered_Throws()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false);
        var t2 = new StubMeshAdapter("headscale", resolves: false);

        Assert.Throws<ArgumentException>(() =>
            new DefaultTransportSelector(new IPeerTransport[] { t1, t2 }, time: new FakeTimeProvider(Now)));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Invalidate_DropsCachedPeerEntry()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var selector = new DefaultTransportSelector(new IPeerTransport[] { t1, t3 }, time: new FakeTimeProvider(Now));

        await selector.SelectAsync(PeerA, CancellationToken.None);
        selector.Invalidate(PeerA);
        await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Equal(2, t1.ResolveCallCount); // cache invalidated → re-resolved
    }

    [Fact]
    public async Task SelectAsync_RegistrationOrderWinsOverLexicographicTieBreak()
    {
        // Two adapters with same "config priority" (registration order
        // is the operator-set priority in DI; lexicographic is only the
        // tie-break). Adapter registered first wins when both succeed.
        // Here: both succeed → first-registered wins.
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false);
        var tailscale = new StubMeshAdapter("tailscale", resolves: true); // registered first
        var headscale = new StubMeshAdapter("headscale", resolves: true); // registered second
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var selector = new DefaultTransportSelector(
            new IPeerTransport[] { t1, tailscale, headscale, t3 },
            time: new FakeTimeProvider(Now));

        var picked = await selector.SelectAsync(PeerA, CancellationToken.None);

        // Both adapters succeed; tailscale was registered first (lower
        // configPriority index), so it wins despite headscale being
        // lexicographically smaller.
        Assert.Same(tailscale, picked);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private class StubPeerTransport : IPeerTransport
    {
        private readonly Func<PeerId, bool> _resolves;
        public StubPeerTransport(TransportTier tier, bool resolves) : this(tier, _ => resolves) { }
        public StubPeerTransport(TransportTier tier, Func<PeerId, bool> resolves)
        {
            Tier = tier;
            _resolves = resolves;
        }
        public TransportTier Tier { get; }
        public bool IsAvailable => true;
        public int ResolveCallCount { get; private set; }
        public bool ThrowOnResolve { get; set; }
        public TimeSpan ResolveDelay { get; set; } = TimeSpan.Zero;

        public async Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct)
        {
            ResolveCallCount++;
            if (ThrowOnResolve) throw new InvalidOperationException("stub forced throw");
            if (ResolveDelay > TimeSpan.Zero) await Task.Delay(ResolveDelay, ct).ConfigureAwait(false);
            if (!_resolves(peer)) return null;
            return new PeerEndpoint
            {
                Peer = peer,
                Endpoint = new IPEndPoint(IPAddress.Loopback, 1),
                Tier = Tier,
                DiscoveredAt = DateTimeOffset.UtcNow,
            };
        }

        public Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct) =>
            throw new NotSupportedException("ConnectAsync is the caller's responsibility — selector tests don't exercise it.");
    }

    private sealed class StubMeshAdapter : StubPeerTransport, IMeshVpnAdapter
    {
        public StubMeshAdapter(string adapterName, bool resolves) : base(TransportTier.MeshVpn, resolves)
        {
            AdapterName = adapterName;
        }
        public string AdapterName { get; }
        public bool IsAvailableValue { get; set; } = true;
        public new bool IsAvailable => IsAvailableValue;
        public Task<MeshNodeStatus> GetMeshStatusAsync(CancellationToken ct) =>
            Task.FromResult(new MeshNodeStatus { IsConnected = true, Peers = Array.Empty<MeshPeer>() });
        public Task RegisterDeviceAsync(MeshDeviceRegistration registration, CancellationToken ct) => Task.CompletedTask;
    }
}
