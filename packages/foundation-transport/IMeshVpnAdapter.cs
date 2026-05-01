using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Transport;

/// <summary>
/// A Tier-2 mesh-VPN transport — one per <c>providers-mesh-*</c> adapter
/// (Headscale, Tailscale, NetBird, etc.). Extends <see cref="IPeerTransport"/>
/// with the control-plane lifecycle calls the host needs to register the
/// local node and observe mesh state.
/// </summary>
/// <remarks>
/// Per ADR 0013 (provider neutrality), no Sunfish-tier code may import
/// adapter-specific types. Adapters live in <c>packages/providers-mesh-*</c>;
/// hosts that want a specific mesh wire it through this interface.
/// </remarks>
public interface IMeshVpnAdapter : IPeerTransport
{
    /// <summary>
    /// Stable adapter discriminator (e.g., <c>"headscale"</c>,
    /// <c>"tailscale"</c>). The selector uses this for lexicographic
    /// tie-break when multiple Tier-2 adapters are registered (ADR 0061 A4).
    /// </summary>
    string AdapterName { get; }

    /// <summary>Returns the current control-plane snapshot for diagnostics.</summary>
    Task<MeshNodeStatus> GetMeshStatusAsync(CancellationToken ct);

    /// <summary>
    /// Registers the local Sunfish peer with the mesh control plane.
    /// Idempotent: repeated calls with the same
    /// <see cref="MeshDeviceRegistration.Peer"/> SHOULD update tags +
    /// device-name without producing duplicate device records.
    /// </summary>
    Task RegisterDeviceAsync(MeshDeviceRegistration registration, CancellationToken ct);
}
