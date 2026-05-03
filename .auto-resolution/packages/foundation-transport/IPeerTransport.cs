using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;

namespace Sunfish.Foundation.Transport;

/// <summary>
/// A single transport-tier strategy: mDNS link-local (Tier 1), mesh-VPN
/// (Tier 2; one implementation per <c>providers-mesh-*</c> adapter), or
/// Bridge-hosted managed relay (Tier 3). Implementations are stateless
/// w.r.t. peers — selection / caching is the
/// <see cref="ITransportSelector"/>'s responsibility per ADR 0061 A4.
/// </summary>
public interface IPeerTransport
{
    /// <summary>The tier this transport occupies in the failover stack.</summary>
    TransportTier Tier { get; }

    /// <summary>
    /// Whether this transport is currently usable. A Tier-2 mesh adapter
    /// returns <c>false</c> when the control plane is unreachable; the
    /// selector skips it and falls through to the next tier.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Resolves <paramref name="peer"/>'s current endpoint, or null if the
    /// transport cannot reach the peer. Implementations MUST honor
    /// <paramref name="ct"/>; the selector cancels with the per-tier
    /// connect budget (T1: 2s, T2: 5s, T3: 10s) per ADR 0061 A4.
    /// </summary>
    Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct);

    /// <summary>
    /// Opens a connected duplex stream to <paramref name="peer"/>. The
    /// caller owns the returned <see cref="IDuplexStream"/> and MUST
    /// dispose it. Implementations MUST honor <paramref name="ct"/>; the
    /// selector cancels with the per-handshake budget (2s) per A4.
    /// </summary>
    Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct);
}
