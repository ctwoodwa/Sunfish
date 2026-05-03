using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Transport.Mdns;
using Xunit;

namespace Sunfish.Foundation.Transport.Tests;

public sealed class MdnsPeerTransportTests
{
    private static readonly PeerId LocalPeer = new("local-peer-id");
    private static readonly PeerId RemotePeer = new("remote-peer-id");
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Tier_IsLocalNetwork()
    {
        var transport = new MdnsPeerTransport();
        Assert.Equal(TransportTier.LocalNetwork, transport.Tier);
    }

    [Fact]
    public void IsAvailable_FalseBeforeStart()
    {
        var transport = new MdnsPeerTransport();
        Assert.False(transport.IsAvailable);
    }

    [Fact]
    public async Task ResolvePeerAsync_NotInCache_ReturnsNull()
    {
        var transport = new MdnsPeerTransport();
        var endpoint = await transport.ResolvePeerAsync(RemotePeer, CancellationToken.None);
        Assert.Null(endpoint);
    }

    [Fact]
    public async Task ResolvePeerAsync_CacheHit_ReturnsEndpoint()
    {
        var time = new FakeTimeProvider(Now);
        var transport = new MdnsPeerTransport(time: time);
        var seeded = new PeerEndpoint
        {
            Peer = RemotePeer,
            Endpoint = new IPEndPoint(IPAddress.Loopback, 7777),
            Tier = TransportTier.LocalNetwork,
            DiscoveredAt = Now,
        };
        transport.SeedCacheForTest(RemotePeer, seeded, Now);

        var resolved = await transport.ResolvePeerAsync(RemotePeer, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(7777, resolved!.Endpoint.Port);
        Assert.Equal(TransportTier.LocalNetwork, resolved.Tier);
    }

    [Fact]
    public async Task ResolvePeerAsync_StaleEntry_ReturnsNullAndEvicts()
    {
        var time = new FakeTimeProvider(Now);
        var opts = new MdnsPeerTransportOptions { PeerCacheTtlSeconds = 60 };
        var transport = new MdnsPeerTransport(opts, time);
        transport.SeedCacheForTest(RemotePeer, new PeerEndpoint
        {
            Peer = RemotePeer,
            Endpoint = new IPEndPoint(IPAddress.Loopback, 7777),
            Tier = TransportTier.LocalNetwork,
            DiscoveredAt = Now,
        }, Now);

        time.Advance(TimeSpan.FromSeconds(61));
        var first = await transport.ResolvePeerAsync(RemotePeer, CancellationToken.None);
        Assert.Null(first);

        // Eviction sticks: a follow-up resolve also returns null.
        var second = await transport.ResolvePeerAsync(RemotePeer, CancellationToken.None);
        Assert.Null(second);
    }

    [Fact]
    public async Task ResolvePeerAsync_HonorsCancellation()
    {
        var transport = new MdnsPeerTransport();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            transport.ResolvePeerAsync(RemotePeer, cts.Token));
    }

    [Fact]
    public async Task ConnectAsync_PeerNotInCache_Throws()
    {
        var transport = new MdnsPeerTransport();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.ConnectAsync(RemotePeer, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectAsync_OpensTcpConnection_AgainstLoopbackListener()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();
        try
        {
            var transport = new MdnsPeerTransport();
            transport.SeedCacheForTest(RemotePeer, new PeerEndpoint
            {
                Peer = RemotePeer,
                Endpoint = new IPEndPoint(IPAddress.Loopback, port),
                Tier = TransportTier.LocalNetwork,
                DiscoveredAt = Now,
            }, Now);

            await using var stream = await transport.ConnectAsync(RemotePeer, CancellationToken.None);

            using var server = await acceptTask;
            Assert.True(server.Connected);
            Assert.NotNull(stream.Stream);

            // Round-trip a byte to confirm the duplex stream is wired.
            await stream.WriteAsync(new byte[] { 0xAB }, CancellationToken.None);
            await stream.FlushAsync(CancellationToken.None);
            var serverBuf = new byte[1];
            await server.GetStream().ReadExactlyAsync(serverBuf, CancellationToken.None);
            Assert.Equal(0xAB, serverBuf[0]);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var transport = new MdnsPeerTransport();
        await transport.DisposeAsync();
        await transport.DisposeAsync(); // must not throw
    }

    [Fact]
    public async Task StartAsync_ThenStopAsync_DoesNotThrow()
    {
        // We don't assert mDNS round-trip semantics here — that's the
        // env-var-gated integration test below. This fact pins the
        // lifecycle: StartAsync wires Makaretu.Dns successfully on a
        // host that allows multicast (loopback is enough for socket
        // binding); StopAsync tears it down cleanly.
        var transport = new MdnsPeerTransport();
        try
        {
            await transport.StartAsync(LocalPeer, new IPEndPoint(IPAddress.Loopback, 7777), CancellationToken.None);
            Assert.True(transport.IsAvailable);
        }
        catch (System.Net.Sockets.SocketException)
        {
            // Some sandboxed CI runners refuse multicast bind. The
            // env-var-gated integration test is the authoritative gate
            // for that; skip rather than fail.
            return;
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        await transport.StopAsync(CancellationToken.None);
        Assert.False(transport.IsAvailable);
    }

    /// <summary>
    /// Local-LAN end-to-end smoke test per ADR 0061 P3 acceptance gate.
    /// Two <see cref="MdnsPeerTransport"/> instances (peer A advertises;
    /// peer B browses) on the same host should resolve each other within
    /// the Tier-1 budget. Gated on <c>SUNFISH_MDNS_TESTS=1</c> per the
    /// existing convention in <see cref="Sunfish.Kernel.Sync.Discovery.MdnsPeerDiscovery"/>'s
    /// test suite — multicast doesn't work in containerized CI.
    /// </summary>
    [Fact]
    public async Task EndToEnd_LoopbackRoundTrip_DiscoversPeerWithinBudget()
    {
        if (Environment.GetEnvironmentVariable("SUNFISH_MDNS_TESTS") != "1")
        {
            return; // skipped by default
        }

        var peerA = new PeerId("test-peer-a");
        var peerB = new PeerId("test-peer-b");
        var serviceType = $"_sunfish-test-{Guid.NewGuid():N}._tcp.local";
        var opts = new MdnsPeerTransportOptions { ServiceType = serviceType, SweepIntervalSeconds = 1 };

        await using var transportA = new MdnsPeerTransport(opts);
        await using var transportB = new MdnsPeerTransport(opts);

        await transportA.StartAsync(peerA, new IPEndPoint(IPAddress.Loopback, 17771), CancellationToken.None);
        await transportB.StartAsync(peerB, new IPEndPoint(IPAddress.Loopback, 17772), CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        PeerEndpoint? resolved = null;
        while (DateTime.UtcNow < deadline)
        {
            resolved = await transportB.ResolvePeerAsync(peerA, CancellationToken.None);
            if (resolved is not null) break;
            await Task.Delay(100);
        }
        Assert.NotNull(resolved);
        Assert.Equal(TransportTier.LocalNetwork, resolved!.Tier);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
