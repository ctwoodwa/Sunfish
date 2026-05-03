namespace Sunfish.Kernel.Sync.Discovery;

/// <summary>
/// Tunable knobs for <see cref="IPeerDiscovery"/> implementations. Bound via
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>.
/// </summary>
/// <remarks>
/// Defaults match paper §6.1 tier-1 (mDNS): 5-second re-announce, 30-second
/// TTL on a peer record, team-scoped filtering. The service-type string
/// (<c>_sunfish-node._tcp.local</c>) is fixed in the sync-daemon-protocol
/// spec §3.1 but left configurable so staging / prod / developer-sandbox can
/// run on the same segment without cross-contamination.
/// </remarks>
public sealed class PeerDiscoveryOptions
{
    /// <summary>
    /// DNS-SD service type. Default <c>_sunfish-node._tcp.local</c> per
    /// sync-daemon-protocol spec §3.1.
    /// </summary>
    public string ServiceType { get; set; } = "_sunfish-node._tcp.local";

    /// <summary>
    /// TCP port advertised in the SRV record. Default 8765.
    /// </summary>
    public int Port { get; set; } = 8765;

    /// <summary>
    /// Re-announce interval in seconds. A peer that has not re-announced in
    /// <see cref="PeerTtlSeconds"/> is evicted and <see cref="IPeerDiscovery.PeerLost"/>
    /// fires.
    /// </summary>
    public int DiscoveryIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Peer time-to-live in seconds. Default 30 — six missed announcements at
    /// the default interval.
    /// </summary>
    public int PeerTtlSeconds { get; set; } = 30;

    /// <summary>
    /// When <c>true</c>, peers whose TXT-record <c>team</c> differs from our
    /// own are suppressed before <see cref="IPeerDiscovery.PeerDiscovered"/>
    /// fires. Default <c>true</c>.
    /// </summary>
    public bool FilterByTeamId { get; set; } = true;
}
