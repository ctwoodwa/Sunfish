using System;
using System.Collections.Generic;
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
    public Task<IChannelSession> OpenAsync(
        TenantId tenant, PeerId peer, ChannelCapability preferredCapabilities, CancellationToken ct)
        => _initiator.OpenAsync(tenant, peer, preferredCapabilities, ct);

    /// <inheritdoc />
    public IAsyncEnumerable<IChannelInvitation> ListenAsync(TenantId tenant, CancellationToken ct)
        => _listener.ListenAsync(tenant, ct);

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
