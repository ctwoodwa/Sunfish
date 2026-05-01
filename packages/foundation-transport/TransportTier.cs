namespace Sunfish.Foundation.Transport;

/// <summary>
/// The three-tier peer-transport priority discriminator (ADR 0061 §"Decision").
/// Selectors prefer lower tiers; failures fall through to higher tiers.
/// </summary>
public enum TransportTier
{
    /// <summary>Tier 1 — mDNS / link-local discovery (same physical LAN; ~2s connect budget).</summary>
    LocalNetwork,

    /// <summary>Tier 2 — WireGuard-backed mesh VPN (control plane via Headscale / Tailscale / NetBird; ~5s connect budget).</summary>
    MeshVpn,

    /// <summary>Tier 3 — Bridge-hosted HTTPS managed relay (last-resort, ciphertext-only; ~10s connect budget).</summary>
    ManagedRelay,
}
