using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Transport.Relay;
using Xunit;

namespace Sunfish.Foundation.Transport.Tests;

public sealed class BridgeRelayPeerTransportTests
{
    private static readonly PeerId AnyPeer = new("any-peer");

    private static BridgeRelayPeerTransport NewTransport(string url = "wss://127.0.0.1:8443/sync") =>
        new(new BridgeRelayOptions { RelayUrl = new Uri(url) });

    [Fact]
    public void Tier_IsManagedRelay()
    {
        var t = NewTransport();
        Assert.Equal(TransportTier.ManagedRelay, t.Tier);
    }

    [Fact]
    public void IsAvailable_TrueByDefault()
    {
        var t = NewTransport();
        Assert.True(t.IsAvailable);
    }

    [Fact]
    public void MarkUnavailable_ToggleAvailable_ChangesIsAvailable()
    {
        var t = NewTransport();
        t.MarkUnavailable();
        Assert.False(t.IsAvailable);
        t.MarkAvailable();
        Assert.True(t.IsAvailable);
    }

    [Fact]
    public async Task ResolvePeerAsync_AlwaysReturnsConfiguredRelayEndpoint()
    {
        var t = NewTransport("wss://127.0.0.1:8443/sync");
        var endpoint = await t.ResolvePeerAsync(AnyPeer, CancellationToken.None);

        Assert.NotNull(endpoint);
        Assert.Equal(TransportTier.ManagedRelay, endpoint!.Tier);
        Assert.Equal(IPAddress.Loopback, endpoint.Endpoint.Address);
        Assert.Equal(8443, endpoint.Endpoint.Port);
        Assert.Equal(AnyPeer, endpoint.Peer);
    }

    [Fact]
    public async Task ResolvePeerAsync_DefaultsToScheme443ForWss()
    {
        var t = NewTransport("wss://127.0.0.1/sync");
        var endpoint = await t.ResolvePeerAsync(AnyPeer, CancellationToken.None);
        Assert.Equal(443, endpoint!.Endpoint.Port);
    }

    [Fact]
    public async Task ResolvePeerAsync_DefaultsToScheme80ForWs()
    {
        var t = NewTransport("ws://127.0.0.1/sync");
        var endpoint = await t.ResolvePeerAsync(AnyPeer, CancellationToken.None);
        Assert.Equal(80, endpoint!.Endpoint.Port);
    }

    [Fact]
    public async Task ResolvePeerAsync_HonorsCancellation()
    {
        var t = NewTransport();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            t.ResolvePeerAsync(AnyPeer, cts.Token));
    }

    [Fact]
    public async Task ResolvePeerAsync_ReturnsTheSameEndpoint_ForDifferentPeers()
    {
        // Tier-3 multiplexes; the relay endpoint is per-relay, not per-peer.
        var t = NewTransport();
        var a = await t.ResolvePeerAsync(new PeerId("peer-a"), CancellationToken.None);
        var b = await t.ResolvePeerAsync(new PeerId("peer-b"), CancellationToken.None);

        Assert.Equal(a!.Endpoint, b!.Endpoint);
    }

    [Fact]
    public async Task ConnectAsync_AgainstUnreachableRelay_ThrowsCleanly()
    {
        // Loopback :8443 with no listener — ConnectAsync should fail
        // fast and not leak a websocket handle.
        var t = NewTransport("wss://127.0.0.1:18443/sync");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            t.ConnectAsync(AnyPeer, CancellationToken.None));
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BridgeRelayPeerTransport(null!));
    }
}
