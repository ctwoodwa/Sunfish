namespace Sunfish.Bridge;

/// <summary>
/// Top-level Bridge configuration bound from the <c>Bridge</c> configuration
/// section. Drives the install-time posture selection per
/// <see href="../../docs/adrs/0026-bridge-posture.md">ADR 0026</see>.
/// </summary>
public sealed class BridgeOptions
{
    /// <summary>Configuration-section name (<c>"Bridge"</c>).</summary>
    public const string SectionName = "Bridge";

    /// <summary>
    /// Which posture to compose at startup. Defaults to <see cref="BridgeMode.SaaS"/>
    /// so existing deployments keep working without a config change.
    /// </summary>
    public BridgeMode Mode { get; set; } = BridgeMode.SaaS;

    /// <summary>Relay-posture knobs. Only consulted when <see cref="Mode"/> is <see cref="BridgeMode.Relay"/>.</summary>
    public RelayOptions Relay { get; set; } = new();
}

/// <summary>
/// Managed-relay configuration (paper §6.1 tier-3, §17.2). Bridge in relay
/// mode accepts inbound sync-daemon connections, authenticates peers via
/// the <c>HandshakeProtocol</c>, and fan-outs <c>DELTA_STREAM</c> and
/// <c>GOSSIP_PING</c> frames to co-tenant peers. The relay is stateless
/// beyond the current connection set — if it crashes, peers reconnect.
/// </summary>
public sealed class RelayOptions
{
    /// <summary>
    /// Transport endpoint the relay listens on. Null falls back to a
    /// platform-appropriate default chosen by <c>ISyncDaemonTransport</c>.
    /// </summary>
    public string? ListenEndpoint { get; set; }

    /// <summary>
    /// Maximum simultaneously connected peers. New connections past this
    /// limit are refused with <c>ErrorCode.RateLimitExceeded</c> and
    /// <c>Recoverable: false</c>. Default <c>500</c> — sized to the paper
    /// §17.2 per-relay cost model (small-team fan-out; heavier deployments
    /// run multiple relay instances behind a discovery layer).
    /// </summary>
    public int MaxConnectedNodes { get; set; } = 500;

    /// <summary>Hostname advertised to peers via discovery. Optional — off by default.</summary>
    public string? AdvertiseHostname { get; set; }

    /// <summary>
    /// If non-empty, only peers whose agreed team-id is in this list are
    /// accepted. Empty array ("accept all") is the default and matches the
    /// open-relay operational shape described in paper §6.1.
    /// </summary>
    public string[] AllowedTeamIds { get; set; } = Array.Empty<string>();
}
