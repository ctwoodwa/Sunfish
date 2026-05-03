using System.Collections.Concurrent;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sunfish.Kernel.Sync.Gossip;
using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.Kernel.Lease;

/// <summary>
/// Flease-inspired distributed lease coordinator. Implements paper §6.3 and
/// sync-daemon-protocol §6 over the shared <see cref="ISyncDaemonTransport"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Protocol sketch.</b>
/// </para>
/// <list type="bullet">
///   <item><description>A node wishing to acquire a lease generates a new
///   16-byte lease id and a monotonically-increasing proposal number, then
///   opens one transport connection per known peer and sends
///   <c>LEASE_REQUEST</c>.</description></item>
///   <item><description>Each responder consults its local conflict cache.
///   If no unexpired lease exists on the same resource it replies
///   <c>LEASE_GRANT</c>; otherwise <c>LEASE_DENIED { reason: "CONFLICT" }</c>
///   (or <c>"QUORUM_UNAVAILABLE"</c> when it too cannot confirm quorum, per
///   spec §6).</description></item>
///   <item><description>The proposer counts grants. Once
///   <c>ceil(N/2)+1</c> grants land — the local node's own "self-grant"
///   included — the lease is acquired. Timeout or majority-denied returns
///   <c>null</c>.</description></item>
///   <item><description>Explicit release broadcasts <c>LEASE_RELEASE</c> to
///   every peer that participated in the grant.</description></item>
/// </list>
/// <para>
/// <b>Responder loop.</b> The coordinator listens on its own transport (if
/// the transport was configured with a listen endpoint) and replies to
/// incoming <c>LEASE_REQUEST</c> frames from its own in-process grant
/// cache. The listener runs under a background task whose lifetime is
/// bounded by <see cref="DisposeAsync"/>.
/// </para>
/// <para>
/// <b>Concurrency.</b> The coordinator is safe to call from multiple
/// threads concurrently. Per-resource acquires race through a single
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> whose
/// <c>TryAdd</c> decides the winner; other proposers observe the winning
/// lease and back off (deny on conflict).
/// </para>
/// </remarks>
public sealed class FleaseLeaseCoordinator : ILeaseCoordinator
{
    // Wire string for LEASE_DENIED reasons. Kept close to the Flease
    // algorithm so the spec §6 mapping is obvious from one file.
    internal const string DenyReasonConflict = "CONFLICT";
    internal const string DenyReasonQuorumUnavailable = "QUORUM_UNAVAILABLE";
    internal const string DenyReasonQuotaExceeded = "QUOTA_EXCEEDED";

    private readonly ISyncDaemonTransport _transport;
    private readonly IGossipDaemon _gossip;
    private readonly LeaseCoordinatorOptions _options;
    private readonly ILogger<FleaseLeaseCoordinator> _logger;
    private readonly string _localNodeId;
    private readonly string? _localListenEndpoint;

    // Our own view of leases we currently hold, keyed by resource id.
    private readonly ConcurrentDictionary<string, Lease> _heldLeases = new(StringComparer.Ordinal);

    // Responder-side cache: leases granted (or held by us) keyed by
    // resource id. This is what incoming LEASE_REQUEST frames check for
    // conflicts.
    private readonly ConcurrentDictionary<string, GrantRecord> _conflictCache = new(StringComparer.Ordinal);

    // Serialize concurrent Acquire calls on the same resource — the
    // responder-side dictionary is not enough because we also need the
    // *proposer* side to coalesce duplicate proposals from the same node.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _acquireLocks = new(StringComparer.Ordinal);

    // Per-lease set of every peer endpoint we *sent* a LEASE_REQUEST to
    // during the acquire round. This is a strict superset of
    // <see cref="Lease.QuorumParticipants"/>: when AcquireInternalAsync
    // short-circuits at quorum, peers that responded after the cutoff (or
    // had their request cancelled mid-flight) may still have cached our
    // grant in their conflict caches, so the release broadcast must reach
    // them too — otherwise their stale entry blocks every other node from
    // re-acquiring the resource until the natural lease expiry. Keyed by
    // lease id (not resource id) so concurrent re-acquires after release
    // do not cross-contaminate.
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _proposalTargets = new(StringComparer.Ordinal);

    // Monotonic proposal counter. Used to tag our own LEASE_REQUESTs so
    // retries/out-of-order responses can be correlated. Not strictly
    // required for the happy-path Flease grant but cheap to carry and
    // cited in the xmldoc as the "proposal-number scheme".
    private long _proposalCounter;

    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly Task? _responderLoop;
    private readonly Task _prunerLoop;
    private bool _disposed;

    /// <summary>
    /// Construct a coordinator for a single local node.
    /// </summary>
    /// <param name="transport">Transport used for both outbound proposals
    /// and the inbound responder loop. Callers who want a responder should
    /// pass a transport that was created with a listen endpoint.</param>
    /// <param name="gossip">Source of truth for peer membership.</param>
    /// <param name="options">Tunables (see <see cref="LeaseCoordinatorOptions"/>).</param>
    /// <param name="localNodeId">Stable identifier of this node. Shows up
    /// in <see cref="Lease.HolderNodeId"/>.</param>
    /// <param name="localListenEndpoint">Optional: the transport endpoint
    /// this node listens on. When supplied, the coordinator starts a
    /// responder loop that accepts incoming <c>LEASE_REQUEST</c> /
    /// <c>LEASE_RELEASE</c> frames.</param>
    /// <param name="logger">Optional logger.</param>
    public FleaseLeaseCoordinator(
        ISyncDaemonTransport transport,
        IGossipDaemon gossip,
        IOptions<LeaseCoordinatorOptions> options,
        string localNodeId,
        string? localListenEndpoint = null,
        ILogger<FleaseLeaseCoordinator>? logger = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _gossip = gossip ?? throw new ArgumentNullException(nameof(gossip));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FleaseLeaseCoordinator>.Instance;
        _localNodeId = localNodeId ?? throw new ArgumentNullException(nameof(localNodeId));
        _localListenEndpoint = localListenEndpoint;

        _prunerLoop = Task.Run(() => PruneLoopAsync(_lifetimeCts.Token));

        if (!string.IsNullOrEmpty(localListenEndpoint))
        {
            _responderLoop = Task.Run(() => ResponderLoopAsync(_lifetimeCts.Token));
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Lease> HeldLeases => _heldLeases.Values.ToList();

    /// <inheritdoc />
    public bool Holds(string resourceId)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceId);
        if (!_heldLeases.TryGetValue(resourceId, out var lease))
        {
            return false;
        }
        if (lease.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            // Expired — drop from the view now so the next Holds call is
            // cheap and the HeldLeases snapshot stays honest.
            _heldLeases.TryRemove(resourceId, out _);
            _conflictCache.TryRemove(resourceId, out _);
            return false;
        }
        return true;
    }

    /// <inheritdoc />
    public async Task<Lease?> AcquireAsync(string resourceId, TimeSpan duration, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceId);
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Lease duration must be positive.");
        }
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Coalesce concurrent Acquire calls on the same resource inside
        // this process — only one proposal at a time. Remote proposers
        // racing us are handled by the responder-side conflict cache.
        var perResourceLock = _acquireLocks.GetOrAdd(resourceId, _ => new SemaphoreSlim(1, 1));
        await perResourceLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Fast path: we already hold an unexpired lease on this
            // resource; hand the existing one back rather than re-proposing.
            if (_heldLeases.TryGetValue(resourceId, out var existing)
                && existing.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return existing;
            }

            return await AcquireInternalAsync(resourceId, duration, ct).ConfigureAwait(false);
        }
        finally
        {
            perResourceLock.Release();
        }
    }

    private async Task<Lease?> AcquireInternalAsync(
        string resourceId,
        TimeSpan duration,
        CancellationToken ct)
    {
        // Snapshot peer membership at proposal time. The gossip daemon
        // adds/removes peers concurrently so we freeze the list for the
        // whole proposal round.
        var peers = _gossip.KnownPeers.Select(p => p.Endpoint).ToList();
        var quorum = EffectiveQuorum(peers.Count);

        var leaseId = GenerateLeaseId();
        var proposalNumber = Interlocked.Increment(ref _proposalCounter);
        _logger.LogDebug(
            "Proposal #{Proposal} leaseId={LeaseId} resource={Resource} peers={PeerCount} quorum={Quorum}",
            proposalNumber, leaseId, resourceId, peers.Count, quorum);

        // The local node always votes for its own proposal provided we
        // don't have a conflicting unexpired lease already recorded. This
        // is the "self-grant" that makes single-node clusters work.
        var localSelfGrant = TryLocalSelfGrant(resourceId, leaseId);
        if (!localSelfGrant)
        {
            // We already have an active grant for this resource that is
            // not ours — deny ourselves and let the caller see the
            // quorum-unavailable path rather than issue a double grant.
            return null;
        }

        var grantors = new List<string> { _localNodeId };
        var deniedHeldBy = (string?)null;
        var denials = 0;

        if (peers.Count > 0)
        {
            using var proposalCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            proposalCts.CancelAfter(_options.ProposalTimeout);

            var perPeerTasks = peers
                .Select(endpoint => ProposeToPeerAsync(endpoint, leaseId, resourceId, duration, proposalCts.Token))
                .ToList();

            try
            {
                // Await as they complete so we can short-circuit once the
                // quorum is reached.
                while (perPeerTasks.Count > 0)
                {
                    var done = await Task.WhenAny(perPeerTasks).ConfigureAwait(false);
                    perPeerTasks.Remove(done);

                    PeerProposalOutcome outcome;
                    try
                    {
                        outcome = await done.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Distinguish user-driven cancellation (re-throw)
                        // from proposal-timeout expiry (fall through to
                        // the quorum check and return null).
                        if (ct.IsCancellationRequested)
                        {
                            _conflictCache.TryRemove(resourceId, out _);
                            throw;
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Peer proposal errored; treating as timeout.");
                        continue;
                    }

                    if (outcome.Granted)
                    {
                        grantors.Add(outcome.PeerEndpoint);
                    }
                    else
                    {
                        denials++;
                        deniedHeldBy ??= outcome.HeldBy;
                    }

                    if (grantors.Count >= quorum)
                    {
                        break; // quorum reached — stop collecting
                    }
                }
            }
            finally
            {
                // Ensure stragglers are cancelled so we do not leak tasks.
                // They will complete (with OCE) on proposalCts.Cancel().
                proposalCts.Cancel();
            }
        }

        if (grantors.Count >= quorum)
        {
            var now = DateTimeOffset.UtcNow;
            var lease = new Lease(
                LeaseId: leaseId,
                ResourceId: resourceId,
                HolderNodeId: _localNodeId,
                AcquiredAt: now,
                ExpiresAt: now + duration,
                QuorumParticipants: grantors);
            _heldLeases[resourceId] = lease;
            _conflictCache[resourceId] = new GrantRecord(leaseId, _localNodeId, lease.ExpiresAt);
            // Remember every peer we asked, not just those whose grant
            // contributed to quorum. ReleaseAsync uses this to clear
            // stale conflict-cache entries on peers whose grant arrived
            // after the quorum short-circuit (see _proposalTargets doc).
            if (peers.Count > 0)
            {
                _proposalTargets[leaseId] = peers;
            }
            _logger.LogInformation(
                "Lease acquired: resource={Resource} leaseId={LeaseId} grantors={Grantors}",
                resourceId, leaseId, grantors.Count);
            return lease;
        }

        // Quorum not reached — roll back our self-grant so the next
        // Acquire can try cleanly, and surface the denial info so the
        // exception-flavoured wrappers have the right data.
        _conflictCache.TryRemove(resourceId, out _);
        _logger.LogDebug(
            "Lease denied: resource={Resource} grantors={Grantors}/{Quorum} denials={Denials}",
            resourceId, grantors.Count, quorum, denials);
        return null;
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Lease lease, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Idempotent: nothing to do if we no longer think we hold it.
        if (!_heldLeases.TryRemove(lease.ResourceId, out var held) || held.LeaseId != lease.LeaseId)
        {
            // Still clear any stale conflict cache entry whose lease id
            // matches — this covers the "double-release" case.
            if (_conflictCache.TryGetValue(lease.ResourceId, out var rec) && rec.LeaseId == lease.LeaseId)
            {
                _conflictCache.TryRemove(lease.ResourceId, out _);
            }
            return;
        }

        _conflictCache.TryRemove(lease.ResourceId, out _);

        // Broadcast LEASE_RELEASE to every peer we *asked* during the
        // acquire round, not just those whose grant ended up in
        // QuorumParticipants. AcquireInternalAsync short-circuits when
        // quorum is reached, so a peer that granted slightly too late
        // (or whose proposal task was cancelled mid-flight) still has
        // our lease cached on its responder side and must see the
        // release — otherwise no other node can re-acquire the resource
        // until the natural expiry. See <see cref="_proposalTargets"/>.
        var releaseMessage = new LeaseReleaseMessage(
            LeaseId: DecodeLeaseIdHex(lease.LeaseId),
            ReleasedAt: (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        _proposalTargets.TryRemove(lease.LeaseId, out var fullProposalSet);
        var broadcastTargets = (fullProposalSet ?? lease.QuorumParticipants)
            .Concat(lease.QuorumParticipants)
            .Where(nodeId => !string.Equals(nodeId, _localNodeId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var broadcastTasks = broadcastTargets
            .Select(nodeId => BroadcastReleaseAsync(nodeId, releaseMessage, ct))
            .ToList();
        if (broadcastTasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(broadcastTasks).ConfigureAwait(false);
            }
            catch
            {
                /* best-effort release; lease expires on timer regardless */
            }
        }
    }

    // ------------------------------------------------------------------
    // Proposer helpers
    // ------------------------------------------------------------------

    private readonly record struct PeerProposalOutcome(string PeerEndpoint, bool Granted, string? HeldBy);

    private async Task<PeerProposalOutcome> ProposeToPeerAsync(
        string peerEndpoint,
        string leaseId,
        string resourceId,
        TimeSpan duration,
        CancellationToken ct)
    {
        ISyncDaemonConnection? conn = null;
        try
        {
            conn = await _transport.ConnectAsync(peerEndpoint, ct).ConfigureAwait(false);

            var request = new LeaseRequestMessage(
                LeaseId: DecodeLeaseIdHex(leaseId),
                ResourceId: resourceId,
                LeaseClass: "cp-default",
                RequestedDurationSeconds: (uint)Math.Max(1, (int)duration.TotalSeconds),
                RequesterNodeId: System.Text.Encoding.UTF8.GetBytes(_localNodeId));
            await conn.SendAsync(request, ct).ConfigureAwait(false);

            var reply = await conn.ReceiveAsync(ct).ConfigureAwait(false);
            return reply switch
            {
                LeaseGrantMessage => new PeerProposalOutcome(peerEndpoint, Granted: true, HeldBy: null),
                LeaseDeniedMessage denied => new PeerProposalOutcome(
                    peerEndpoint,
                    Granted: false,
                    HeldBy: denied.HeldBy is null ? null : System.Text.Encoding.UTF8.GetString(denied.HeldBy)),
                _ => new PeerProposalOutcome(peerEndpoint, Granted: false, HeldBy: null),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Proposal to {Peer} failed; counts as no-vote.", peerEndpoint);
            return new PeerProposalOutcome(peerEndpoint, Granted: false, HeldBy: null);
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

    private async Task BroadcastReleaseAsync(
        string peerEndpoint,
        LeaseReleaseMessage message,
        CancellationToken ct)
    {
        ISyncDaemonConnection? conn = null;
        try
        {
            conn = await _transport.ConnectAsync(peerEndpoint, ct).ConfigureAwait(false);
            await conn.SendAsync(message, ct).ConfigureAwait(false);

            // Wait for the peer to *finish* applying HandleLeaseRelease
            // before we return. The responder loop closes its half of the
            // connection only after HandleLeaseRelease has cleared the
            // entry from its conflict cache, so a follow-up ReceiveAsync
            // here will throw (channel completed without items) exactly
            // when the peer has drained the release. This is what makes
            // ReleaseAsync a synchronous cluster-wide barrier rather than
            // a fire-and-forget that races every subsequent acquire on
            // the same resource.
            //
            // We bound the wait so a misbehaving or partitioned peer
            // cannot stall the local release indefinitely; on timeout we
            // fall through to dispose-and-log, matching the
            // best-effort-release contract.
            using var ackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ackCts.CancelAfter(_options.ReleaseAckTimeout);
            try
            {
                _ = await conn.ReceiveAsync(ackCts.Token).ConfigureAwait(false);
                // A real reply on the release channel is unexpected (the
                // protocol has no LEASE_RELEASE_ACK frame) but harmless —
                // log and treat as ack.
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Release ack from {Peer} timed out after {Timeout}.",
                    peerEndpoint, _options.ReleaseAckTimeout);
            }
            catch
            {
                // Expected path: peer closed its half of the connection
                // after applying HandleLeaseRelease. ReadAsync on a
                // completed channel throws — that throw is the ack.
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Release broadcast to {Peer} failed (ignored).", peerEndpoint);
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

    private bool TryLocalSelfGrant(string resourceId, string leaseId)
    {
        // If the conflict cache has an unexpired entry for this resource
        // that isn't ours (or isn't this lease id), we can't self-grant.
        var now = DateTimeOffset.UtcNow;
        var added = _conflictCache.AddOrUpdate(
            resourceId,
            addValueFactory: _ => new GrantRecord(leaseId, _localNodeId, now + _options.DefaultLeaseDuration),
            updateValueFactory: (_, existing) => existing.ExpiresAt <= now
                ? new GrantRecord(leaseId, _localNodeId, now + _options.DefaultLeaseDuration)
                : existing);
        return added.LeaseId == leaseId && added.GrantorNodeId == _localNodeId;
    }

    private int EffectiveQuorum(int peerCount)
    {
        // Peer count from IGossipDaemon excludes us; cluster size N counts
        // the local node. ceil(N/2)+1 is the paper §6.3 default.
        if (_options.QuorumSize > 0)
        {
            return _options.QuorumSize;
        }
        var n = peerCount + 1;
        return (n / 2) + 1;
    }

    // ------------------------------------------------------------------
    // Responder loop
    // ------------------------------------------------------------------

    private async Task ResponderLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var conn in _transport.ListenAsync(ct).ConfigureAwait(false))
            {
                _ = Task.Run(() => HandleInboundAsync(conn, ct), ct);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Responder loop exited.");
        }
    }

    private async Task HandleInboundAsync(ISyncDaemonConnection conn, CancellationToken ct)
    {
        try
        {
            await using var _c = conn;
            while (!ct.IsCancellationRequested)
            {
                object inbound;
                try
                {
                    inbound = await conn.ReceiveAsync(ct).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return; // connection closed by peer
                }

                switch (inbound)
                {
                    case LeaseRequestMessage req:
                        await HandleLeaseRequestAsync(conn, req, ct).ConfigureAwait(false);
                        return; // one request per connection (matches ProposeToPeerAsync above)

                    case LeaseReleaseMessage rel:
                        HandleLeaseRelease(rel);
                        return;

                    default:
                        // Unknown message types are simply ignored — this
                        // listener is lease-only and does not participate in
                        // the main sync-daemon handshake ladder.
                        return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Inbound lease frame processing failed.");
        }
    }

    private async Task HandleLeaseRequestAsync(
        ISyncDaemonConnection conn,
        LeaseRequestMessage req,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var requesterNodeId = System.Text.Encoding.UTF8.GetString(req.RequesterNodeId);

        // Conflict check against our cache. If the cached entry is expired
        // we let the new request win.
        var winner = _conflictCache.AddOrUpdate(
            req.ResourceId,
            addValueFactory: _ => new GrantRecord(
                LeaseId: EncodeLeaseIdHex(req.LeaseId),
                GrantorNodeId: requesterNodeId,
                ExpiresAt: now + TimeSpan.FromSeconds(req.RequestedDurationSeconds)),
            updateValueFactory: (_, existing) => existing.ExpiresAt <= now
                ? new GrantRecord(
                    LeaseId: EncodeLeaseIdHex(req.LeaseId),
                    GrantorNodeId: requesterNodeId,
                    ExpiresAt: now + TimeSpan.FromSeconds(req.RequestedDurationSeconds))
                : existing);

        var grantedToCaller = winner.LeaseId == EncodeLeaseIdHex(req.LeaseId);
        if (grantedToCaller)
        {
            var grant = new LeaseGrantMessage(
                LeaseId: req.LeaseId,
                GrantedDurationSeconds: req.RequestedDurationSeconds,
                GrantedAt: (ulong)now.ToUnixTimeMilliseconds(),
                GrantorNodeId: System.Text.Encoding.UTF8.GetBytes(_localNodeId));
            try
            {
                await conn.SendAsync(grant, ct).ConfigureAwait(false);
            }
            catch { /* best-effort reply */ }
        }
        else
        {
            var denied = new LeaseDeniedMessage(
                LeaseId: req.LeaseId,
                Reason: DenyReasonConflict,
                HeldBy: System.Text.Encoding.UTF8.GetBytes(winner.GrantorNodeId));
            try
            {
                await conn.SendAsync(denied, ct).ConfigureAwait(false);
            }
            catch { /* best-effort reply */ }
        }
    }

    private void HandleLeaseRelease(LeaseReleaseMessage rel)
    {
        var leaseIdHex = EncodeLeaseIdHex(rel.LeaseId);
        foreach (var kv in _conflictCache.ToArray())
        {
            if (kv.Value.LeaseId == leaseIdHex)
            {
                _conflictCache.TryRemove(kv.Key, out _);
            }
        }
    }

    // ------------------------------------------------------------------
    // Expiry sweep
    // ------------------------------------------------------------------

    private async Task PruneLoopAsync(CancellationToken ct)
    {
        var interval = _options.ExpiryPruneInterval > TimeSpan.Zero
            ? _options.ExpiryPruneInterval
            : TimeSpan.FromSeconds(10);
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                PruneOnce();
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    private void PruneOnce()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _conflictCache.ToArray())
        {
            if (kv.Value.ExpiresAt <= now)
            {
                _conflictCache.TryRemove(kv.Key, out _);
            }
        }
        foreach (var kv in _heldLeases.ToArray())
        {
            if (kv.Value.ExpiresAt <= now)
            {
                _heldLeases.TryRemove(kv.Key, out _);
            }
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string GenerateLeaseId()
    {
        Span<byte> buf = stackalloc byte[16];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToHexString(buf);
    }

    private static byte[] DecodeLeaseIdHex(string hex) => Convert.FromHexString(hex);
    private static string EncodeLeaseIdHex(byte[] bytes) => Convert.ToHexString(bytes);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _lifetimeCts.Cancel();
        }
        catch (ObjectDisposedException) { /* already disposed */ }

        try
        {
            if (_responderLoop is not null)
            {
                await _responderLoop.ConfigureAwait(false);
            }
        }
        catch { /* shutdown swallow */ }

        try
        {
            await _prunerLoop.ConfigureAwait(false);
        }
        catch { /* shutdown swallow */ }

        foreach (var sem in _acquireLocks.Values)
        {
            sem.Dispose();
        }
        _acquireLocks.Clear();

        _lifetimeCts.Dispose();
    }

    /// <summary>
    /// Conflict-cache entry. Responder-side record for "lease currently
    /// held by node X on resource Y until Z". Expires when
    /// <see cref="ExpiresAt"/> passes; the pruner sweeps stale entries.
    /// </summary>
    private sealed record GrantRecord(string LeaseId, string GrantorNodeId, DateTimeOffset ExpiresAt);
}
