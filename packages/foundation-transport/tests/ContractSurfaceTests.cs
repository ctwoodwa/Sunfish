using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;
using Xunit;

namespace Sunfish.Foundation.Transport.Tests;

/// <summary>
/// Phase 2.1 contract-surface gate (ADR 0061 §"Initial contract surface" + A1):
/// these tests don't exercise behavior — they assert that the contracts
/// declared in <c>Sunfish.Foundation.Transport</c> can be implemented and
/// consumed from a downstream assembly with no compile errors. If the
/// contract surface drifts (a property gets renamed, a tier discriminator
/// disappears, an interface gets a new required member), this file fails
/// to build.
/// </summary>
public sealed class ContractSurfaceTests
{
    [Fact]
    public void TransportTier_HasThreeTiers_InOrder()
    {
        Assert.Equal(0, (int)TransportTier.LocalNetwork);
        Assert.Equal(1, (int)TransportTier.MeshVpn);
        Assert.Equal(2, (int)TransportTier.ManagedRelay);
    }

    [Fact]
    public void PeerEndpoint_RequiresPeer_Endpoint_Tier_DiscoveredAt()
    {
        var peer = new PeerId("test-peer");
        var endpoint = new PeerEndpoint
        {
            Peer = peer,
            Endpoint = new IPEndPoint(IPAddress.Loopback, 7777),
            Tier = TransportTier.LocalNetwork,
            DiscoveredAt = DateTimeOffset.UtcNow,
        };
        Assert.Equal(peer, endpoint.Peer);
        Assert.Equal(TransportTier.LocalNetwork, endpoint.Tier);
        Assert.Null(endpoint.LastSeenAt);
    }

    [Fact]
    public void MeshPeer_LastHandshakeAt_IsRequired()
    {
        var ts = DateTimeOffset.UtcNow;
        var p = new MeshPeer
        {
            Peer = new PeerId("p"),
            MeshEndpoint = new IPEndPoint(IPAddress.Loopback, 51820),
            LastHandshakeAt = ts,
        };
        Assert.Equal(ts, p.LastHandshakeAt);
    }

    [Fact]
    public void MeshNodeStatus_TracksConnectedFlag_PeerList_OptionalLastHandshake()
    {
        var status = new MeshNodeStatus
        {
            IsConnected = true,
            Peers = new List<MeshPeer>(),
        };
        Assert.True(status.IsConnected);
        Assert.Empty(status.Peers);
        Assert.Null(status.LastHandshakeAt);
    }

    [Fact]
    public void MeshDeviceRegistration_SeparatesDeviceIdFromPeerId()
    {
        // Per ADR 0061 A1: a single Sunfish peer may rotate Headscale
        // node-keys without rotating its PeerId. The two-field shape
        // makes this mapping adapter-private + explicit.
        var reg = new MeshDeviceRegistration
        {
            DeviceId = "headscale-abc-123",
            Peer = new PeerId("base64url-ed25519-pubkey"),
            DeviceName = "anchor-laptop-01",
            Tags = new[] { "tag:sunfish-anchor" },
        };
        Assert.NotEqual(reg.DeviceId, reg.Peer.Value);
        Assert.Single(reg.Tags);
    }

    [Fact]
    public async Task IPeerTransport_CanBeImplementedExternally()
    {
        IPeerTransport transport = new StubPeerTransport(TransportTier.LocalNetwork);
        Assert.Equal(TransportTier.LocalNetwork, transport.Tier);
        Assert.True(transport.IsAvailable);
        var endpoint = await transport.ResolvePeerAsync(new PeerId("p"), CancellationToken.None);
        Assert.NotNull(endpoint);
        await using var stream = await transport.ConnectAsync(new PeerId("p"), CancellationToken.None);
        Assert.NotNull(stream.Stream);
    }

    [Fact]
    public async Task IMeshVpnAdapter_ExtendsIPeerTransport_AndAddsControlPlaneShape()
    {
        IMeshVpnAdapter adapter = new StubMeshAdapter();
        IPeerTransport asTransport = adapter; // covariant assignment compiles ⇒ inheritance contract intact
        Assert.Equal(TransportTier.MeshVpn, asTransport.Tier);
        Assert.Equal("stub", adapter.AdapterName);
        var status = await adapter.GetMeshStatusAsync(CancellationToken.None);
        Assert.False(status.IsConnected);
        await adapter.RegisterDeviceAsync(new MeshDeviceRegistration
        {
            DeviceId = "d1",
            Peer = new PeerId("p1"),
            DeviceName = "dev",
            Tags = Array.Empty<string>(),
        }, CancellationToken.None);
    }

    [Fact]
    public async Task ITransportSelector_ReturnsAnIPeerTransport()
    {
        ITransportSelector selector = new StubSelector();
        var t = await selector.SelectAsync(new PeerId("p"), CancellationToken.None);
        Assert.Equal(TransportTier.ManagedRelay, t.Tier);
    }

    [Fact]
    public async Task IDuplexStream_DisposeAsync_IsIdempotent()
    {
        var s = new StubDuplexStream();
        await s.DisposeAsync();
        await s.DisposeAsync(); // second call must not throw
        Assert.Equal(2, s.DisposeCount);
    }

    private sealed class StubPeerTransport(TransportTier tier) : IPeerTransport
    {
        public TransportTier Tier => tier;
        public bool IsAvailable => true;
        public Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct) =>
            Task.FromResult<PeerEndpoint?>(new PeerEndpoint
            {
                Peer = peer,
                Endpoint = new IPEndPoint(IPAddress.Loopback, 1),
                Tier = tier,
                DiscoveredAt = DateTimeOffset.UtcNow,
            });
        public Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct) =>
            Task.FromResult<IDuplexStream>(new StubDuplexStream());
    }

    private sealed class StubMeshAdapter : IMeshVpnAdapter
    {
        public TransportTier Tier => TransportTier.MeshVpn;
        public bool IsAvailable => false;
        public string AdapterName => "stub";
        public Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct) =>
            Task.FromResult<PeerEndpoint?>(null);
        public Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<MeshNodeStatus> GetMeshStatusAsync(CancellationToken ct) =>
            Task.FromResult(new MeshNodeStatus { IsConnected = false, Peers = Array.Empty<MeshPeer>() });
        public Task RegisterDeviceAsync(MeshDeviceRegistration registration, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubSelector : ITransportSelector
    {
        public Task<IPeerTransport> SelectAsync(PeerId peer, CancellationToken ct) =>
            Task.FromResult<IPeerTransport>(new StubPeerTransport(TransportTier.ManagedRelay));
    }

    private sealed class StubDuplexStream : IDuplexStream
    {
        public int DisposeCount { get; private set; }
        public Stream Stream { get; } = new MemoryStream();
        public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct) => Task.FromResult(0);
        public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct) => Task.CompletedTask;
        public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
