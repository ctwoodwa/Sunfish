using System;
using System.Collections.Generic;
using System.Net;
using Sunfish.Federation.Common;

namespace Sunfish.Foundation.Transport;

/// <summary>
/// Snapshot of a mesh-VPN adapter's control-plane state (per
/// <see cref="IMeshVpnAdapter.GetMeshStatusAsync"/>). Diagnostic /
/// observability surface; not consumed by the selector itself.
/// </summary>
public sealed record MeshNodeStatus
{
    /// <summary>Whether the local node is currently authenticated to the mesh control plane.</summary>
    public required bool IsConnected { get; init; }

    /// <summary>Peers currently visible to this mesh adapter.</summary>
    public required IReadOnlyList<MeshPeer> Peers { get; init; }

    /// <summary>Time of the most recent successful WireGuard handshake (UTC). Null if never handshaken.</summary>
    public DateTimeOffset? LastHandshakeAt { get; init; }
}

/// <summary>
/// One peer visible inside a mesh-VPN adapter — the federation
/// <see cref="PeerId"/>, the mesh-tunnel-resolved IPEndPoint, and the
/// last-handshake timestamp the selector uses for its
/// most-recently-handshaked tie-break.
/// </summary>
public sealed record MeshPeer
{
    /// <summary>The Sunfish federation peer this mesh peer maps to.</summary>
    public required PeerId Peer { get; init; }

    /// <summary>The IP + port reachable through the mesh-VPN tunnel.</summary>
    public required IPEndPoint MeshEndpoint { get; init; }

    /// <summary>Timestamp of the last WireGuard handshake (UTC).</summary>
    public required DateTimeOffset LastHandshakeAt { get; init; }
}
