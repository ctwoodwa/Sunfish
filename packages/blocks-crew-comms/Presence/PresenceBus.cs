using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Sunfish.Blocks.CrewComms.Crypto;
using Sunfish.Blocks.CrewComms.Protocol;
using Sunfish.Blocks.CrewComms.Session;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport;

namespace Sunfish.Blocks.CrewComms.Presence;

/// <summary>
/// Maintains the live presence roster for a tenant: receives signed
/// HEARTBEAT frames from every open session, evicts stale peers at 45s
/// TTL, and broadcasts a fresh HEARTBEAT every 30s. Per ADR 0076.
/// </summary>
/// <remarks>
/// <para>
/// PresenceBus does NOT itself open transports — it consumes <see cref="NativeChannelSession"/>
/// instances registered after handshake completion. The 20-second in-session
/// keepalive is delegated to each session's <c>MaybeSendKeepaliveAsync</c>
/// helper so a quiet TEXT-only session keeps its stream alive without
/// flooding the bus.
/// </para>
/// <para>
/// Speculative relay-HELLO bootstrap (per hand-off) is the Phase-2 path
/// where the roster has peers not yet seen on any tier; PresenceBus fires
/// up to 3 concurrent <see cref="IPeerTransport.ConnectAsync"/> probes (10s
/// budget each) so quiet roster members surface in <see cref="GetSnapshot"/>
/// without waiting for a user-driven INVITE.
/// </para>
/// </remarks>
public sealed class PresenceBus : IAsyncDisposable
{
    private static readonly TimeSpan HeartbeatBroadcastInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PresenceTtl = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan RelayProbeBudget = TimeSpan.FromSeconds(10);
    private const int MaxConcurrentRelayProbes = 3;

    private readonly KeyPair _identity;
    private readonly ICrewRoster _roster;
    private readonly TenantId _tenantId;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<PeerId, CrewPresence> _presence = new();
    private readonly ConcurrentDictionary<PeerId, NativeChannelSession> _sessions = new();
    private readonly SemaphoreSlim _probeGate = new(MaxConcurrentRelayProbes, MaxConcurrentRelayProbes);
    private readonly CancellationTokenSource _stopCts = new();
    private ITimer? _broadcastTimer;
    private bool _disposed;

    /// <summary>Creates a presence bus bound to the supplied identity and tenant roster.</summary>
    public PresenceBus(KeyPair identity, ICrewRoster roster, TenantId tenantId, TimeProvider? time = null)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _roster = roster ?? throw new ArgumentNullException(nameof(roster));
        _tenantId = tenantId;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Starts the 30s heartbeat-broadcast timer. Idempotent.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _broadcastTimer ??= _time.CreateTimer(
            _ => _ = OnTickAsync(),
            state: null,
            dueTime: HeartbeatBroadcastInterval,
            period: HeartbeatBroadcastInterval);
    }

    /// <summary>Registers an active session for keepalive + heartbeat broadcast.</summary>
    public void RegisterSession(NativeChannelSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions[session.Peer] = session;
    }

    /// <summary>Removes a terminated session from the broadcast set.</summary>
    public void UnregisterSession(PeerId peer)
    {
        _sessions.TryRemove(peer, out _);
    }

    /// <summary>
    /// Records a verified HEARTBEAT received from <paramref name="peer"/>. Callers MUST
    /// have already verified the heartbeat signature via
    /// <see cref="EncryptionHandshake.VerifyHeartbeat"/>.
    /// </summary>
    public void OnHeartbeatReceived(PeerId peer, PresenceHeartbeat hb, TransportTier via, string displayName)
    {
        ArgumentNullException.ThrowIfNull(hb);
        var record = new CrewPresence
        {
            Peer = peer,
            TenantId = _tenantId,
            DisplayName = displayName,
            Caps = (ChannelCapability)hb.Caps,
            Status = PresenceStatus.Available,
            Via = via,
            LastSeenAt = DateTimeOffset.FromUnixTimeMilliseconds(hb.Timestamp),
        };
        _presence[peer] = record;
    }

    /// <summary>
    /// Returns the currently-present roster (TTL-evicted at 45s from
    /// <c>LastSeenAt</c>). Order is not guaranteed.
    /// </summary>
    public IReadOnlyList<CrewPresence> GetSnapshot()
    {
        var cutoff = _time.GetUtcNow() - PresenceTtl;
        var live = new List<CrewPresence>(_presence.Count);
        foreach (var kvp in _presence)
        {
            if (kvp.Value.LastSeenAt >= cutoff)
                live.Add(kvp.Value);
            else
                _presence.TryRemove(kvp.Key, out _);
        }
        return live;
    }

    /// <summary>
    /// Attempts to surface roster members not yet seen on any tier by
    /// probing them via <paramref name="relay"/> (Tier 3) with bounded
    /// concurrency. Returns the count of newly-surfaced peers. Per the
    /// hand-off "speculative relay HELLO bootstrap".
    /// </summary>
    public async Task<int> ProbeRelayPeersAsync(IPeerTransport relay, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(relay);
        if (relay.Tier != TransportTier.ManagedRelay)
            throw new ArgumentException("Speculative probe requires the Tier-3 ManagedRelay transport.", nameof(relay));

        var roster = await _roster.GetCrewAsync(_tenantId, ct).ConfigureAwait(false);
        var quiet = roster
            .Select(m => m.Peer)
            .Where(p => p != PeerId.From(_identity.PrincipalId))
            .Where(p => !_presence.ContainsKey(p))
            .ToList();

        var probesLaunched = 0;
        var tasks = quiet.Select(async peer =>
        {
            await _probeGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Interlocked.Increment(ref probesLaunched);
                using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                probeCts.CancelAfter(RelayProbeBudget);
                var endpoint = await relay.ResolvePeerAsync(peer, probeCts.Token).ConfigureAwait(false);
                if (endpoint is null) return false;
                // Resolve-only: this method does NOT open a session — that's the caller's job
                // via NativeChannelProvider.OpenAsync. We just confirm the peer answers Tier-3.
                _presence[peer] = new CrewPresence
                {
                    Peer = peer,
                    TenantId = _tenantId,
                    DisplayName = peer.Value,
                    Caps = ChannelCapability.None,
                    Status = PresenceStatus.Available,
                    Via = TransportTier.ManagedRelay,
                    LastSeenAt = _time.GetUtcNow(),
                };
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                _probeGate.Release();
            }
        });
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Count(r => r);
    }

    /// <summary>Emits a fresh signed HEARTBEAT payload for the local peer.</summary>
    internal byte[] BuildHeartbeatPayload(ChannelCapability caps)
    {
        var now = _time.GetUtcNow();
        var hb = BuildHeartbeat(caps, now);
        return MessagePackSerializer.Serialize(hb, CrewCommsResolver.Options);
    }

    private async Task OnTickAsync()
    {
        if (_stopCts.IsCancellationRequested) return;

        // 1. TTL-evict stale presence entries.
        var cutoff = _time.GetUtcNow() - PresenceTtl;
        foreach (var kvp in _presence)
        {
            if (kvp.Value.LastSeenAt < cutoff)
                _presence.TryRemove(kvp.Key, out _);
        }

        // 2. Broadcast HEARTBEAT to every registered session.
        if (_sessions.IsEmpty) return;
        var payload = BuildHeartbeatPayload(ChannelCapability.Text);
        var sessions = _sessions.Values.ToArray();
        foreach (var session in sessions)
        {
            try
            {
                await session.MaybeSendKeepaliveAsync(payload, _stopCts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Per-session failures are not fatal to the bus; the session itself
                // will surface termination through its Completed task.
            }
        }
    }

    private PresenceHeartbeat BuildHeartbeat(ChannelCapability caps, DateTimeOffset now)
    {
        var pubKey = _identity.PrincipalId.AsSpan().ToArray();
        var tenantBytes = EncryptionHandshake.TenantBytes(_tenantId);
        var ts = now.ToUnixTimeMilliseconds();

        var signableLen = pubKey.Length + tenantBytes.Length + 1 + sizeof(long);
        var signable = new byte[signableLen];
        var span = signable.AsSpan();
        var offset = 0;
        pubKey.CopyTo(span.Slice(offset, pubKey.Length));
        offset += pubKey.Length;
        tenantBytes.AsSpan().CopyTo(span.Slice(offset, tenantBytes.Length));
        offset += tenantBytes.Length;
        span[offset++] = (byte)caps;
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(span.Slice(offset, sizeof(long)), ts);

        var sig = _identity.Sign(signable);
        return new PresenceHeartbeat
        {
            PeerId = PeerId.From(_identity.PrincipalId).Value,
            TenantId = _tenantId.Value,
            Caps = (byte)caps,
            Timestamp = ts,
            Signature = sig.AsSpan().ToArray(),
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _stopCts.CancelAsync().ConfigureAwait(false);
        if (_broadcastTimer is not null)
            await _broadcastTimer.DisposeAsync().ConfigureAwait(false);
        _probeGate.Dispose();
        _stopCts.Dispose();
    }
}
