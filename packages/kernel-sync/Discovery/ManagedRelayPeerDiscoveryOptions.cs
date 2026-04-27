namespace Sunfish.Kernel.Sync.Discovery;

/// <summary>
/// Configuration for <see cref="ManagedRelayPeerDiscovery"/>. Phase 1 G4
/// (paper §17.2 + ADR 0031) — Anchors that need to reach peers across the
/// WAN dial a managed Bridge relay; this options object carries the relay's
/// endpoint, identity, and trust hints.
/// </summary>
/// <remarks>
/// <para>
/// The relay is a Zone-C SaaS shell run by Bridge — it forwards CBOR
/// envelopes between Anchor peers but never decrypts inner payload bytes
/// (payload is end-to-end encrypted via per-team role keys derived in
/// <c>kernel-security/Keys/</c>). The discovery layer only needs the
/// transport endpoint plus the public key the daemon can verify against
/// during the HELLO handshake.
/// </para>
/// <para>
/// <b>Phase 1 single-relay convention:</b> exactly one Bridge relay per
/// Anchor install. The user provisions <see cref="RelayUrl"/> +
/// <see cref="RelayPublicKey"/> via Anchor's settings UI (G4 follow-up)
/// or via initial onboarding QR (paper §13.4). Multi-relay round-robin /
/// failover is a Phase 2 follow-up.
/// </para>
/// <para>
/// <b>Trust model:</b> the Bridge's <c>RelayPublicKey</c> ships with the
/// onboarding payload or is fetched out-of-band before the first round.
/// The gossip daemon's HELLO ladder verifies the relay signs HELLO with
/// this key; a mismatch fails the round (per existing handshake code in
/// <c>HandshakeProtocol</c>). Discovery does not perform crypto — it just
/// surfaces the configured relay.
/// </para>
/// </remarks>
public sealed class ManagedRelayPeerDiscoveryOptions
{
    /// <summary>
    /// WebSocket URL the gossip daemon will dial. Empty string disables
    /// managed-relay discovery — useful when Anchor is fully LAN-bound
    /// and only mDNS discovery is wanted. Example:
    /// <c>wss://relay.bridge.example.com/sync</c>.
    /// </summary>
    public string RelayUrl { get; set; } = string.Empty;

    /// <summary>
    /// Hex-encoded 16-byte node id the relay advertises in its HELLO. Must
    /// match the node id the relay's own keystore produces from its root
    /// Ed25519 public key. Empty string when <see cref="RelayUrl"/> is empty.
    /// </summary>
    public string RelayNodeId { get; set; } = string.Empty;

    /// <summary>
    /// 32-byte raw Ed25519 public key the relay signs HELLO with. Sourced
    /// from the onboarding payload (paper §13.4) or out-of-band Bridge
    /// admin config. Empty array when <see cref="RelayUrl"/> is empty.
    /// </summary>
    public byte[] RelayPublicKey { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Sync-daemon-protocol schema version the relay supports. Default
    /// matches <c>HandshakeProtocol.DefaultSchemaVersion</c>; override only
    /// when pinning a specific Bridge build.
    /// </summary>
    public string RelaySchemaVersion { get; set; } = "1.0";
}
