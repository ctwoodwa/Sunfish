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

    /// <summary>Fires on every successful gossip round; useful for tests and observability.</summary>
    event EventHandler<GossipRoundCompletedEventArgs>? RoundCompleted;
}

/// <summary>
/// Membership record kept by the gossip daemon. Public surface intentionally
/// small — richer attributes (role, attestation snapshots, ...) live in
/// sibling packages and flow through the handshake, not through membership.
/// </summary>
public sealed record PeerInfo(
    string Endpoint,
    byte[] PublicKey,
    DateTimeOffset LastSeenAt,
    ulong LastSeenVectorClock);

/// <summary>Payload of <see cref="IGossipDaemon.RoundCompleted"/>.</summary>
public sealed record GossipRoundCompletedEventArgs(
    int PeersSelected,
    int DeltasExchanged,
    int OpsReceived);
