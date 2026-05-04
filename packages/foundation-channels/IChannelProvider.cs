using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Channels;

/// <summary>
/// Top-level provider surface for crew-comms channels. The native
/// reference implementation (<c>NativeChannelProvider</c> in
/// <c>blocks-crew-comms</c>) ships in Phase 3+ of ADR 0076. Per ADR 0076.
/// </summary>
public interface IChannelProvider
{
    /// <summary>
    /// Capabilities this provider can negotiate (flags-combined). The
    /// substrate-v1 (Phase 1) provider supports only
    /// <see cref="ChannelCapability.Text"/>; Phase 3 extends with
    /// <see cref="ChannelCapability.Audio"/>; Phase 4 with
    /// <see cref="ChannelCapability.Video"/>.
    /// </summary>
    ChannelCapability Capabilities { get; }

    /// <summary>
    /// Resolve every currently-present crew member for the tenant —
    /// the union of <see cref="ICrewRoster.GetCrewAsync"/> + the
    /// presence bus's TTL-evicted heartbeat state.
    /// </summary>
    Task<IReadOnlyList<CrewPresence>> GetPresentCrewAsync(TenantId tenant, CancellationToken ct);

    /// <summary>
    /// Open a new outbound channel session.
    /// </summary>
    /// <param name="tenant">Tenant scope of both peers.</param>
    /// <param name="peer">Remote peer to open against.</param>
    /// <param name="preferredCapabilities">
    /// Flags-combined value indicating desired capabilities. The
    /// implementation selects the highest common capability between
    /// this value and the remote peer's advertised capabilities. Use
    /// <see cref="ChannelCapability.Text"/> for Phase 1 text-only sessions.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly-established session in <see cref="ChannelSessionState.Active"/>.</returns>
    Task<IChannelSession> OpenAsync(
        TenantId tenant,
        PeerId peer,
        ChannelCapability preferredCapabilities,
        CancellationToken ct);

    /// <summary>
    /// Stream incoming channel invitations for the supplied tenant.
    /// </summary>
    /// <remarks>
    /// Backed by a bounded <c>System.Threading.Channels.Channel</c> of
    /// capacity 16. Incoming INVITEs are dropped when the channel is
    /// full; a <c>ChannelInviteDropped</c> audit event is emitted on each
    /// drop. Callers MUST process each <see cref="IChannelInvitation"/>
    /// promptly (accept or reject) so the bounded channel doesn't fill.
    /// </remarks>
    IAsyncEnumerable<IChannelInvitation> ListenAsync(TenantId tenant, CancellationToken ct);
}
