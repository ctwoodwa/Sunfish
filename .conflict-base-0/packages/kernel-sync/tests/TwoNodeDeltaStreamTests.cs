using Microsoft.Extensions.Options;

using Sunfish.Kernel.Crdt;
using Sunfish.Kernel.Crdt.Backends;
using Sunfish.Kernel.Sync.Application;
using Sunfish.Kernel.Sync.Discovery;

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Phase 1 G2 — Wave 2.5 DELTA_STREAM application acceptance test. Spins up
/// two in-process gossip daemons over <see cref="InMemorySyncDaemonTransport"/>,
/// has node A mutate a CRDT through its <see cref="ICrdtDocument"/>, runs a
/// gossip round, asserts node B's local CRDT projection reflects the
/// mutation. Closes the conformance gap from
/// <c>icm/05_implementation-plan/output/business-mvp-phase-1-plan-2026-04-26.md</c>
/// (G2 task) and Kleppmann property P3 (network optional) + P4
/// (collaboration).
/// </summary>
/// <remarks>
/// Architecture mirrors the existing
/// <c>Two_Daemons_Complete_Handshake_And_Merge_VectorClocks</c> in
/// <see cref="GossipDaemonTests"/>: daemon A is the round initiator, daemon
/// B is a hand-rolled responder loop on the listener transport (the
/// <see cref="GossipDaemon"/> doesn't ship a built-in responder yet — that's
/// a future wave). The responder mirrors the wire convention from the
/// initiator's round loop: read PING + DELTA_STREAM, then send PING +
/// DELTA_STREAM back.
/// </remarks>
public class TwoNodeDeltaStreamTests : IAsyncLifetime
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

    [Fact]
    public async Task TwoNode_DeltaStream_AppliesToReceiver_CRDT()
    {
        // ----- Arrange ----------------------------------------------------
        var endpointB = $"gossip-delta-{Guid.NewGuid():N}";
        await using var transportA = new InMemorySyncDaemonTransport();
        await using var transportB = new InMemorySyncDaemonTransport(endpointB);

        var engine = new StubCrdtEngine();
        await using var docA = engine.CreateDocument("default");
        await using var docB = engine.CreateDocument("default");

        // Node A mutates a text container — this is the "local edit" the
        // gossip round must propagate to node B.
        var textA = docA.GetText("greeting");
        textA.Insert(0, "hello");

        // Wire daemon A with a real producer that encodes from docA.
        // Sink on A is the no-op default — A doesn't need to apply inbound
        // deltas in this test (we only assert A → B propagation).
        var producerA = new CrdtDocumentDeltaProducer(docA);

        var signerA = TestIdentityFactory.NewSigner();
        var identityA = TestIdentityFactory.NewNodeIdentity(signerA);
        var signerB = TestIdentityFactory.NewSigner();
        var identityB = TestIdentityFactory.NewNodeIdentity(signerB);

        var daemonA = BuildDaemon(
            transportA,
            roundSeconds: 1,
            peerPickCount: 1,
            identityProvider: new InMemoryNodeIdentityProvider(identityA),
            signer: signerA,
            deltaProducer: producerA);
        _cleanup.Add(daemonA);

        // ----- Responder on B --------------------------------------------
        // Hand-rolled. Mirrors the wire order the initiator's round loop
        // produces: HELLO ladder, then PING, then DELTA_STREAM (from A to B),
        // then PING (from B to A), then DELTA_STREAM (from B to A — empty
        // payload because docB has no local mutations).
        var deltaApplied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
                            var bIdentity = new LocalIdentity(
                                NodeId: identityB.NodeIdBytes,
                                PublicKey: identityB.PublicKey,
                                Signer: signerB,
                                PrivateKey: identityB.PrivateKey,
                                SchemaVersion: HandshakeProtocol.DefaultSchemaVersion,
                                SupportedVersions: HandshakeProtocol.DefaultSupportedVersions);
                            await HandshakeProtocol.RespondAsync(
                                conn,
                                bIdentity,
                                policy: proposal => new AckMessage(
                                    GrantedSubscriptions: proposal.ProposedStreams,
                                    Rejected: Array.Empty<Rejection>()),
                                responderCts.Token);

                            // Wire order from initiator: PING → DELTA_STREAM.
                            var pingIn = await conn.ReceiveAsync(responderCts.Token);
                            Assert.IsType<GossipPingMessage>(pingIn);

                            var deltaIn = await conn.ReceiveAsync(responderCts.Token);
                            var delta = Assert.IsType<DeltaStreamMessage>(deltaIn);

                            // Apply to docB — this is the assertion we're
                            // really making: the wire-transported delta
                            // bytes round-trip through the engine cleanly.
                            docB.ApplyDelta(delta.CrdtOps);
                            deltaApplied.TrySetResult();

                            // Mirror back: PING + DELTA_STREAM (empty).
                            var pingOut = new GossipPingMessage(
                                VectorClock: new Dictionary<string, ulong>(),
                                PeerMembershipDelta: new MembershipDelta(
                                    Array.Empty<byte[]>(),
                                    Array.Empty<byte[]>()),
                                MonotonicNonce: 1);
                            await conn.SendAsync(pingOut, responderCts.Token);

                            var emptyDelta = new DeltaStreamMessage(
                                StreamId: "default",
                                OpSequence: 1,
                                CrdtOps: Array.Empty<byte>());
                            await conn.SendAsync(emptyDelta, responderCts.Token);
                        }
                        catch { /* responder end-of-test close */ }
                    }, responderCts.Token);
                }
            }
            catch { /* responder shutdown */ }
        }, responderCts.Token);

        // ----- Act --------------------------------------------------------
        daemonA.AddPeer(endpointB, new byte[32]);
        await daemonA.StartAsync(CancellationToken.None);

        var applied = await Task.WhenAny(
            deltaApplied.Task,
            Task.Delay(TimeSpan.FromSeconds(5)));
        await daemonA.StopAsync(CancellationToken.None);
        responderCts.Cancel();

        // ----- Assert -----------------------------------------------------
        Assert.True(
            ReferenceEquals(applied, deltaApplied.Task),
            "Daemon A should have completed a round and shipped a DELTA_STREAM to B within 5s.");

        var textB = docB.GetText("greeting");
        Assert.Equal("hello", textB.Value);
    }

    private GossipDaemon BuildDaemon(
        ISyncDaemonTransport transport,
        VectorClock? clock = null,
        int roundSeconds = 1,
        int peerPickCount = 2,
        int connectTimeoutSeconds = 2,
        int deadPeerBackoffSeconds = 60,
        INodeIdentityProvider? identityProvider = null,
        IEd25519Signer? signer = null,
        IDeltaProducer? deltaProducer = null,
        IDeltaSink? deltaSink = null)
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
        return new GossipDaemon(
            transport,
            clock ?? new VectorClock(),
            opts,
            identityProvider,
            signer,
            deltaProducer,
            deltaSink);
    }

    /// <summary>
    /// Test-local <see cref="IDeltaProducer"/> backed by a single
    /// <see cref="ICrdtDocument"/>. Phase 1 single-document convention.
    /// </summary>
    private sealed class CrdtDocumentDeltaProducer : IDeltaProducer
    {
        private readonly ICrdtDocument _doc;
        public CrdtDocumentDeltaProducer(ICrdtDocument doc) => _doc = doc;

        public ValueTask<ReadOnlyMemory<byte>?> EncodeOutboundDeltaAsync(
            string documentId,
            ReadOnlyMemory<byte> peerVectorClock,
            CancellationToken ct)
        {
            var bytes = _doc.EncodeDelta(peerVectorClock);
            return ValueTask.FromResult<ReadOnlyMemory<byte>?>(bytes);
        }
    }
}
