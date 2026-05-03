using Microsoft.Extensions.Options;

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Wave 6.5 coverage for <see cref="IGossipDaemon.FrameReceived"/>. The event
/// is the observable surface that <c>GossipEventTeamNotificationStream</c>
/// (in <c>Sunfish.Kernel.Runtime</c>) subscribes to; these tests pin the
/// shape of the args and that the raise happens on the expected round
/// outcomes.
/// </summary>
/// <remarks>
/// We deliberately cover the "happy handshake" path here via a responder
/// loop (mirroring <see cref="GossipDaemonTests.Two_Daemons_Complete_Handshake_And_Merge_VectorClocks"/>)
/// and the "connect failed → GossipError" path via an unreachable peer. The
/// handshake-failure (bad signature) path is covered indirectly — a
/// transport-drop mid-HELLO is reported as <see cref="GossipFrameType.HandshakeFailure"/>
/// by the same code path.
/// </remarks>
public class GossipDaemonFrameEventTests : IAsyncLifetime
{
    private readonly List<IAsyncDisposable> _cleanup = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var d in _cleanup)
        {
            try { await d.DisposeAsync(); } catch { /* best-effort */ }
        }
    }

    private static string NewEndpoint() => $"gossip-frame-{Guid.NewGuid():N}";

    [Fact]
    public async Task FrameReceived_fires_GossipError_on_unreachable_peer()
    {
        // Unknown endpoint → transport ConnectAsync throws → the daemon
        // should emit a GossipError frame carrying the endpoint in the
        // summary.
        await using var transport = new InMemorySyncDaemonTransport();
        var signer = TestIdentityFactory.NewSigner();
        var identity = TestIdentityFactory.NewNodeIdentity(signer);
        var opts = Options.Create(new GossipDaemonOptions
        {
            RoundIntervalSeconds = 1,
            PeerPickCount = 1,
            ConnectTimeoutSeconds = 2,
            DeadPeerBackoffSeconds = 60,
        });
        var daemon = new GossipDaemon(
            transport, new VectorClock(), opts,
            new InMemoryNodeIdentityProvider(identity), signer);
        _cleanup.Add(daemon);

        daemon.AddPeer("no-such-endpoint", new byte[32]);

        var frameTcs = new TaskCompletionSource<GossipFrameEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        daemon.FrameReceived += (_, e) => frameTcs.TrySetResult(e);

        await daemon.StartAsync(CancellationToken.None);
        var frame = await frameTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await daemon.StopAsync(CancellationToken.None);

        Assert.Equal(GossipFrameType.GossipError, frame.FrameType);
        Assert.Equal("no-such-endpoint", frame.PeerEndpoint);
        // PeerNodeId is derived from the first 16 bytes of the peer's stored
        // public key — we passed a zero buffer, so it's 32 chars of zeros.
        Assert.Equal(new string('0', 32), frame.PeerNodeId);
        Assert.NotNull(frame.Summary);
        Assert.Contains("no-such-endpoint", frame.Summary!, StringComparison.Ordinal);
        // OccurredAt must be recent (within the test window).
        Assert.True(DateTimeOffset.UtcNow - frame.OccurredAt < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task FrameReceived_fires_Hello_and_GossipPing_on_successful_exchange()
    {
        // Port of GossipDaemonTests.Two_Daemons_Complete_Handshake_And_Merge_VectorClocks —
        // asserts the *event* surface rather than the vector-clock merge.
        var endpointB = NewEndpoint();
        await using var transportA = new InMemorySyncDaemonTransport();
        await using var transportB = new InMemorySyncDaemonTransport(endpointB);

        var signerA = TestIdentityFactory.NewSigner();
        var identityA = TestIdentityFactory.NewNodeIdentity(signerA);
        var signerB = TestIdentityFactory.NewSigner();
        var identityB = TestIdentityFactory.NewNodeIdentity(signerB);

        var opts = Options.Create(new GossipDaemonOptions
        {
            RoundIntervalSeconds = 1,
            PeerPickCount = 1,
            ConnectTimeoutSeconds = 3,
            DeadPeerBackoffSeconds = 60,
        });
        var daemonA = new GossipDaemon(
            transportA, new VectorClock(), opts,
            new InMemoryNodeIdentityProvider(identityA), signerA);
        _cleanup.Add(daemonA);

        using var responderCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var conn in transportB.ListenAsync(responderCts.Token))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await using var _c = conn;
                            var identity = new LocalIdentity(
                                NodeId: identityB.NodeIdBytes,
                                PublicKey: identityB.PublicKey,
                                Signer: signerB,
                                PrivateKey: identityB.PrivateKey,
                                SchemaVersion: HandshakeProtocol.DefaultSchemaVersion,
                                SupportedVersions: HandshakeProtocol.DefaultSupportedVersions);
                            await HandshakeProtocol.RespondAsync(
                                conn,
                                identity,
                                policy: proposal => new AckMessage(
                                    GrantedSubscriptions: proposal.ProposedStreams,
                                    Rejected: Array.Empty<Rejection>()),
                                responderCts.Token);

                            var pingIn = await conn.ReceiveAsync(responderCts.Token);
                            Assert.IsType<GossipPingMessage>(pingIn);

                            var pingOut = new GossipPingMessage(
                                VectorClock: new Dictionary<string, ulong> { ["B"] = 1ul },
                                PeerMembershipDelta: new MembershipDelta(
                                    Array.Empty<byte[]>(),
                                    Array.Empty<byte[]>()),
                                MonotonicNonce: 1);
                            await conn.SendAsync(pingOut, responderCts.Token);
                        }
                        catch { /* responder end-of-test close */ }
                    }, responderCts.Token);
                }
            }
            catch { /* responder shutdown */ }
        }, responderCts.Token);

        daemonA.AddPeer(endpointB, new byte[32]);

        var seen = new List<GossipFrameEventArgs>();
        var gotHello = new SemaphoreSlim(0, 8);
        var gotPing = new SemaphoreSlim(0, 8);
        daemonA.FrameReceived += (_, e) =>
        {
            lock (seen) seen.Add(e);
            if (e.FrameType == GossipFrameType.Hello) gotHello.Release();
            else if (e.FrameType == GossipFrameType.GossipPing) gotPing.Release();
        };

        await daemonA.StartAsync(CancellationToken.None);

        Assert.True(await gotHello.WaitAsync(TimeSpan.FromSeconds(5)),
            "Expected a Hello FrameReceived event after the handshake.");
        Assert.True(await gotPing.WaitAsync(TimeSpan.FromSeconds(5)),
            "Expected a GossipPing FrameReceived event after the PING round-trip.");

        await daemonA.StopAsync(CancellationToken.None);
        responderCts.Cancel();

        lock (seen)
        {
            var hello = seen.FirstOrDefault(f => f.FrameType == GossipFrameType.Hello);
            Assert.NotNull(hello);
            Assert.Equal(endpointB, hello!.PeerEndpoint);
            // PeerNodeId derived from AddPeer's zeroed public key — we kept
            // the same "stored" identity as the existing test harness.
            Assert.Equal(new string('0', 32), hello.PeerNodeId);

            var ping = seen.FirstOrDefault(f => f.FrameType == GossipFrameType.GossipPing);
            Assert.NotNull(ping);
            Assert.Equal(endpointB, ping!.PeerEndpoint);
        }
    }
}
