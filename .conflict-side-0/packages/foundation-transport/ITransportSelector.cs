using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;

namespace Sunfish.Foundation.Transport;

/// <summary>
/// Picks the best <see cref="IPeerTransport"/> for a given peer per the
/// ADR 0061 §"Decision" failover order: Tier 1 (mDNS) → Tier 2 (mesh-VPN
/// adapters, in config-priority + lexicographic order) → Tier 3 (Bridge
/// relay). Implementations MUST honor the per-tier connect budgets + per-
/// handshake budget (A4) and SHOULD cache the per-peer winner with a
/// short TTL (~30s; see <c>DefaultTransportSelector</c> in Phase 2).
/// </summary>
public interface ITransportSelector
{
    /// <summary>
    /// Returns the transport that successfully resolved
    /// <paramref name="peer"/>. Falls through to Tier 3 when no lower
    /// tier resolves.
    /// </summary>
    Task<IPeerTransport> SelectAsync(PeerId peer, CancellationToken ct);
}
