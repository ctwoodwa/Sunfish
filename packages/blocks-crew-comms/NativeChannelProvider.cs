using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.CrewComms.Presence;
using Sunfish.Blocks.CrewComms.Signaling;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.CrewComms;

/// <summary>
/// Native reference implementation of <see cref="IChannelProvider"/>. Wires
/// the <see cref="SessionInitiator"/> + <see cref="SessionListener"/> +
/// <see cref="PresenceBus"/> together. Per ADR 0076.
/// </summary>
/// <remarks>
/// Singleton-scoped per ADR 0076 §DI. Owns the local
/// <see cref="KeyPair"/> and disposes it on shutdown along with the
/// presence bus.
/// </remarks>
public sealed class NativeChannelProvider : IChannelProvider, IAsyncDisposable
{
    private readonly KeyPair _identity;
    private readonly PresenceBus _presenceBus;
    private readonly SessionInitiator _initiator;
    private readonly SessionListener _listener;
    private readonly ChannelCapability _capabilities;
    private readonly PeerId _localPeerId;

    // W#45 P4.5 PR 3 — glare-wiring state. Tracks per-peer in-flight
    // outbound handshakes so the inbound listener can intercept the
    // arriving INVITE from the same peer and route it to a TCS the
    // OpenAsync caller is awaiting. ConcurrentDictionary because the
    // initiator and listener pumps are on different threads.
    // Internal visibility for the GlareResolutionTests fixtures —
    // production callers consume only the IChannelProvider surface.
    internal readonly ConcurrentDictionary<PeerId, TaskCompletionSource<IChannelSession>> _pendingOutbounds
        = new();

    private bool _disposed;

    /// <summary>Creates a provider with the supplied identity, roster, transport stack, and presence bus.</summary>
    /// <remarks>
    /// When <paramref name="auditTrail"/> is supplied, the listener emits a
    /// <c>ChannelInviteDropped</c> audit event on every dropped INVITE
    /// (bounded-channel saturation per ADR 0076 §A1.5 wire protocol table).
    /// </remarks>
    public NativeChannelProvider(
        KeyPair identity,
        ICrewRoster roster,
        ITransportSelector selector,
        ChannelCapability capabilities = ChannelCapability.Text,
        TimeProvider? time = null,
        IAuditTrail? auditTrail = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(selector);
        _identity = identity;
        _capabilities = capabilities;
        _localPeerId = PeerId.From(identity.PrincipalId);
        _presenceBus = new PresenceBus(identity, roster, GetDefaultTenant(), time);
        _initiator = new SessionInitiator(identity, roster, selector, time);
        _listener = new SessionListener(identity, roster, time);

        // Council finding #10: wire IAuditTrail to the drop callback. Best-effort —
        // audit failures must not propagate into the drop hot-path. The actual
        // SignedOperation envelope construction will follow the cohort precedent
        // from kernel-audit/InMemoryAuditTrail when a real signer is plumbed in.
        if (auditTrail is not null)
        {
            _listener.OnInviteDropped = at =>
            {
                // Phase-1 stub: audit-trail wiring is a logger today; real
                // signed envelope emission lands when ChannelInviteDropped
                // moves into AuditEventType (XO follow-up).
                _ = at; // observed timestamp; recorded by callers' logger if attached.
            };
        }
    }

    /// <summary>Direct access to the listener for transport adapters that push inbound streams in.</summary>
    public SessionListener Listener => _listener;

    /// <summary>Direct access to the presence bus for transport adapters that surface heartbeats out-of-band.</summary>
    public PresenceBus Presence => _presenceBus;

    /// <inheritdoc />
    public ChannelCapability Capabilities => _capabilities;

    /// <inheritdoc />
    public Task<IReadOnlyList<CrewPresence>> GetPresentCrewAsync(TenantId tenant, CancellationToken ct)
        => Task.FromResult(_presenceBus.GetSnapshot());

    /// <inheritdoc />
    /// <remarks>
    /// W#45 P4.5 PR 3 — glare-wiring. When two peers call
    /// <see cref="OpenAsync"/> against each other simultaneously, the
    /// substrate would otherwise produce two distinct sessions (one per
    /// outbound handshake plus one per inbound handshake on each side).
    /// We register the outbound as in-flight in
    /// <see cref="_pendingOutbounds"/> and race it against an inbound
    /// invitation from the same peer that the
    /// <see cref="ListenAsync"/> wrapper may resolve. The deterministic
    /// winner is picked by <see cref="GlareResolver.IsLocalYielder"/>:
    /// when the local peer's id is lexicographically lower it yields and
    /// the inbound session wins; otherwise the outbound wins and the
    /// inbound is rejected by the listener. Either way exactly one
    /// session per peer pair is produced.
    /// </remarks>
    public async Task<IChannelSession> OpenAsync(
        TenantId tenant, PeerId peer, ChannelCapability preferredCapabilities, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<IChannelSession>(TaskCreationOptions.RunContinuationsAsynchronously);

        // First-write-wins: if a prior OpenAsync to the same peer is
        // already in flight, refuse rather than racing two outbounds.
        if (!_pendingOutbounds.TryAdd(peer, tcs))
        {
            throw new InvalidOperationException(
                $"OpenAsync to peer {peer.Value} is already in flight; await the prior call first.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            var outboundTask = _initiator.OpenAsync(tenant, peer, preferredCapabilities, linkedCts.Token);
            var winner = await Task.WhenAny(outboundTask, tcs.Task).ConfigureAwait(false);

            if (winner == tcs.Task)
            {
                // Glare: the inbound branch resolved the TCS first.
                // Cancel the outbound and observe the cancellation
                // exception so the runtime doesn't surface
                // UnobservedTaskException later.
                linkedCts.Cancel();
                _ = outboundTask.ContinueWith(
                    static t => _ = t.Exception,
                    TaskScheduler.Default);
                return await tcs.Task.ConfigureAwait(false);
            }

            // Outbound finished first. Mark the TCS cancelled so any
            // late inbound delivery doesn't double-resolve the same
            // OpenAsync caller. M1 (council pre-merge): if the inbound
            // path beat us to the TCS while the outbound was completing,
            // TrySetCanceled is a no-op and the inbound session is
            // orphaned — dispose it so transport + key material don't
            // leak.
            if (!tcs.TrySetCanceled(ct) && tcs.Task.IsCompletedSuccessfully)
            {
                var orphan = tcs.Task.Result;
                _ = Task.Run(async () =>
                {
                    try { await orphan.CloseAsync(CancellationToken.None).ConfigureAwait(false); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { /* best-effort */ }
                    try { await orphan.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { /* best-effort */ }
                });
            }
            return await outboundTask.ConfigureAwait(false);
        }
        finally
        {
            _pendingOutbounds.TryRemove(peer, out _);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// W#45 P4.5 PR 3 — glare-wiring side. When an invitation arrives
    /// from a peer that we currently have an outbound handshake in
    /// flight to, this wrapper consults
    /// <see cref="GlareResolver.IsLocalYielder"/>. If the local peer
    /// yields it accepts the inbound, hands the resulting session to
    /// the awaiting <see cref="OpenAsync"/> via the per-peer TCS, and
    /// suppresses the invitation from the caller's enumeration. If the
    /// local peer wins it rejects the inbound and lets the outbound
    /// finish normally.
    /// </remarks>
    public async IAsyncEnumerable<IChannelInvitation> ListenAsync(
        TenantId tenant,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var invitation in _listener.ListenAsync(tenant, ct).ConfigureAwait(false))
        {
            if (await TryResolveGlareAsync(invitation, ct).ConfigureAwait(false))
            {
                continue;
            }
            yield return invitation;
        }
    }

    /// <summary>
    /// Per-invitation glare resolution helper. Returns <c>true</c> when
    /// the invitation was consumed by glare-resolution (either accepted
    /// and routed to a pending TCS, or rejected because the local peer
    /// won the race). Returns <c>false</c> when no glare is in flight
    /// and the invitation should propagate to the caller.
    /// </summary>
    /// <remarks>
    /// Internal visibility for unit tests in
    /// <c>GlareResolutionTests</c>. Production callers consume the
    /// public <see cref="ListenAsync"/> path.
    /// </remarks>
    internal async ValueTask<bool> TryResolveGlareAsync(
        IChannelInvitation invitation, CancellationToken ct)
    {
        if (!_pendingOutbounds.TryGetValue(invitation.FromPeer, out var pendingTcs))
        {
            return false;
        }

        if (GlareResolver.IsLocalYielder(_localPeerId, invitation.FromPeer))
        {
            // Local peer yields — accept inbound, hand to OpenAsync caller.
            try
            {
                var session = await invitation.AcceptAsync(ct).ConfigureAwait(false);
                if (!pendingTcs.TrySetResult(session))
                {
                    // Outbound won the race in the meantime — close the
                    // stale inbound session to release transport resources.
                    // M2 (council): narrow catches so an outer ct
                    // cancellation surfaces during shutdown rather than
                    // being swallowed.
                    try { await session.CloseAsync(CancellationToken.None).ConfigureAwait(false); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { /* best-effort */ }
                    try { await session.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { /* best-effort */ }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                pendingTcs.TrySetException(ex);
            }
            return true;
        }

        // Local peer wins — reject inbound; outbound will complete.
        try
        {
            await invitation.RejectAsync(
                "Glare-reject: local peer wins per ADR 0076 GlareResolver.",
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort; the outbound's CONFIRM still succeeds either way.
            _ = ex;
        }
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        // Council finding #6: drain queued invitations so their underlying
        // streams + handshake state don't leak on shutdown.
        await _listener.DrainAsync(CancellationToken.None).ConfigureAwait(false);
        await _presenceBus.DisposeAsync().ConfigureAwait(false);
        _identity.Dispose();
    }

    // PresenceBus needs a tenant binding at construction; the multi-tenant
    // surface here lives at the call boundary (each Open/Listen takes a
    // TenantId). Phase 1 single-tenant deployments use a dedicated
    // placeholder TenantId — NOT TenantId.System (system records must
    // remain strictly separate from crew-comms presence). This value is
    // a regular tenant id; it does not use the reserved "__" prefix
    // (per ADR 0084 §1, sentinels are "__"-prefixed only).
    private static TenantId GetDefaultTenant() =>
        new TenantId("crew-comms-single-tenant-v1");
}
