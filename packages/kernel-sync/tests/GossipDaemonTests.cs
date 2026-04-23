using Microsoft.Extensions.Options;

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Coverage for <see cref="GossipDaemon"/>. Tests use a tight
/// <see cref="GossipDaemonOptions.RoundIntervalSeconds"/> = 1 and rely on
/// the daemon firing an immediate first round on start, plus the
/// <see cref="IGossipDaemon.RoundCompleted"/> event for synchronisation.
/// </summary>
public class GossipDaemonTests : IAsyncLifetime
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

    private static string NewEndpoint() => $"gossip-{Guid.NewGuid():N}";

    private static GossipDaemon BuildDaemon(
        ISyncDaemonTransport transport,
        VectorClock? clock = null,
        int roundSeconds = 1,
        int peerPickCount = 2,
        int connectTimeoutSeconds = 2,
        int deadPeerBackoffSeconds = 60,
        INodeIdentityProvider? identityProvider = null,
        IEd25519Signer? signer = null)
    {
        var opts = Options.Create(new GossipDaemonOptions
        {
            RoundIntervalSeconds = roundSeconds,
            PeerPickCount = peerPickCount,
            ConnectTimeoutSeconds = connectTimeoutSeconds,
            DeadPeerBackoffSeconds = deadPeerBackoffSeconds,
        });
        signer ??= TestIdentityFactory.NewSigner();
        identityProvider ??= new InMemoryNodeIdentityProvider(
            TestIdentityFactory.NewNodeIdentity(signer));
        return new GossipDaemon(transport, clock ?? new VectorClock(), opts, identityProvider, signer);
    }

    [Fact]
    public async Task Start_Then_Stop_Idempotent_And_No_Throw()
    {
        await using var transport = new InMemorySyncDaemonTransport();
        var daemon = BuildDaemon(transport);
        _cleanup.Add(daemon);

        await daemon.StartAsync(CancellationToken.None);
        await daemon.StartAsync(CancellationToken.None); // idempotent

        await daemon.StopAsync(CancellationToken.None);
        await daemon.StopAsync(CancellationToken.None); // idempotent
    }

    [Fact]
    public async Task AddPeer_Then_KnownPeers_Contains_It()
    {
        await using var transport = new InMemorySyncDaemonTransport();
        var daemon = BuildDaemon(transport);
        _cleanup.Add(daemon);

        daemon.AddPeer("endpoint-1", new byte[32]);
        daemon.AddPeer("endpoint-2", new byte[32]);

        Assert.Equal(2, daemon.KnownPeers.Count);
        Assert.Contains(daemon.KnownPeers, p => p.Endpoint == "endpoint-1");
        Assert.Contains(daemon.KnownPeers, p => p.Endpoint == "endpoint-2");
    }

    [Fact]
    public async Task RemovePeer_Drops_It_From_KnownPeers()
    {
        await using var transport = new InMemorySyncDaemonTransport();
        var daemon = BuildDaemon(transport);
        _cleanup.Add(daemon);

        daemon.AddPeer("endpoint-1", new byte[32]);
        daemon.AddPeer("endpoint-2", new byte[32]);
        daemon.RemovePeer("endpoint-1");

        Assert.Single(daemon.KnownPeers);
        Assert.Equal("endpoint-2", daemon.KnownPeers.First().Endpoint);
    }

    [Fact]
    public async Task RoundCompleted_Fires_Even_With_No_Peers()
    {
        await using var transport = new InMemorySyncDaemonTransport();
        var daemon = BuildDaemon(transport);
        _cleanup.Add(daemon);

        var tcs = new TaskCompletionSource<GossipRoundCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        daemon.RoundCompleted += (_, e) => tcs.TrySetResult(e);

        await daemon.StartAsync(CancellationToken.None);
        var round = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await daemon.StopAsync(CancellationToken.None);

        Assert.Equal(0, round.PeersSelected);
        Assert.Equal(0, round.DeltasExchanged);
    }

    [Fact]
    public async Task Dead_Peer_Skipped_Until_Backoff_Expires()
    {
        // A peer that doesn't exist on the in-memory registry causes
        // ConnectAsync to throw. The daemon catches this, marks the peer
        // dead, and SkipUntil should push the peer out of the eligible set
        // for the next several rounds.
        await using var transport = new InMemorySyncDaemonTransport();
        // deadPeerBackoffSeconds: 10 so the skip window is well longer than
        // the one-second round tick.
        var daemon = BuildDaemon(transport, roundSeconds: 1, peerPickCount: 1, deadPeerBackoffSeconds: 10);
        _cleanup.Add(daemon);

        daemon.AddPeer("does-not-exist", new byte[32]);

        // Collect two rounds' worth of events; the first should pick the peer
        // (and fail), the second should skip it (PeersSelected == 0).
        var rounds = new List<GossipRoundCompletedEventArgs>();
        var gate = new SemaphoreSlim(0, 10);
        daemon.RoundCompleted += (_, e) =>
        {
            lock (rounds) rounds.Add(e);
            gate.Release();
        };

        await daemon.StartAsync(CancellationToken.None);

        // Wait for two rounds to land.
        await gate.WaitAsync(TimeSpan.FromSeconds(3));
        await gate.WaitAsync(TimeSpan.FromSeconds(3));

        await daemon.StopAsync(CancellationToken.None);

        lock (rounds)
        {
            Assert.True(rounds.Count >= 2);
            // First round picks and fails → PeersSelected == 1,
            // DeltasExchanged == 0 because the connect threw.
            Assert.Equal(1, rounds[0].PeersSelected);
            Assert.Equal(0, rounds[0].DeltasExchanged);
            // Second round: peer is still in SkipUntil window, so eligible
            // set is empty and we see PeersSelected == 0.
            Assert.Equal(0, rounds[1].PeersSelected);
        }
    }

    [Fact]
    public async Task Two_Daemons_Complete_Handshake_And_Merge_VectorClocks()
    {
        // Daemon A has the listener + runs the round loop.
        // A "peer responder" task runs on Daemon B's listener transport and
        // calls HandshakeProtocol.RespondAsync then reads the PING and
        // responds with its own PING carrying a different vector clock
        // snapshot.
        var endpointB = NewEndpoint();
        await using var transportA = new InMemorySyncDaemonTransport();
        await using var transportB = new InMemorySyncDaemonTransport(endpointB);

        // Real Ed25519 keypairs on both sides so the signed HELLO handshake
        // verifies end-to-end. A and B each get their own signer instance;
        // the handshake code path is stateless and allows that.
        var signerA = TestIdentityFactory.NewSigner();
        var identityA = TestIdentityFactory.NewNodeIdentity(signerA);
        var signerB = TestIdentityFactory.NewSigner();
        var identityB = TestIdentityFactory.NewNodeIdentity(signerB);

        var clockA = new VectorClock();
        clockA.Set("A", 5);
        var daemonA = BuildDaemon(
            transportA, clockA, roundSeconds: 1, peerPickCount: 1,
            identityProvider: new InMemoryNodeIdentityProvider(identityA),
            signer: signerA);
        _cleanup.Add(daemonA);

        // Responder loop: accept every inbound connection on B.
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

                            // Now read the initiator's PING and send our own
                            // PING back carrying a divergent clock.
                            var pingIn = await conn.ReceiveAsync(responderCts.Token);
                            Assert.IsType<GossipPingMessage>(pingIn);

                            var pingOut = new GossipPingMessage(
                                VectorClock: new Dictionary<string, ulong>
                                {
                                    ["B"] = 11ul,
                                },
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

        var roundGate = new SemaphoreSlim(0, 10);
        daemonA.RoundCompleted += (_, e) => { if (e.DeltasExchanged > 0) roundGate.Release(); };

        await daemonA.StartAsync(CancellationToken.None);

        var fired = await roundGate.WaitAsync(TimeSpan.FromSeconds(5));
        await daemonA.StopAsync(CancellationToken.None);
        responderCts.Cancel();

        Assert.True(fired, "At least one round should have exchanged with the responder.");
        // Daemon A's clock should now include B's value after merge.
        Assert.Equal(11ul, clockA.Get("B"));
        // A's own entry should be preserved.
        Assert.Equal(5ul, clockA.Get("A"));
    }

    [Fact]
    public async Task Start_Throws_If_Disposed()
    {
        await using var transport = new InMemorySyncDaemonTransport();
        var daemon = BuildDaemon(transport);

        await daemon.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await daemon.StartAsync(CancellationToken.None));
    }
}
