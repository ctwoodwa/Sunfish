using System;

namespace Sunfish.Foundation.Transport.Relay;

/// <summary>
/// Configuration for <see cref="BridgeRelayPeerTransport"/>. The Tier-3
/// fallback always tries to resolve when registered, so the relay URL
/// is required at construction time.
/// </summary>
public sealed class BridgeRelayOptions
{
    /// <summary>
    /// The Bridge relay URL (HTTPS or WSS). Example:
    /// <c>https://relay.bridge.example.com/sync</c> or
    /// <c>wss://relay.bridge.example.com/sync</c>. The transport
    /// resolves to the host:port pair extracted from this URL.
    /// </summary>
    public required Uri RelayUrl { get; init; }
}
