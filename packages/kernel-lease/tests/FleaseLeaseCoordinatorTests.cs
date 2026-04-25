using Microsoft.Extensions.Options;

namespace Sunfish.Kernel.Lease.Tests;

/// <summary>
/// End-to-end coverage for <see cref="FleaseLeaseCoordinator"/> using the
/// <see cref="InMemorySyncDaemonTransport"/> from Kernel.Sync Wave 2.1 to
/// stand up 3- and 5-node clusters in-process.
/// </summary>
/// <remarks>
/// <para>
/// Each "node" in a test cluster is one <see cref="Node"/> instance: a
/// listener transport on a unique endpoint, a stub <see cref="IGossipDaemon"/>
/// whose <c>KnownPeers</c> returns every other node's endpoint, and one
/// <see cref="FleaseLeaseCoordinator"/>. The coordinators talk to each other
/// over the in-memory transport registry.
/// </para>
/// <para>
/// <b>Partition simulation.</b> The in-memory harness cannot produce a real
/// network partition — <c>ConnectAsync</c> either finds a listener or
/// immediately throws <see cref="System.IO.IOException"/>. We simulate a
/// partitioned peer by never constructing its coordinator (no listener on
/// the endpoint). The proposer observes an instant connect failure and
/// treats it as a no-vote, which exercises the same quorum-math branch as
/// a real timeout but without waiting for the 5-second proposal timer.
/// </para>
/// </remarks>
public class FleaseLeaseCoordinatorTests : IAsyncLifetime
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

    private static string NewEndpoint() => $"lease-{Guid.NewGuid():N}";

    // ------------------------------------------------------------------
    // Test harness: a Node bundles transport + gossip stub + coordinator.
    // ------------------------------------------------------------------

    private sealed class Node : IAsyncDisposable
    {
        public string Endpoint { get; }
        public string NodeId { get; }
        public InMemorySyncDaemonTransport Transport { get; }
        public StubGossipDaemon Gossip { get; }
        public FleaseLeaseCoordinator Coordinator { get; }

        public Node(string nodeId, string endpoint, LeaseCoordinatorOptions? options = null)
        {
            NodeId = nodeId;
            Endpoint = endpoint;
            Transport = new InMemorySyncDaemonTransport(endpoint);
            Gossip = new StubGossipDaemon();
            var opts = Options.Create(options ?? new LeaseCoordinatorOptions
            {
                DefaultLeaseDuration = TimeSpan.FromSeconds(30),
                ProposalTimeout = TimeSpan.FromSeconds(2),
                ExpiryPruneInterval = TimeSpan.FromMilliseconds(200),
            });
            Coordinator = new FleaseLeaseCoordinator(Transport, Gossip, opts, nodeId, endpoint);
        }

        public async ValueTask DisposeAsync()
        {
            await Coordinator.DisposeAsync();
            await Transport.DisposeAsync();
        }
    }

    private sealed class StubGossipDaemon : IGossipDaemon
    {
        private readonly List<PeerInfo> _peers = new();

        public IReadOnlyCollection<PeerInfo> KnownPeers => _peers.ToList();

        public bool IsRunning => false;

        public void AddPeer(string peerEndpoint, byte[] peerPublicKey)
            => _peers.Add(new PeerInfo(peerEndpoint, peerPublicKey, DateTimeOffset.MinValue, 0));

        public void RemovePeer(string peerEndpoint)
            => _peers.RemoveAll(p => p.Endpoint == peerEndpoint);

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public event EventHandler<GossipRoundCompletedEventArgs>? RoundCompleted { add { } remove { } }
        public event EventHandler<GossipFrameEventArgs>? FrameReceived { add { } remove { } }
    }

    private List<Node> BuildCluster(int size, LeaseCoordinatorOptions? options = null)
    {
        var endpoints = Enumerable.Range(0, size).Select(_ => NewEndpoint()).ToList();
        var nodes = new List<Node>();
        for (var i = 0; i < size; i++)
        {
            var node = new Node($"node-{i}", endpoints[i], options);
            nodes.Add(node);
            _cleanup.Add(node);
        }
        // Full mesh: every node's gossip stub knows every other endpoint.
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                if (i == j) continue;
                nodes[i].Gossip.AddPeer(endpoints[j], new byte[32]);
            }
        }
        return nodes;
    }

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task ThreeNode_Cluster_Acquire_Then_Other_Denied()
    {
        var nodes = BuildCluster(3);

        var leaseA = await nodes[0].Coordinator.AcquireAsync("order:42", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.NotNull(leaseA);
        Assert.Equal("order:42", leaseA!.ResourceId);
        Assert.True(nodes[0].Coordinator.Holds("order:42"));

        // Another node tries to take the same resource — responders on nodes
        // 0 and 2 both have "order:42" locked (node 0 because it holds it,
        // node 2 because it granted it), so B sees a majority-denied round
        // and AcquireAsync returns null.
        var leaseB = await nodes[1].Coordinator.AcquireAsync("order:42", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.Null(leaseB);
        Assert.False(nodes[1].Coordinator.Holds("order:42"));
    }

    [Fact]
    public async Task ThreeNode_OnePartitioned_Quorum_Still_Grants()
    {
        // Cluster of 3 but only wire up 2 coordinators. Node 0's gossip
        // stub claims all 3 peers exist, so it proposes to the offline
        // peer and gets an instant connect error — that counts as
        // "no-vote" but 2/3 (node 0 self + node 1 grant) ≥ ceil(3/2)+1 = 2
        // so quorum is reached.
        var endpoints = new[] { NewEndpoint(), NewEndpoint(), NewEndpoint() };
        var n0 = new Node("node-0", endpoints[0]);
        var n1 = new Node("node-1", endpoints[1]);
        _cleanup.Add(n0);
        _cleanup.Add(n1);

        // node 0 sees both node 1 (alive) and node 2 (partitioned) as peers.
        n0.Gossip.AddPeer(endpoints[1], new byte[32]);
        n0.Gossip.AddPeer(endpoints[2], new byte[32]);

        var lease = await n0.Coordinator.AcquireAsync("order:7", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.NotNull(lease);
        Assert.True(n0.Coordinator.Holds("order:7"));
    }

    [Fact]
    public async Task ThreeNode_TwoPartitioned_QuorumUnreachable_Returns_Null()
    {
        // Cluster of 3 but only one coordinator alive. 1/3 does not meet
        // ceil(3/2)+1 = 2.
        var endpoints = new[] { NewEndpoint(), NewEndpoint(), NewEndpoint() };
        var n0 = new Node("node-0", endpoints[0]);
        _cleanup.Add(n0);
        n0.Gossip.AddPeer(endpoints[1], new byte[32]);
        n0.Gossip.AddPeer(endpoints[2], new byte[32]);

        var lease = await n0.Coordinator.AcquireAsync("order:99", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.Null(lease);
        Assert.False(n0.Coordinator.Holds("order:99"));
    }

    [Fact]
    public async Task Lease_Expires_After_Duration()
    {
        var nodes = BuildCluster(3);
        var lease = await nodes[0].Coordinator.AcquireAsync("order:x", TimeSpan.FromMilliseconds(200), CancellationToken.None);
        Assert.NotNull(lease);
        Assert.True(nodes[0].Coordinator.Holds("order:x"));

        // Wait past the expiry; Holds must flip to false without anyone
        // calling Release. 750 ms = 200 ms lease + 550 ms slack for the
        // test agent (pruner runs at 200 ms).
        await Task.Delay(TimeSpan.FromMilliseconds(750));
        Assert.False(nodes[0].Coordinator.Holds("order:x"));
    }

    [Fact]
    public async Task Release_Clears_Holds_And_Unblocks_Peers()
    {
        var nodes = BuildCluster(3);

        var leaseA = await nodes[0].Coordinator.AcquireAsync("order:r", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.NotNull(leaseA);

        await nodes[0].Coordinator.ReleaseAsync(leaseA!, CancellationToken.None);
        Assert.False(nodes[0].Coordinator.Holds("order:r"));

        // Release broadcast is fire-and-forget at the wire level: our
        // ReleaseAsync returns as soon as SendAsync completes, but each
        // peer responder has its own task that must drain the channel
        // and apply HandleLeaseRelease. Retry the follow-up Acquire for
        // a short window — the "unblocks peers" claim is satisfied as
        // long as node 1 eventually succeeds within a bounded wait.
        Lease? leaseB = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (leaseB is null && sw.Elapsed < TimeSpan.FromSeconds(3))
        {
            leaseB = await nodes[1].Coordinator.AcquireAsync(
                "order:r", TimeSpan.FromSeconds(30), CancellationToken.None);
            if (leaseB is null) await Task.Delay(50);
        }

        Assert.NotNull(leaseB);
        Assert.True(nodes[1].Coordinator.Holds("order:r"));
    }

    [Fact]
    public async Task Concurrent_Acquire_Same_Resource_Exactly_One_Wins()
    {
        var nodes = BuildCluster(3);

        // Two different nodes race for the same resource. Exactly one
        // must win; the loser must see null.
        var t0 = nodes[0].Coordinator.AcquireAsync("order:race", TimeSpan.FromSeconds(30), CancellationToken.None);
        var t1 = nodes[1].Coordinator.AcquireAsync("order:race", TimeSpan.FromSeconds(30), CancellationToken.None);

        var results = await Task.WhenAll(t0, t1);

        var wins = results.Count(r => r is not null);
        Assert.Equal(1, wins);
    }

    [Fact]
    public async Task FiveNode_Cluster_ThreeOfFive_Grants()
    {
        // 5 alive, quorum = ceil(5/2)+1 = 3. Node 0 self + 4 peers all grant
        // → lease acquired.
        var nodes = BuildCluster(5);

        var lease = await nodes[0].Coordinator.AcquireAsync("order:five", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.NotNull(lease);
        // Quorum participants must include us plus at least (quorum-1) peers.
        Assert.True(lease!.QuorumParticipants.Count >= 3);
    }

    [Fact]
    public async Task FiveNode_Cluster_TwoOfFive_Fails()
    {
        // 5 in membership, only 2 online (us + 1). ceil(5/2)+1 = 3. We see
        // 2 grants (self + peer 1) and 3 instant connect errors → null.
        var endpoints = Enumerable.Range(0, 5).Select(_ => NewEndpoint()).ToList();
        var n0 = new Node("node-0", endpoints[0]);
        var n1 = new Node("node-1", endpoints[1]);
        _cleanup.Add(n0);
        _cleanup.Add(n1);
        // node 0 believes all 5 peers exist (membership lag).
        for (var i = 1; i < 5; i++)
        {
            n0.Gossip.AddPeer(endpoints[i], new byte[32]);
        }

        var lease = await n0.Coordinator.AcquireAsync("order:five-two", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.Null(lease);
    }

    [Fact]
    public async Task Release_Is_Idempotent()
    {
        var nodes = BuildCluster(3);
        var lease = await nodes[0].Coordinator.AcquireAsync("order:idem", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.NotNull(lease);

        await nodes[0].Coordinator.ReleaseAsync(lease!, CancellationToken.None);
        // Second release must not throw and must not surface any state.
        await nodes[0].Coordinator.ReleaseAsync(lease!, CancellationToken.None);
        Assert.False(nodes[0].Coordinator.Holds("order:idem"));
    }

    [Fact]
    public async Task Holds_On_NonHeld_Resource_Returns_False()
    {
        var nodes = BuildCluster(3);
        Assert.False(nodes[0].Coordinator.Holds("never-acquired"));
    }

    [Fact]
    public async Task Explicit_QuorumSize_Override_Respected()
    {
        // Quorum of 1 makes every acquire trivial — even with two
        // partitioned peers in a 3-node cluster, our self-grant satisfies
        // quorum and the lease is issued.
        var options = new LeaseCoordinatorOptions
        {
            DefaultLeaseDuration = TimeSpan.FromSeconds(30),
            ProposalTimeout = TimeSpan.FromSeconds(2),
            ExpiryPruneInterval = TimeSpan.FromMilliseconds(200),
            QuorumSize = 1,
        };
        var endpoints = new[] { NewEndpoint(), NewEndpoint(), NewEndpoint() };
        var n0 = new Node("node-0", endpoints[0], options);
        _cleanup.Add(n0);
        // Membership claims two dead peers.
        n0.Gossip.AddPeer(endpoints[1], new byte[32]);
        n0.Gossip.AddPeer(endpoints[2], new byte[32]);

        var lease = await n0.Coordinator.AcquireAsync("order:q1", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.NotNull(lease);
    }

    [Fact]
    public async Task Proposal_Timeout_Returns_Null_Without_Throwing()
    {
        // Two-node cluster with one silent peer endpoint registered in
        // the in-memory registry but not running a coordinator → the peer
        // connection opens (listener exists) but no response ever lands.
        var e0 = NewEndpoint();
        var e1 = NewEndpoint();
        // Silent-peer transport: listens but never reads messages off the
        // connection it receives.
        var silentTransport = new InMemorySyncDaemonTransport(e1);
        _cleanup.Add(silentTransport);

        var options = new LeaseCoordinatorOptions
        {
            DefaultLeaseDuration = TimeSpan.FromSeconds(30),
            ProposalTimeout = TimeSpan.FromMilliseconds(400),
            ExpiryPruneInterval = TimeSpan.FromMilliseconds(200),
        };
        var n0 = new Node("node-0", e0, options);
        _cleanup.Add(n0);
        // Membership claims e1 + one more dead peer so the silent peer
        // genuinely matters for quorum (we need 2/3 grants: self + silent).
        n0.Gossip.AddPeer(e1, new byte[32]);
        n0.Gossip.AddPeer(NewEndpoint(), new byte[32]);

        // Drain connections on the silent listener so ConnectAsync does
        // not leak — but never reply.
        using var silentCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var conn in silentTransport.ListenAsync(silentCts.Token))
                {
                    _ = conn; // keep alive; do not read/write
                }
            }
            catch { /* shutdown */ }
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lease = await n0.Coordinator.AcquireAsync("order:slow", TimeSpan.FromSeconds(30), CancellationToken.None);
        sw.Stop();
        silentCts.Cancel();

        Assert.Null(lease);
        // Must have respected our 400 ms budget (with generous slack).
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"Took {sw.Elapsed.TotalMilliseconds} ms.");
    }

    [Fact]
    public async Task AcquireAsync_Cancels_Cleanly_When_Ct_Triggered()
    {
        // Three-node membership with a silent-listener peer: connections
        // are accepted but no reply ever arrives, so AcquireAsync sits
        // inside the proposal WhenAny loop and external cancellation has
        // something meaningful to cancel.
        var e0 = NewEndpoint();
        var eSilent = NewEndpoint();
        var silentTransport = new InMemorySyncDaemonTransport(eSilent);
        _cleanup.Add(silentTransport);

        using var silentCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var conn in silentTransport.ListenAsync(silentCts.Token))
                {
                    _ = conn; // accept and forget — never reply
                }
            }
            catch { /* shutdown */ }
        });

        var options = new LeaseCoordinatorOptions
        {
            DefaultLeaseDuration = TimeSpan.FromSeconds(30),
            // Long proposal timeout so cancellation (not timeout) drives the exit.
            ProposalTimeout = TimeSpan.FromSeconds(30),
            ExpiryPruneInterval = TimeSpan.FromMilliseconds(200),
        };
        var n0 = new Node("node-0", e0, options);
        _cleanup.Add(n0);
        n0.Gossip.AddPeer(eSilent, new byte[32]);
        n0.Gossip.AddPeer(NewEndpoint(), new byte[32]); // dead peer — connect fails fast

        using var userCts = new CancellationTokenSource();
        userCts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await n0.Coordinator.AcquireAsync("order:cancel-me", TimeSpan.FromSeconds(30), userCts.Token);
        });

        silentCts.Cancel();
    }
}
