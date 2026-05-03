using Microsoft.Extensions.Options;

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Coverage for GOSSIP_PING monotonic-nonce replay protection
/// (sync-daemon-protocol §8). The daemon tracks a per-peer last-seen
/// nonce and drops PINGs whose nonce is not strictly greater.
/// </summary>
public class GossipPingNonceTests : IAsyncLifetime
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

    private GossipDaemon BuildDaemon()
    {
        var transport = new InMemorySyncDaemonTransport();
        _cleanup.Add(transport);

        var opts = Options.Create(new GossipDaemonOptions
        {
            RoundIntervalSeconds = 30, // we are not exercising the round loop
            PeerPickCount = 1,
            ConnectTimeoutSeconds = 2,
            DeadPeerBackoffSeconds = 60,
        });
        var signer = TestIdentityFactory.NewSigner();
        var identity = new InMemoryNodeIdentityProvider(
            TestIdentityFactory.NewNodeIdentity(signer));
        var daemon = new GossipDaemon(transport, new VectorClock(), opts, identity, signer);
        _cleanup.Add(daemon);
        return daemon;
    }

    // ------------------------------------------------------------------
    // Send-side: round-loop increments its outbound nonce on each tick.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Daemon_Increments_Nonce_On_Each_Round()
    {
        // Instead of wiring a responder that echoes the outbound PING back,
        // we observe the outbound side by snooping the in-memory connection
        // the daemon connects to — the listener on the peer side receives
        // PINGs with strictly-increasing nonces.

        var peerEndpoint = $"peer-{Guid.NewGuid():N}";
        await using var peerTransport = new InMemorySyncDaemonTransport(peerEndpoint);

        var signerA = TestIdentityFactory.NewSigner();
        var idA = new InMemoryNodeIdentityProvider(TestIdentityFactory.NewNodeIdentity(signerA));
        await using var clientTransport = new InMemorySyncDaemonTransport();
        var opts = Options.Create(new GossipDaemonOptions
        {
            RoundIntervalSeconds = 1,
            PeerPickCount = 1,
            ConnectTimeoutSeconds = 2,
            DeadPeerBackoffSeconds = 60,
        });
        var daemon = new GossipDaemon(clientTransport, new VectorClock(), opts, idA, signerA);
        _cleanup.Add(daemon);

        var signerB = TestIdentityFactory.NewSigner();
        var idB = TestIdentityFactory.NewNodeIdentity(signerB);
        var bLocal = new LocalIdentity(
            NodeId: idB.NodeIdBytes,
            PublicKey: idB.PublicKey,
            Signer: signerB,
            PrivateKey: idB.PrivateKey,
            SchemaVersion: HandshakeProtocol.DefaultSchemaVersion,
            SupportedVersions: HandshakeProtocol.DefaultSupportedVersions);

        var nonces = new List<ulong>();
        var gate = new SemaphoreSlim(0, 10);
        var errors = new List<Exception>();
        using var responderCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var conn in peerTransport.ListenAsync(responderCts.Token))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await using var _c = conn;
                            await HandshakeProtocol.RespondAsync(
                                conn, bLocal,
                                p => new AckMessage(p.ProposedStreams, Array.Empty<Rejection>()),
                                responderCts.Token);
                            var ping = await conn.ReceiveAsync(responderCts.Token);
                            if (ping is GossipPingMessage gp)
                            {
                                lock (nonces) nonces.Add(gp.MonotonicNonce);
                                gate.Release();

                                // Echo back a PING so the daemon's receive
                                // completes cleanly — otherwise dispose of
                                // this responder-side connection closes the
                                // channel while the daemon is mid-receive
                                // and throws a generic ChannelClosedException
                                // that the daemon's round-loop interprets as
                                // a peer-failure, pushing the peer into
                                // backoff and starving subsequent rounds.
                                var echo = new GossipPingMessage(
                                    VectorClock: new Dictionary<string, ulong>(),
                                    PeerMembershipDelta: new MembershipDelta(
                                        Array.Empty<byte[]>(), Array.Empty<byte[]>()),
                                    MonotonicNonce: gp.MonotonicNonce + 1);
                                await conn.SendAsync(echo, responderCts.Token);
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (errors) errors.Add(ex);
                        }
                    }, responderCts.Token);
                }
            }
            catch { /* listener shutdown */ }
        }, responderCts.Token);

        daemon.AddPeer(peerEndpoint, idB.PublicKey);
        await daemon.StartAsync(CancellationToken.None);

        // Wait for two PINGs to land.
        var first = await gate.WaitAsync(TimeSpan.FromSeconds(5));
        var second = await gate.WaitAsync(TimeSpan.FromSeconds(5));

        await daemon.StopAsync(CancellationToken.None);
        responderCts.Cancel();

        string diag;
        lock (errors)
            diag = errors.Count == 0
                ? "(no responder errors)"
                : string.Join("; ", errors.Select(e => e.GetType().Name + ": " + e.Message));

        Assert.True(first, "First PING should arrive. Errors: " + diag);
        Assert.True(second, "Second PING should arrive. Errors: " + diag);

        lock (nonces)
        {
            Assert.True(nonces.Count >= 2, "Should have captured at least two PINGs.");
            Assert.True(nonces[1] > nonces[0],
                $"Second nonce ({nonces[1]}) should strictly exceed first ({nonces[0]}).");
        }
    }

    // ------------------------------------------------------------------
    // Receive-side: per-peer last-seen nonce gate.
    // ------------------------------------------------------------------

    [Fact]
    public void Incoming_Nonce_Greater_Than_LastSeen_Is_Accepted()
    {
        var daemon = BuildDaemon();
        daemon.AddPeer("peer-1", new byte[32]);

        Assert.True(daemon.TryAdvancePeerNonce("peer-1", 1));
        Assert.True(daemon.TryAdvancePeerNonce("peer-1", 5));
        Assert.True(daemon.TryAdvancePeerNonce("peer-1", ulong.MaxValue - 1));

        var info = daemon.KnownPeers.Single(p => p.Endpoint == "peer-1");
        Assert.Equal(ulong.MaxValue - 1, info.LastSeenNonce);
    }

    [Fact]
    public void Incoming_Nonce_Equal_Or_Lower_Than_LastSeen_Is_Rejected_As_Replay()
    {
        var daemon = BuildDaemon();
        daemon.AddPeer("peer-1", new byte[32]);

        Assert.True(daemon.TryAdvancePeerNonce("peer-1", 10));

        // Equal → replay.
        Assert.False(daemon.TryAdvancePeerNonce("peer-1", 10));
        // Lower → replay.
        Assert.False(daemon.TryAdvancePeerNonce("peer-1", 1));
        // Zero (default for "unseen") is trivially ≤ 10 → replay.
        Assert.False(daemon.TryAdvancePeerNonce("peer-1", 0));

        var info = daemon.KnownPeers.Single(p => p.Endpoint == "peer-1");
        Assert.Equal(10ul, info.LastSeenNonce);
    }

    [Fact]
    public void Two_Peers_Have_Independent_Nonce_Streams()
    {
        var daemon = BuildDaemon();
        daemon.AddPeer("peer-a", new byte[32]);
        daemon.AddPeer("peer-b", new byte[32]);

        Assert.True(daemon.TryAdvancePeerNonce("peer-a", 100));
        // peer-b has never sent a PING, so even a nonce of 1 is accepted
        // (strictly greater than the implicit zero).
        Assert.True(daemon.TryAdvancePeerNonce("peer-b", 1));

        // peer-a's stream continues independently — 50 < 100 is a replay.
        Assert.False(daemon.TryAdvancePeerNonce("peer-a", 50));
        Assert.True(daemon.TryAdvancePeerNonce("peer-a", 200));

        var a = daemon.KnownPeers.Single(p => p.Endpoint == "peer-a");
        var b = daemon.KnownPeers.Single(p => p.Endpoint == "peer-b");
        Assert.Equal(200ul, a.LastSeenNonce);
        Assert.Equal(1ul, b.LastSeenNonce);
    }

    [Fact]
    public void Unknown_Peer_Always_Rejects_Nonce()
    {
        var daemon = BuildDaemon();
        // No AddPeer call → the daemon has no state for this endpoint.
        Assert.False(daemon.TryAdvancePeerNonce("never-added", 1));
        Assert.False(daemon.TryAdvancePeerNonce("never-added", ulong.MaxValue));
    }

    /// <summary>
    /// Nonce wraparound at <see cref="ulong.MaxValue"/> is documented as
    /// "not a real-world concern" — at 1000 PINGs/sec the counter would
    /// take roughly 99 billion years to wrap. This test pins that
    /// expectation so a future refactor doesn't accidentally introduce
    /// a wrap-around check that changes correctness.
    /// </summary>
    [Fact]
    public void Nonce_Wraparound_At_UlongMaxValue_Is_Not_Guarded()
    {
        var daemon = BuildDaemon();
        daemon.AddPeer("peer-1", new byte[32]);

        // Advance to just-below-max.
        Assert.True(daemon.TryAdvancePeerNonce("peer-1", ulong.MaxValue - 1));
        // ulong.MaxValue is still > (MaxValue - 1), so accepted.
        Assert.True(daemon.TryAdvancePeerNonce("peer-1", ulong.MaxValue));

        // Beyond ulong.MaxValue the next PING would be 0 (wrap). That would
        // be rejected as a replay — which is the correct outcome, and in
        // practice is never reached (~99 billion years at 1000 ops/sec).
        Assert.False(daemon.TryAdvancePeerNonce("peer-1", 0));
    }
}
