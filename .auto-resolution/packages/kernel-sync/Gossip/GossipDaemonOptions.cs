namespace Sunfish.Kernel.Sync.Gossip;

/// <summary>
/// Tunable knobs for the <see cref="GossipDaemon"/>. Bound via
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>.
/// </summary>
/// <remarks>
/// Defaults match paper §6.1 / sync-daemon-protocol §2.3: 30-second gossip
/// tick, two random peers per round, 5-second connect timeout, 60-second
/// dead-peer cool-off window. Override per-deployment if the local topology
/// needs a different cadence.
/// </remarks>
public sealed class GossipDaemonOptions
{
    /// <summary>
    /// Seconds between gossip rounds. Default 30, matching paper §6.1.
    /// Tests typically set this to 1.
    /// </summary>
    public int RoundIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Number of random peers gossiped to per round. Default 2 per paper §6.1
    /// ("two random peers" — open question in the spec leaves this a knob).
    /// </summary>
    public int PeerPickCount { get; set; } = 2;

    /// <summary>
    /// How long a single peer connect + handshake is allowed to take before
    /// the peer is marked dead for the current round. Default 5 seconds
    /// (sync-daemon-protocol §7 timeout schedule).
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Initial cool-off window after a peer times out. The actual skip window
    /// grows exponentially on repeated failures (doubles per strike, capped
    /// at 4× this value). A successful round resets the counter and clears
    /// the skip window entirely.
    /// </summary>
    public int DeadPeerBackoffSeconds { get; set; } = 60;

    /// <summary>
    /// Per-peer DELTA_STREAM budget, enforced by
    /// <see cref="Protocol.DeltaStreamRateLimiter"/>. Default 1000 per
    /// sync-daemon-protocol §8 "Rate limiting". Incoming DELTA_STREAM
    /// frames above this rate are dropped and logged at warning level.
    /// </summary>
    public int MaxDeltaStreamPerSecondPerPeer { get; set; } = 1000;
}
