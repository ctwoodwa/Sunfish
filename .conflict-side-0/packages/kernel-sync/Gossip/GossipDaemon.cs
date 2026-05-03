using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Sync.Handshake;
using Sunfish.Kernel.Sync.Identity;
using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.Kernel.Sync.Gossip;

/// <summary>
/// Paper §6.1 gossip-based anti-entropy daemon. See <see cref="IGossipDaemon"/>
/// for the contract.
/// </summary>
/// <remarks>
/// <para>
/// <b>Concurrency model:</b> the daemon drives rounds with a
/// <see cref="PeriodicTimer"/> rather than wrapping a
/// <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>. The reason is
/// that this package is consumed both from Hosting-backed apps (where a
/// BackgroundService would be natural) and from lightweight CLI / test
/// harnesses where pulling in Hosting just to get a background loop is
/// overkill. PeriodicTimer gives us clean cancellation, deterministic ticks,
/// and zero Hosting dependency.
/// </para>
/// <para>
/// <b>Dead-peer backoff.</b> A peer that times out or errors during a round
/// gets a "strike"; the next round skips it for an exponentially-growing
/// window (doubles per strike, capped at 4× the configured base). A successful
/// round clears both the strike count and the skip deadline. Rebound is
/// immediate — a reconnected peer does not "slow-start" back into the
/// rotation.
/// </para>
/// <para>
/// <b>Replay protection (spec §8).</b> Every outbound GOSSIP_PING carries a
/// strictly-increasing <c>monotonic_nonce</c> maintained via
/// <see cref="Interlocked.Increment(ref ulong)"/>. Every inbound PING is
/// checked against the per-peer <see cref="PeerInfo.LastSeenNonce"/>; a
/// non-monotonic nonce is dropped (spec "not fatal — recoverable"). The
/// wraparound at <c>ulong.MaxValue</c> is not a real-world concern: at
/// 1000 PINGs/sec/peer (≫ the 30 s tick), the counter would take ~5×10^17
/// seconds — roughly 99 billion years — to wrap.
/// </para>
/// <para>
/// <b>What this daemon ships in Wave 2.5.</b> The round loop completes the
/// signed HELLO handshake against every picked peer, exchanges a
/// signed-payload GOSSIP_PING carrying the current vector-clock snapshot
/// plus the next monotonic nonce, and rate-limits inbound DELTA_STREAM at
/// <see cref="GossipDaemonOptions.MaxDeltaStreamPerSecondPerPeer"/>. CRDT-op
/// apply-back into <c>ICrdtDocument</c> still lands in Wave 2.6 alongside
/// the application-layer integration.
/// </para>
/// </remarks>
public sealed class GossipDaemon : IGossipDaemon
{
    private readonly ISyncDaemonTransport _transport;
    private readonly VectorClock _vectorClock;
    private readonly GossipDaemonOptions _options;
    private readonly ILogger<GossipDaemon> _logger;
    private readonly Random _rng;
    private readonly INodeIdentityProvider _nodeIdentity;
    private readonly IEd25519Signer _signer;
    private readonly DeltaStreamRateLimiter _rateLimiter;
    private readonly Application.IDeltaProducer _deltaProducer;
    private readonly Application.IDeltaSink _deltaSink;
    private ulong _outboundDeltaSeq;

    // Monotonic nonce counter. ulong.MaxValue wrap-around is not a
    // real-world concern (see class remarks); we do not guard against it.
    private ulong _pingNonce;

    private readonly ConcurrentDictionary<string, PeerState> _peers = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private CancellationTokenSource? _runCts;
    private Task? _runLoop;
    private bool _disposed;

    public event EventHandler<GossipRoundCompletedEventArgs>? RoundCompleted;

    /// <inheritdoc />
    public event EventHandler<GossipFrameEventArgs>? FrameReceived;

    public GossipDaemon(
        ISyncDaemonTransport transport,
        VectorClock vectorClock,
        IOptions<GossipDaemonOptions> options,
        INodeIdentityProvider nodeIdentity,
        IEd25519Signer signer,
        Application.IDeltaProducer? deltaProducer = null,
        Application.IDeltaSink? deltaSink = null,
        ILogger<GossipDaemon>? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _vectorClock = vectorClock ?? throw new ArgumentNullException(nameof(vectorClock));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _nodeIdentity = nodeIdentity ?? throw new ArgumentNullException(nameof(nodeIdentity));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        // Wave 2.5 — null defaults preserve PING-only behavior for callers
        // that haven't migrated to the Application namespace yet (the older
        // 5-arg constructor signature).
        _deltaProducer = deltaProducer ?? new Application.NoopDeltaProducer();
        _deltaSink = deltaSink ?? new Application.NoopDeltaSink();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GossipDaemon>.Instance;
        _rng = new Random();
        _rateLimiter = new DeltaStreamRateLimiter(_options.MaxDeltaStreamPerSecondPerPeer);
    }

    public IReadOnlyCollection<PeerInfo> KnownPeers =>
        _peers.Values.Select(p => p.Info).ToList();

    /// <inheritdoc />
    /// <remarks>
    /// Reads the private <c>_runLoop</c> reference under no lock — the field is
    /// a plain managed reference and the read is a single aligned pointer load,
    /// so the worst-case observable state is "just-transitioned"; that is
    /// acceptable for the health-check consumer, which treats this as an
    /// advisory signal.
    /// </remarks>
    public bool IsRunning => _runLoop is not null;

    /// <summary>
    /// Exposed for tests and observability. Per-peer DELTA_STREAM budget
    /// enforced against inbound frames in this daemon's receive path.
    /// </summary>
    public DeltaStreamRateLimiter RateLimiter => _rateLimiter;

    public void AddPeer(string peerEndpoint, byte[] peerPublicKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(peerEndpoint);
        ArgumentNullException.ThrowIfNull(peerPublicKey);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _peers[peerEndpoint] = new PeerState(
            new PeerInfo(peerEndpoint, peerPublicKey, DateTimeOffset.MinValue, 0, LastSeenNonce: 0));
    }

    public void RemovePeer(string peerEndpoint)
    {
        ArgumentException.ThrowIfNullOrEmpty(peerEndpoint);
        _peers.TryRemove(peerEndpoint, out _);
    }

    /// <summary>
    /// Validate and record a GOSSIP_PING received from a peer. Returns
    /// <c>true</c> if the nonce is strictly greater than the peer's
    /// <see cref="PeerInfo.LastSeenNonce"/> and was advanced; <c>false</c>
    /// if it was a replay (stale or equal) and the caller should drop it.
    /// </summary>
    /// <remarks>
    /// Exposed as a public entrypoint so receive loops outside the
    /// round-loop (e.g. a dedicated listener) can share the same replay
    /// check. An unknown peer returns <c>false</c> — only membership
    /// advertised via <see cref="AddPeer"/> is subject to replay tracking.
    /// </remarks>
    public bool TryAdvancePeerNonce(string peerEndpoint, ulong incomingNonce)
    {
        if (!_peers.TryGetValue(peerEndpoint, out var peer))
        {
            return false;
        }
        return peer.TryAdvanceNonce(incomingNonce);
    }

    /// <summary>
    /// Rate-limit check for inbound DELTA_STREAM from <paramref name="peerEndpoint"/>.
    /// Returns <c>true</c> if the frame may be processed; <c>false</c> if
    /// the peer exceeded its per-second budget and the frame must be
    /// dropped. Convenience wrapper over <see cref="RateLimiter"/>.
    /// </summary>
    public bool AllowInboundDelta(string peerEndpoint)
    {
        var allowed = _rateLimiter.TryConsume(peerEndpoint);
        if (!allowed)
        {
            _logger.LogWarning(
                "DELTA_STREAM from {Peer} dropped: exceeds {Budget}/s rate limit.",
                peerEndpoint, _options.MaxDeltaStreamPerSecondPerPeer);
        }
        return allowed;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_runLoop is not null) return; // idempotent start
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _runLoop = Task.Run(() => RunLoopAsync(_runCts.Token));
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_runLoop is null) return;
            _runCts?.Cancel();
            try
            {
                await _runLoop.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                /* expected during shutdown */
            }
            _runLoop = null;
            _runCts?.Dispose();
            _runCts = null;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            /* swallow — DisposeAsync should not throw */
        }
        _lifecycleLock.Dispose();
    }

    // ------------------------------------------------------------------
    // Round loop
    // ------------------------------------------------------------------

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var period = TimeSpan.FromSeconds(Math.Max(1, _options.RoundIntervalSeconds));
        using var timer = new PeriodicTimer(period);

        // Run one round immediately so tests with RoundIntervalSeconds: 1 do
        // not have to wait for the first tick.
        await RunOneRoundAsync(ct).ConfigureAwait(false);

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await RunOneRoundAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            /* expected during shutdown */
        }
    }

    private async Task RunOneRoundAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var eligible = _peers.Values
            .Where(p => p.SkipUntil <= now)
            .ToList();

        if (eligible.Count == 0)
        {
            // Still emit an empty round event so tests observing RoundCompleted
            // can unblock even when no peers are configured.
            RoundCompleted?.Invoke(this, new GossipRoundCompletedEventArgs(0, 0, 0));
            return;
        }

        var picks = PickRandom(eligible, _options.PeerPickCount);
        var deltasExchanged = 0;
        var opsReceived = 0;

        foreach (var peer in picks)
        {
            if (ct.IsCancellationRequested) break;
            var outcome = await ExchangeWithPeerAsync(peer, ct).ConfigureAwait(false);
            deltasExchanged += outcome.DeltasExchanged;
            opsReceived += outcome.OpsReceived;
        }

        RoundCompleted?.Invoke(this, new GossipRoundCompletedEventArgs(
            picks.Count, deltasExchanged, opsReceived));
    }

    private List<PeerState> PickRandom(List<PeerState> pool, int count)
    {
        if (count >= pool.Count) return pool.ToList();

        // Fisher–Yates prefix shuffle over the index array — O(count) work.
        var indices = Enumerable.Range(0, pool.Count).ToArray();
        for (var i = 0; i < count; i++)
        {
            var swap = _rng.Next(i, pool.Count);
            (indices[i], indices[swap]) = (indices[swap], indices[i]);
        }
        return indices.Take(count).Select(i => pool[i]).ToList();
    }

    private async Task<(int DeltasExchanged, int OpsReceived)> ExchangeWithPeerAsync(
        PeerState peer,
        CancellationToken roundCt)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(roundCt);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));

        ISyncDaemonConnection? conn = null;
        var peerNodeId = Convert.ToHexString(peer.Info.PublicKey.AsSpan(
            0, Math.Min(16, peer.Info.PublicKey.Length))).ToLowerInvariant();
        try
        {
            conn = await _transport.ConnectAsync(peer.Info.Endpoint, timeoutCts.Token).ConfigureAwait(false);

            var identity = _nodeIdentity.Current;

            // The handshake ladder: HELLO (both ways) → CAPABILITY_NEG → ACK.
            try
            {
                await HandshakeProtocol.InitiateAsync(
                    conn,
                    localIdentity: new LocalIdentity(
                        NodeId: identity.NodeIdBytes,
                        PublicKey: identity.PublicKey,
                        Signer: _signer,
                        PrivateKey: identity.PrivateKey,
                        SchemaVersion: HandshakeProtocol.DefaultSchemaVersion,
                        SupportedVersions: HandshakeProtocol.DefaultSupportedVersions),
                    timeoutCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Signed-HELLO failed (bad signature, incompatible schema,
                // transport dropped mid-handshake). Surface as an observable
                // HandshakeFailure frame before the outer catch reclassifies
                // it as a dead-peer round failure — the outer catch re-raises
                // per-peer backoff, but the notification layer still wants to
                // know the handshake itself failed rather than a generic
                // connect error.
                FrameReceived?.Invoke(this, new GossipFrameEventArgs(
                    PeerEndpoint: peer.Info.Endpoint,
                    PeerNodeId: peerNodeId,
                    FrameType: GossipFrameType.HandshakeFailure,
                    OccurredAt: DateTimeOffset.UtcNow,
                    Summary: $"handshake with {peerNodeId} failed: {ex.GetType().Name}"));
                throw;
            }

            // Successful HELLO → Hello frame event.
            FrameReceived?.Invoke(this, new GossipFrameEventArgs(
                PeerEndpoint: peer.Info.Endpoint,
                PeerNodeId: peerNodeId,
                FrameType: GossipFrameType.Hello,
                OccurredAt: DateTimeOffset.UtcNow,
                Summary: $"{peerNodeId} completed handshake"));

            // Exchange a GOSSIP_PING carrying our vector-clock snapshot and
            // the next monotonic nonce (spec §8). We accept at most one
            // inbound ping per round to bound the loop.
            var nonce = Interlocked.Increment(ref _pingNonce);
            var ping = new GossipPingMessage(
                VectorClock: _vectorClock.Snapshot(),
                PeerMembershipDelta: new MembershipDelta(
                    Added: Array.Empty<byte[]>(),
                    Removed: Array.Empty<byte[]>()),
                MonotonicNonce: nonce);
            await conn.SendAsync(ping, timeoutCts.Token).ConfigureAwait(false);

            // Wave 2.5 — outbound DELTA_STREAM. Always send a frame so the
            // wire protocol stays predictable: every round is PING then
            // DELTA_STREAM, regardless of whether the producer has anything
            // to ship. An empty CrdtOps payload is a no-op on the receiver
            // (the sink interprets empty as "nothing to apply"). This mirrors
            // the always-PING convention and keeps responder code simple.
            //
            // Phase 1 single-document callers pass an empty peer-vector-clock
            // (the gossip-level VC isn't the CRDT-level VC; richer-VC plumbing
            // is a follow-up wave). The producer interprets empty as "encode
            // full history."
            const string phase1DocumentId = "default";
            var outboundDelta = await _deltaProducer.EncodeOutboundDeltaAsync(
                phase1DocumentId,
                ReadOnlyMemory<byte>.Empty,
                timeoutCts.Token).ConfigureAwait(false);
            var outboundSeq = Interlocked.Increment(ref _outboundDeltaSeq);
            var deltaOut = new DeltaStreamMessage(
                StreamId: phase1DocumentId,
                OpSequence: outboundSeq,
                CrdtOps: outboundDelta?.ToArray() ?? Array.Empty<byte>());
            await conn.SendAsync(deltaOut, timeoutCts.Token).ConfigureAwait(false);
            var opsReceivedThisRound = 0;

            // Receive-with-timeout — if the peer does not reply within the
            // round's timeout, we count the round as "half-exchanged" and
            // move on without marking the peer dead (they accepted our data).
            using var recvCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
            try
            {
                var inbound = await conn.ReceiveAsync(recvCts.Token).ConfigureAwait(false);
                if (inbound is GossipPingMessage inboundPing)
                {
                    // Replay-check the inbound nonce before merging state.
                    if (!peer.TryAdvanceNonce(inboundPing.MonotonicNonce))
                    {
                        _logger.LogWarning(
                            "Dropped replayed GOSSIP_PING from {Peer}: nonce {Nonce} <= last seen {Last}.",
                            peer.Info.Endpoint, inboundPing.MonotonicNonce, peer.Info.LastSeenNonce);
                    }
                    else
                    {
                        // Merge the peer's vector clock so we converge on the
                        // newer-op frontier for the next round.
                        var peerClock = new VectorClock(inboundPing.VectorClock);
                        _vectorClock.Merge(peerClock);

                        // Successful inbound PING → GossipPing frame event.
                        FrameReceived?.Invoke(this, new GossipFrameEventArgs(
                            PeerEndpoint: peer.Info.Endpoint,
                            PeerNodeId: peerNodeId,
                            FrameType: GossipFrameType.GossipPing,
                            OccurredAt: DateTimeOffset.UtcNow,
                            Summary: $"{peerNodeId} sent gossip ping"));

                        // Wave 2.5 — after the PING comes an optional DELTA_STREAM
                        // from the peer. We attempt a second receive with the
                        // remaining round timeout; if the peer didn't send a
                        // delta this round the receive cancels and we move on.
                        opsReceivedThisRound = await TryReceiveDeltaStreamAsync(
                            conn, peer, peerNodeId, recvCts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                /* reply timed out — still a successful send */
            }

            // Success → reset strike count.
            peer.OnRoundSucceeded();
            return (DeltasExchanged: 1, OpsReceived: opsReceivedThisRound);
        }
        catch (OperationCanceledException) when (!roundCt.IsCancellationRequested)
        {
            peer.OnRoundFailed(_options.DeadPeerBackoffSeconds);
            _logger.LogDebug("Gossip round timed out for {Endpoint}", peer.Info.Endpoint);
            FrameReceived?.Invoke(this, new GossipFrameEventArgs(
                PeerEndpoint: peer.Info.Endpoint,
                PeerNodeId: peerNodeId,
                FrameType: GossipFrameType.GossipError,
                OccurredAt: DateTimeOffset.UtcNow,
                Summary: $"gossip round timed out for {peer.Info.Endpoint}"));
            return (0, 0);
        }
        catch (Exception ex)
        {
            peer.OnRoundFailed(_options.DeadPeerBackoffSeconds);
            _logger.LogDebug(ex, "Gossip round failed for {Endpoint}", peer.Info.Endpoint);
            // The HandshakeFailure path already raised its own FrameReceived
            // before rethrowing; we still emit a GossipError here so consumers
            // that only track GossipError see every round failure. If the
            // double-emit becomes noisy a future enhancement can gate this
            // on a "did we already raise" flag — today the notification
            // aggregator de-dupes by TeamNotification.Id (random guid), so a
            // double event is two benign notifications, not a state bug.
            FrameReceived?.Invoke(this, new GossipFrameEventArgs(
                PeerEndpoint: peer.Info.Endpoint,
                PeerNodeId: peerNodeId,
                FrameType: GossipFrameType.GossipError,
                OccurredAt: DateTimeOffset.UtcNow,
                Summary: $"gossip round failed for {peer.Info.Endpoint}: {ex.GetType().Name}"));
            return (0, 0);
        }
        finally
        {
            if (conn is not null)
            {
                try { await conn.DisposeAsync().ConfigureAwait(false); }
                catch { /* best-effort close */ }
            }
        }
    }

    /// <summary>
    /// Wave 2.5 — attempt to receive an inbound <c>DELTA_STREAM</c> after the
    /// PING exchange. Best-effort: returns 0 on timeout, malformed frame, or
    /// rate-limit rejection so the round continues to count as successful.
    /// </summary>
    private async Task<int> TryReceiveDeltaStreamAsync(
        ISyncDaemonConnection conn,
        PeerState peer,
        string peerNodeId,
        CancellationToken ct)
    {
        try
        {
            var inbound = await conn.ReceiveAsync(ct).ConfigureAwait(false);
            if (inbound is not DeltaStreamMessage delta)
            {
                return 0; // peer sent something else (or nothing) — ignore
            }

            // Empty payload = peer had nothing to ship this round. The frame
            // is sent unconditionally (see outbound section above) to keep
            // the wire protocol predictable, but there's nothing for the sink
            // to apply.
            if (delta.CrdtOps is null || delta.CrdtOps.Length == 0)
            {
                return 0;
            }

            // Spec §8 rate-limit check before dispatching to the sink.
            if (!AllowInboundDelta(peer.Info.Endpoint))
            {
                return 0;
            }

            await _deltaSink.ApplyInboundDeltaAsync(
                delta.StreamId,
                delta.OpSequence,
                delta.CrdtOps,
                ct).ConfigureAwait(false);

            FrameReceived?.Invoke(this, new GossipFrameEventArgs(
                PeerEndpoint: peer.Info.Endpoint,
                PeerNodeId: peerNodeId,
                FrameType: GossipFrameType.DeltaStream,
                OccurredAt: DateTimeOffset.UtcNow,
                Summary: $"{peerNodeId} sent delta-stream {delta.StreamId} op {delta.OpSequence}"));

            // Reporting "1 op received" is a coarse stand-in for the actual
            // CRDT op count — the wire payload is opaque bytes whose internal
            // op-count semantics belong to the engine. Adequate for the
            // existing GossipRoundCompletedEventArgs.OpsReceived signal until
            // the engine surfaces a real op-count from ApplyDelta.
            return 1;
        }
        catch (OperationCanceledException)
        {
            return 0; // peer didn't send a delta this round — fine
        }
        catch (Exception ex)
        {
            // Per IDeltaSink contract: malformed payloads are recoverable.
            // Log and drop the frame rather than aborting the round (which
            // would mark the peer dead and over-penalize a single bad frame).
            _logger.LogWarning(ex,
                "Inbound DELTA_STREAM from {Peer} rejected: {Reason}",
                peer.Info.Endpoint, ex.Message);
            return 0;
        }
    }

    // ------------------------------------------------------------------
    // Per-peer state — owns backoff bookkeeping + last-seen nonce
    // ------------------------------------------------------------------

    private sealed class PeerState
    {
        private readonly object _nonceLock = new();
        private int _strikes;

        public PeerInfo Info { get; private set; }
        public DateTimeOffset SkipUntil { get; private set; } = DateTimeOffset.MinValue;

        public PeerState(PeerInfo info)
        {
            Info = info;
        }

        public void OnRoundSucceeded()
        {
            _strikes = 0;
            SkipUntil = DateTimeOffset.MinValue;
            Info = Info with { LastSeenAt = DateTimeOffset.UtcNow };
        }

        public void OnRoundFailed(int baseBackoffSeconds)
        {
            _strikes = Math.Min(_strikes + 1, 3); // cap doubling at 2^3 = 8× ... then we clamp below.
            var multiplier = Math.Min(1 << (_strikes - 1), 4); // 1,2,4,4…
            SkipUntil = DateTimeOffset.UtcNow.AddSeconds(baseBackoffSeconds * multiplier);
        }

        /// <summary>
        /// Replay-protection gate: accept <paramref name="incomingNonce"/>
        /// only if it is strictly greater than the previously seen nonce.
        /// </summary>
        public bool TryAdvanceNonce(ulong incomingNonce)
        {
            lock (_nonceLock)
            {
                if (incomingNonce <= Info.LastSeenNonce)
                {
                    return false;
                }
                Info = Info with { LastSeenNonce = incomingNonce };
                return true;
            }
        }
    }
}
