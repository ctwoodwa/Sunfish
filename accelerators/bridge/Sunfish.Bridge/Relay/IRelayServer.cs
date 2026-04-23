namespace Sunfish.Bridge.Relay;

/// <summary>
/// Paper §6.1 tier-3 managed-relay server contract. Accepts inbound peer
/// connections on <c>ISyncDaemonTransport</c>, runs the
/// <c>HandshakeProtocol</c> ladder to authenticate them, then fans out
/// subsequent <c>DELTA_STREAM</c> / <c>GOSSIP_PING</c> frames to other
/// connected peers in the same team.
/// </summary>
/// <remarks>
/// <para>
/// <b>Statelessness.</b> The relay persists nothing beyond the in-memory
/// connection set (<see cref="ConnectedNodes"/>). If the relay crashes,
/// peers reconnect and resume gossip from their own local state — paper
/// §6.1 tier-3: "lightweight relay for teams where direct peer
/// connectivity is not viable."
/// </para>
/// <para>
/// <b>Authority.</b> The relay has no authority semantics. It does not
/// resolve CRDT conflicts, does not keep a causal history, does not vend
/// leases. It is a fan-out forwarder with a handshake gate.
/// </para>
/// </remarks>
public interface IRelayServer : IAsyncDisposable
{
    /// <summary>
    /// Start the accept loop. Subsequent calls while running are
    /// idempotent (no-op). Throws if the server has already been disposed.
    /// </summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Signal the accept loop to stop and drop all current connections.
    /// Idempotent.
    /// </summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>Current connected-peer snapshot. Enumeration is safe across accepts.</summary>
    IReadOnlyCollection<ConnectedNode> ConnectedNodes { get; }

    /// <summary>Convenience count — equivalent to <c>ConnectedNodes.Count</c>.</summary>
    int ConnectedCount { get; }

    /// <summary>Fires on every successful handshake completion.</summary>
    event EventHandler<NodeConnectedEventArgs>? NodeConnected;

    /// <summary>Fires when a peer disconnects (clean close, transport error, or relay shutdown).</summary>
    event EventHandler<NodeDisconnectedEventArgs>? NodeDisconnected;
}

/// <summary>Snapshot of one connected peer held by the relay.</summary>
public sealed record ConnectedNode(
    string NodeId,
    string RemoteEndpoint,
    string TeamId,
    DateTimeOffset ConnectedAt);

/// <summary>Event payload for <see cref="IRelayServer.NodeConnected"/>.</summary>
public sealed record NodeConnectedEventArgs(ConnectedNode Node);

/// <summary>Event payload for <see cref="IRelayServer.NodeDisconnected"/>.</summary>
public sealed record NodeDisconnectedEventArgs(string NodeId, string Reason);
