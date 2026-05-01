namespace Sunfish.Foundation.Transport.Mdns;

/// <summary>
/// Configuration for <see cref="MdnsPeerTransport"/>.
/// </summary>
public sealed class MdnsPeerTransportOptions
{
    /// <summary>
    /// The mDNS service type to advertise + browse. Default
    /// <c>_sunfish._tcp.local</c> per ADR 0061 §"Decision". May be
    /// scoped (e.g., <c>_sunfish-staging._tcp.local</c>) when running
    /// multiple cohorts on the same physical LAN.
    /// </summary>
    public string ServiceType { get; init; } = "_sunfish._tcp.local";

    /// <summary>
    /// Time after which an un-refreshed peer entry is evicted from the
    /// cache. Default 60s.
    /// </summary>
    public int PeerCacheTtlSeconds { get; init; } = 60;

    /// <summary>
    /// Sweep period for the eviction loop. Default 15s. The actual
    /// eviction sweep also re-issues a service-instance query to keep
    /// short-lived peers visible.
    /// </summary>
    public int SweepIntervalSeconds { get; init; } = 15;
}
