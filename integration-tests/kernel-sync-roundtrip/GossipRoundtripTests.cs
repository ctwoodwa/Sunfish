using Sunfish.Foundation.Assets.Common;
using Sunfish.Integration.KernelSyncRoundtrip.Harness;

namespace Sunfish.Integration.KernelSyncRoundtrip;

/// <summary>
/// Event-log gossip convergence tests over the real Unix-socket / named-pipe
/// transport. These tests stand up a custom session bridge on top of the
/// kernel-sync handshake ladder because the Wave 2.1
/// <see cref="GossipDaemon"/> exchanges vector-clock snapshots only — the
/// CRDT-op / event-log wiring on DELTA_STREAM is a later-wave deliverable
/// (see <see cref="GossipDaemon"/> xmldoc). The purpose of these tests is to
/// prove that the <b>transport</b> + <b>framing</b> + <b>handshake</b> are
/// wire-correct under real-world conditions; the event-log content exchange
/// is driven by the test harness on top of that substrate.
/// </summary>
public class GossipRoundtripTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Append_On_A_Shows_Up_On_B_After_One_Gossip_Roundtrip()
    {
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        await using var harness = await TwoNodeHarness.StartAsync(ct, enableLeaseResponder: false);

        // Seed: one event on A, zero on B.
        var evt = BuildEvent("entity-1", "entity.created");
        var assignedSeq = await harness.NodeA.EventLog.AppendAsync(evt, ct);
        Assert.Equal(1ul, assignedSeq);

        // Run the delta-replay session (handshake + replay-from-seq-0).
        await RunReplaySessionAsync(harness, pullerIsA: false, ct);

        // B should now see A's event.
        var entriesOnB = await CollectAsync(harness.NodeB.EventLog.ReadAfterAsync(0, ct))
            ;
        Assert.Single(entriesOnB);
        Assert.Equal(evt.Id, entriesOnB[0].Event.Id);
        Assert.Equal("entity.created", entriesOnB[0].Event.Kind);
    }

    [Fact]
    public async Task Two_Concurrent_Events_On_Different_Nodes_Both_Converge()
    {
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        await using var harness = await TwoNodeHarness.StartAsync(ct, enableLeaseResponder: false);

        var evtA = BuildEvent("entity-a", "entity.created");
        var evtB = BuildEvent("entity-b", "entity.updated");
        await harness.NodeA.EventLog.AppendAsync(evtA, ct);
        await harness.NodeB.EventLog.AppendAsync(evtB, ct);

        // Round 1: A pulls from B. B's event now lives on A.
        await RunReplaySessionAsync(harness, pullerIsA: true, ct);
        // Round 2: B pulls from A. A's event (evtA + previously-merged evtB)
        // now lives on B.
        await RunReplaySessionAsync(harness, pullerIsA: false, ct);

        var onA = (await CollectAsync(harness.NodeA.EventLog.ReadAfterAsync(0, ct))
            ).Select(e => e.Event.Id).ToHashSet();
        var onB = (await CollectAsync(harness.NodeB.EventLog.ReadAfterAsync(0, ct))
            ).Select(e => e.Event.Id).ToHashSet();

        Assert.Contains(evtA.Id, onA);
        Assert.Contains(evtB.Id, onA);
        Assert.Contains(evtA.Id, onB);
        Assert.Contains(evtB.Id, onB);
    }

    [Fact]
    public async Task Mid_Gossip_Disconnect_Then_Reconnect_Replays_Lost_Events()
    {
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        await using var harness = await TwoNodeHarness.StartAsync(ct, enableLeaseResponder: false);

        // Three events on A; simulate a drop after event #1 replicates,
        // then reconnect and verify the remaining two events still land on B.
        var evt1 = BuildEvent("x", "entity.created");
        var evt2 = BuildEvent("x", "entity.updated");
        var evt3 = BuildEvent("x", "entity.deleted");
        await harness.NodeA.EventLog.AppendAsync(evt1, ct);

        // Session 1: B pulls from A, carries only evt1.
        await RunReplaySessionAsync(harness, pullerIsA: false, ct);

        var afterFirst = await CollectAsync(harness.NodeB.EventLog.ReadAfterAsync(0, ct))
            ;
        Assert.Single(afterFirst);
        Assert.Equal(evt1.Id, afterFirst[0].Event.Id);

        // Simulate network drop: append evt2 and evt3 on A while the
        // transport is "idle" (no active session). A real cross-daemon
        // scenario would lose these until the next gossip tick.
        await harness.NodeA.EventLog.AppendAsync(evt2, ct);
        await harness.NodeA.EventLog.AppendAsync(evt3, ct);

        // Session 2: reconnect + replay. B already has evt1 so only evt2
        // and evt3 should flow.
        await RunReplaySessionAsync(harness, pullerIsA: false, ct);

        var afterReconnect = await CollectAsync(harness.NodeB.EventLog.ReadAfterAsync(0, ct))
            ;
        Assert.Equal(3, afterReconnect.Count);
        Assert.Equal(new[] { evt1.Id, evt2.Id, evt3.Id },
            afterReconnect.Select(e => e.Event.Id).ToArray());
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Run a single "delta replay" session. One side is the puller (dials
    /// out, initiates the handshake, then reads DELTA_STREAM frames until
    /// the peer signals end-of-stream). The other side is the pusher
    /// (accepts, responds to the handshake, then writes every entry after
    /// the puller's current sequence).
    /// </summary>
    private static async Task RunReplaySessionAsync(
        TwoNodeHarness harness,
        bool pullerIsA,
        CancellationToken ct)
    {
        var pullerNode = pullerIsA ? harness.NodeA : harness.NodeB;
        var pusherNode = pullerIsA ? harness.NodeB : harness.NodeA;
        var pusherEndpoint = pullerIsA ? harness.SharedSocketPathB : harness.SharedSocketPathA;

        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var pusherFinished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var inbound in pusherNode.Transport.ListenAsync(sessionCts.Token)
                    )
                {
                    await using (inbound)
                    {
                        // 1. Handshake as responder.
                        await HandshakeProtocol.RespondAsync(
                            inbound,
                            pusherNode.BuildLocalIdentity(),
                            policy: proposal => new AckMessage(
                                GrantedSubscriptions: proposal.ProposedStreams,
                                Rejected: Array.Empty<Rejection>()),
                            sessionCts.Token);

                        // 2. Read the puller's checkpoint (carried in a
                        //    DELTA_STREAM frame whose OpSequence = last-known
                        //    sequence and whose CrdtOps payload is empty).
                        var checkpoint = await inbound.ReceiveAsync(sessionCts.Token)
                            ;
                        ulong sinceSeq = 0;
                        if (checkpoint is DeltaStreamMessage chk && chk.StreamId == "__checkpoint__")
                        {
                            sinceSeq = chk.OpSequence;
                        }

                        // 3. Replay every entry after the checkpoint as a
                        //    DELTA_STREAM frame whose CrdtOps bytes encode
                        //    one KernelEvent (JSON for the test — the log is
                        //    opaque to the protocol).
                        await foreach (var entry in pusherNode.EventLog
                            .ReadAfterAsync(sinceSeq, sessionCts.Token))
                        {
                            var payload = EventEncoder.Encode(entry.Event);
                            var frame = new DeltaStreamMessage(
                                StreamId: "event-log",
                                OpSequence: entry.Sequence,
                                CrdtOps: payload);
                            await inbound.SendAsync(frame, sessionCts.Token);
                        }

                        // 4. End-of-stream marker: empty payload.
                        var eof = new DeltaStreamMessage(
                            StreamId: "__eof__",
                            OpSequence: 0,
                            CrdtOps: Array.Empty<byte>());
                        await inbound.SendAsync(eof, sessionCts.Token);
                    }
                    pusherFinished.TrySetResult(true);
                    break;
                }
            }
            catch (Exception ex)
            {
                pusherFinished.TrySetException(ex);
            }
        }, sessionCts.Token);

        // Puller side
        await using var conn = await pullerNode.Transport
            .ConnectAsync(pusherEndpoint, ct);

        await HandshakeProtocol.InitiateAsync(
            conn,
            pullerNode.BuildLocalIdentity(),
            ct);

        // Send checkpoint: ask for every event the pusher has. We dedupe
        // on the puller side via observed event ids — this keeps the
        // session harness sequence-agnostic so two nodes with overlapping
        // local sequence numbers still converge correctly.
        await conn.SendAsync(new DeltaStreamMessage(
            StreamId: "__checkpoint__",
            OpSequence: 0,
            CrdtOps: Array.Empty<byte>()), ct);

        // Snapshot the puller's current event ids so we skip duplicates.
        var seenIds = new HashSet<EventId>();
        await foreach (var existing in pullerNode.EventLog.ReadAfterAsync(0, ct))
        {
            seenIds.Add(existing.Event.Id);
        }

        // Drain DELTA_STREAM frames until EOF.
        while (true)
        {
            var inbound = await conn.ReceiveAsync(ct);
            if (inbound is not DeltaStreamMessage delta)
            {
                throw new InvalidOperationException(
                    $"Expected DELTA_STREAM, got {inbound.GetType().Name}.");
            }
            if (delta.StreamId == "__eof__")
            {
                break;
            }
            var decoded = EventEncoder.Decode(delta.CrdtOps);
            if (seenIds.Add(decoded.Id))
            {
                await pullerNode.EventLog.AppendAsync(decoded, ct);
            }
        }

        await pusherFinished.Task.WaitAsync(TestTimeout, ct);
        sessionCts.Cancel();
    }

    private static KernelEvent BuildEvent(string entityLocal, string kind)
    {
        return new KernelEvent(
            Id: EventId.NewId(),
            EntityId: new EntityId("test", "integration", entityLocal),
            Kind: kind,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: new Dictionary<string, object?>
            {
                ["v"] = 1,
            });
    }

    private static async Task<List<LogEntry>> CollectAsync(IAsyncEnumerable<LogEntry> src)
    {
        var list = new List<LogEntry>();
        await foreach (var e in src)
        {
            list.Add(e);
        }
        return list;
    }
}
