using System;
using System.Net;
using Sunfish.Federation.Common;

namespace Sunfish.Foundation.Transport;

/// <summary>
/// A resolved peer endpoint — the result of
/// <see cref="IPeerTransport.ResolvePeerAsync"/>. Captures the tier the
/// resolution came from so callers can record which transport answered.
/// </summary>
public sealed record PeerEndpoint
{
    /// <summary>The federation peer this endpoint represents.</summary>
    public required PeerId Peer { get; init; }

    /// <summary>The IP + port the transport reached the peer at.</summary>
    public required IPEndPoint Endpoint { get; init; }

    /// <summary>The transport tier that produced this resolution.</summary>
    public required TransportTier Tier { get; init; }

    /// <summary>When the resolution was produced (UTC).</summary>
    public required DateTimeOffset DiscoveredAt { get; init; }

    /// <summary>
    /// When the endpoint was last seen alive (UTC). Optional; set by
    /// transports that track liveness (mesh-VPN handshake telemetry,
    /// Bridge-relay heartbeat).
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; init; }
}
