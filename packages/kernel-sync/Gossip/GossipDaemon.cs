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

    // Monotonic nonce counter. ulong.MaxValue wrap-around is not a
    // real-world concern (see class remarks); we do not guard against it.
    private ulong _pingNonce;

    private readonly ConcurrentDictionary<string, PeerState> _peers = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private CancellationTokenSource? _runCts;
    private Task? _runLoop;
    private bool _disposed;

    public event EventHandler<GossipRoundCompletedEventArgs>? RoundCompleted;

    public GossipDaemon(
        ISyncDaemonTransport transport,
        VectorClock vectorClock,
        IOptions<GossipDaemonOptions> options,
        INodeIdentityProvider nodeIdentity,
        IEd25519Signer signer,
        ILogger<GossipDaemon>? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _vectorClock = vectorClock ?? throw new ArgumentNullException(nameof(vectorClock));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _nodeIdentity = nodeIdentity ?? throw new ArgumentNullException(nameof(nodeIdentity));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GossipDaemon>.Instance;
        _rng = new Random();
        _rateLimiter = new DeltaStreamRateLimiter(_options.MaxDeltaStreamPerSecondPerPeer);
    }

    public IReadOnlyCollection<PeerInfo> KnownPeers =>
        _peers.Values.Select(p => p.Info).ToList();

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
        try
        {
            conn = await _transport.ConnectAsync(peer.Info.Endpoint, timeoutCts.Token).ConfigureAwait(false);

            var identity = _nodeIdentity.Current;

            // The handshake ladder: HELLO (both ways) → CAPABILITY_NEG → ACK.
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
                    }
                }
            }
            catch (OperationCanceledException)
            {
                /* reply timed out — still a successful send */
            }

            // Success → reset strike count.
            peer.OnRoundSucceeded();
            return (DeltasExchanged: 1, OpsReceived: 0);
        }
        catch (OperationCanceledException) when (!roundCt.IsCancellationRequested)
        {
            peer.OnRoundFailed(_options.DeadPeerBackoffSeconds);
            _logger.LogDebug("Gossip round timed out for {Endpoint}", peer.Info.Endpoint);
            return (0, 0);
        }
        catch (Exception ex)
        {
            peer.OnRoundFailed(_options.DeadPeerBackoffSeconds);
            _logger.LogDebug(ex, "Gossip round failed for {Endpoint}", peer.Info.Endpoint);
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
