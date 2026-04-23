namespace Sunfish.Kernel.Sync.Gossip;

/// <summary>
/// Paper §6.1 gossip-based anti-entropy daemon. Every
/// <see cref="GossipDaemonOptions.RoundIntervalSeconds"/> seconds, picks
/// <see cref="GossipDaemonOptions.PeerPickCount"/> random peers from
/// <see cref="KnownPeers"/> and exchanges HELLO / CAPABILITY_NEG / ACK /
/// DELTA_STREAM / GOSSIP_PING with each.
/// </summary>
public interface IGossipDaemon : IAsyncDisposable
{
    /// <summary>
    /// Start the round scheduler. Subsequent calls while running are
    /// idempotent (no-op). Throws if the daemon has already been disposed.
    /// </summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Signal the scheduler to stop. Waits for the in-flight round (if any)
    /// to finish, then returns. Idempotent.
    /// </summary>
    Task StopAsync(CancellationToken ct);

    /// <summary>Add a known peer for gossip rounds to consider.</summary>
    void AddPeer(string peerEndpoint, byte[] peerPublicKey);

    /// <summary>Remove a peer from the membership list.</summary>
    void RemovePeer(string peerEndpoint);

    /// <summary>Current membership snapshot. Enumeration is safe across rounds.</summary>
    IReadOnlyCollection<PeerInfo> KnownPeers { get; }

    /// <summary>
    /// Whether the daemon's round loop is currently running. Transitions from
    /// <c>false</c> to <c>true</c> on <see cref="StartAsync"/> and back to
    /// <c>false</c> on <see cref="StopAsync"/> / <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// Surfaced for health-check consumers (Wave 5.2.D
    /// <c>LocalNodeHealthCheck</c>) that need to distinguish "active team exists
    /// but gossip is not yet spinning" (Degraded) from "active team + gossip
    /// running" (Healthy).
    /// </summary>
    bool IsRunning { get; }

    /// <summary>Fires on every successful gossip round; useful for tests and observability.</summary>
    event EventHandler<GossipRoundCompletedEventArgs>? RoundCompleted;

    /// <summary>
    /// Fires once for every inbound sync-daemon frame the round loop
    /// successfully observed from a peer — completed HELLO handshakes,
    /// received GOSSIP_PINGs, and round-level errors (handshake failure,
    /// generic gossip error).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The event is coarser than the wire protocol: it does not fire for
    /// purely outbound frames (the daemon's own PING send) nor for inbound
    /// frames that the round loop currently ignores (ACK, DELTA_STREAM —
    /// those are still Wave 2.6 / 6.x territory). The intent is to give
    /// notification producers (Wave 6.5
    /// <c>GossipEventTeamNotificationStream</c>) a stable signal of
    /// "something observable happened with peer X" without coupling them to
    /// the daemon's internal frame-dispatch state machine.
    /// </para>
    /// <para>
    /// Handlers run synchronously on the round loop's task. They must not
    /// block — if a consumer needs to fan out heavy work, it should
    /// offload to its own queue / channel (see
    /// <c>GossipEventTeamNotificationStream</c> for the canonical pattern).
    /// </para>
    /// </remarks>
    event EventHandler<GossipFrameEventArgs>? FrameReceived;
}

/// <summary>
/// Membership record kept by the gossip daemon. Public surface intentionally
/// small — richer attributes (role, attestation snapshots, ...) live in
/// sibling packages and flow through the handshake, not through membership.
/// </summary>
/// <remarks>
/// <see cref="LastSeenNonce"/> tracks the highest <c>monotonic_nonce</c> seen
/// in a GOSSIP_PING from this peer (sync-daemon-protocol §8 replay
/// protection). Initial value is <c>0</c> — any real peer PING carries a
/// strictly-positive nonce, so zero unambiguously means "no PING yet".
/// </remarks>
public sealed record PeerInfo(
    string Endpoint,
    byte[] PublicKey,
    DateTimeOffset LastSeenAt,
    ulong LastSeenVectorClock,
    ulong LastSeenNonce = 0);

/// <summary>Payload of <see cref="IGossipDaemon.RoundCompleted"/>.</summary>
public sealed record GossipRoundCompletedEventArgs(
    int PeersSelected,
    int DeltasExchanged,
    int OpsReceived);

/// <summary>
/// Coarse classification of an observable event emitted through
/// <see cref="IGossipDaemon.FrameReceived"/>. The enum is deliberately
/// broader than the wire-level <c>MessageTypes</c> discriminator — it
/// folds in round-level outcomes (<see cref="HandshakeFailure"/>,
/// <see cref="GossipError"/>) so consumers can render them as
/// notifications without needing to dissect the receive-loop plumbing.
/// </summary>
public enum GossipFrameType
{
    /// <summary>An inbound HELLO (handshake) completed successfully.</summary>
    Hello = 0,

    /// <summary>An inbound GOSSIP_PING was received and accepted.</summary>
    GossipPing = 1,

    /// <summary>An inbound DELTA_STREAM frame was processed.</summary>
    DeltaStream = 2,

    /// <summary>
    /// The handshake against this peer failed (signature invalid,
    /// schema-incompatible, timeout during HELLO/CAPABILITY_NEG).
    /// </summary>
    HandshakeFailure = 3,

    /// <summary>
    /// A generic gossip-round error against this peer (connect failed,
    /// transport dropped mid-exchange, unexpected exception).
    /// </summary>
    GossipError = 4,
}

/// <summary>
/// Payload of <see cref="IGossipDaemon.FrameReceived"/>.
/// </summary>
/// <param name="PeerEndpoint">The sync-daemon endpoint of the peer that
/// produced the frame (transport-level; e.g. unix socket path or
/// websocket URL). Unique per peer on a given node.</param>
/// <param name="PeerNodeId">Opaque per-peer identifier (hex-encoded
/// 16-byte node id, lowercase). Empty string if the peer's identity has
/// not yet been validated (e.g. <see cref="GossipFrameType.HandshakeFailure"/>
/// before HELLO completed).</param>
/// <param name="FrameType">Which class of frame was observed.</param>
/// <param name="OccurredAt">UTC timestamp the frame was observed.</param>
/// <param name="Summary">Optional human-readable one-liner suitable for
/// a notification tooltip (e.g. <c>"alice sent gossip ping"</c>).
/// <c>null</c> means consumers should synthesise their own from
/// <see cref="FrameType"/>.</param>
public sealed record GossipFrameEventArgs(
    string PeerEndpoint,
    string PeerNodeId,
    GossipFrameType FrameType,
    DateTimeOffset OccurredAt,
    string? Summary = null);
