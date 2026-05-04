using System;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Transport;

namespace Sunfish.Foundation.Channels;

/// <summary>
/// Live presence record for a crew member: the peer's identifier, tenant
/// scope, capabilities advertised in the most-recent HEARTBEAT, presence
/// status, and the transport tier they were last reached on. Per ADR 0076.
/// </summary>
public sealed record CrewPresence
{
    /// <summary>Federation peer identifier.</summary>
    public required PeerId Peer { get; init; }

    /// <summary>Tenant scope this presence record belongs to.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Display name carried in the peer's HELLO + HEARTBEAT.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Capabilities advertised in the most-recent HEARTBEAT (flags-combined).</summary>
    public required ChannelCapability Caps { get; init; }

    /// <summary>Operator-visible presence state.</summary>
    public required PresenceStatus Status { get; init; }

    /// <summary>Transport tier the peer was last reached on (mDNS / mesh-VPN / Bridge relay).</summary>
    public required TransportTier Via { get; init; }

    /// <summary>Wall-clock time of the most-recent HELLO or HEARTBEAT from this peer.</summary>
    public required DateTimeOffset LastSeenAt { get; init; }
}
