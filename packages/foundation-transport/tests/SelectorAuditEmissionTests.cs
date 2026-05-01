using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Transport.Tests;

public sealed class SelectorAuditEmissionTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly PeerId PeerA = new("peer-a");
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SelectAsync_T1Success_EmitsTransportTierSelected()
    {
        var (selector, trail, _, _) = NewAuditEnabled(
            new StubPeerTransport(TransportTier.LocalNetwork, resolves: true),
            new StubPeerTransport(TransportTier.ManagedRelay, resolves: true));

        await selector.SelectAsync(PeerA, CancellationToken.None);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.TransportTierSelected) && r.TenantId.Equals(TenantA)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAsync_T2Success_EmitsTransportTierSelected_AndNoMeshFailedSinceFirstAdapterWon()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false);
        var t2 = new StubMeshAdapter("headscale", resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var (selector, trail, _, _) = NewAuditEnabled(t1, t2, t3);

        await selector.SelectAsync(PeerA, CancellationToken.None);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.TransportTierSelected)),
            Arg.Any<CancellationToken>());
        await trail.DidNotReceive().AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.MeshTransportFailed)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAsync_T2AdapterFails_EmitsMeshTransportFailed_BeforeFallthrough()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false);
        var failed = new StubMeshAdapter("headscale", resolves: false);
        var winner = new StubMeshAdapter("netbird", resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var (selector, trail, _, _) = NewAuditEnabled(t1, failed, winner, t3);

        await selector.SelectAsync(PeerA, CancellationToken.None);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.MeshTransportFailed)),
            Arg.Any<CancellationToken>());
        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.TransportTierSelected)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAsync_FallthroughToT3_EmitsTransportFallbackToRelay_OutcomeSelected()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false);
        var t2 = new StubMeshAdapter("headscale", resolves: false);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var (selector, trail, _, _) = NewAuditEnabled(t1, t2, t3);

        await selector.SelectAsync(PeerA, CancellationToken.None);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.TransportFallbackToRelay)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAsync_T3ResolveFails_StillEmitsFallbackToRelay_AndStillReturnsTier3()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: false);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: false);
        var (selector, trail, _, _) = NewAuditEnabled(t1, t3);

        var picked = await selector.SelectAsync(PeerA, CancellationToken.None);

        Assert.Same(t3, picked);
        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.TransportFallbackToRelay)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAsync_AuditDisabled_DoesNotEmit()
    {
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var selector = new DefaultTransportSelector(new IPeerTransport[] { t1, t3 }, time: new FakeTimeProvider(Now));

        await selector.SelectAsync(PeerA, CancellationToken.None);

        // No audit trail wired — no exceptions, no emissions. The
        // assertion is implicit (nothing throws), but we add a positive
        // gate by ensuring the selector returns the right transport.
        var resolved = await selector.SelectAsync(PeerA, CancellationToken.None);
        Assert.Same(t1, resolved);
    }

    [Fact]
    public async Task SelectAsync_AuditEnabled_CacheHitSkipsEmission()
    {
        // Audit fires on selection (cache miss), not on every call.
        // Cache hit → no second emission.
        var t1 = new StubPeerTransport(TransportTier.LocalNetwork, resolves: true);
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var (selector, trail, _, _) = NewAuditEnabled(t1, t3);

        await selector.SelectAsync(PeerA, CancellationToken.None);
        await selector.SelectAsync(PeerA, CancellationToken.None);

        await trail.Received(1).AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_AuditEnabled_RequiresAllArgs()
    {
        var t3 = new StubPeerTransport(TransportTier.ManagedRelay, resolves: true);
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());

        Assert.Throws<ArgumentNullException>(() => new DefaultTransportSelector(
            new IPeerTransport[] { t3 }, null!, signer, TenantA));
        Assert.Throws<ArgumentNullException>(() => new DefaultTransportSelector(
            new IPeerTransport[] { t3 }, trail, null!, TenantA));
        Assert.Throws<ArgumentException>(() => new DefaultTransportSelector(
            new IPeerTransport[] { t3 }, trail, signer, default));
    }

    private static (DefaultTransportSelector selector, IAuditTrail trail, IOperationSigner signer, FakeTimeProvider time) NewAuditEnabled(params IPeerTransport[] transports)
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var time = new FakeTimeProvider(Now);
        var selector = new DefaultTransportSelector(transports, trail, signer, TenantA, time);
        return (selector, trail, signer, time);
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
        private readonly bool _resolves;
        public StubPeerTransport(TransportTier tier, bool resolves) { Tier = tier; _resolves = resolves; }
        public TransportTier Tier { get; }
        public bool IsAvailable => true;
        public Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct) =>
            Task.FromResult<PeerEndpoint?>(_resolves
                ? new PeerEndpoint { Peer = peer, Endpoint = new IPEndPoint(IPAddress.Loopback, 1), Tier = Tier, DiscoveredAt = DateTimeOffset.UtcNow }
                : null);
        public Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class StubMeshAdapter : StubPeerTransport, IMeshVpnAdapter
    {
        public StubMeshAdapter(string adapterName, bool resolves) : base(TransportTier.MeshVpn, resolves)
        {
            AdapterName = adapterName;
        }
        public string AdapterName { get; }
        public Task<MeshNodeStatus> GetMeshStatusAsync(CancellationToken ct) =>
            Task.FromResult(new MeshNodeStatus { IsConnected = true, Peers = Array.Empty<MeshPeer>() });
        public Task RegisterDeviceAsync(MeshDeviceRegistration registration, CancellationToken ct) => Task.CompletedTask;
    }
}
