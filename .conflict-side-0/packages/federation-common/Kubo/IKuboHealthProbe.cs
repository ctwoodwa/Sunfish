namespace Sunfish.Federation.Common.Kubo;

/// <summary>
/// Probes a Kubo (IPFS) sidecar daemon for its operational state. Implemented by the blob-replication
/// package (Task D-5 / <c>IpfsBlobStore</c>) and consumed here by <c>FederationStartupChecks</c> to
/// enforce private-network posture in production environments.
/// </summary>
/// <remarks>
/// This interface is published here — not inside the blob-replication package — so that federation
/// startup can depend on it without taking a hard dependency on the Kubo implementation. Federation
/// startup resolves this dependency optionally; when no blob-replication package is wired, startup
/// logs a warning and continues (see <c>FederationStartupChecks</c>).
/// </remarks>
public interface IKuboHealthProbe
{
    /// <summary>
    /// Returns the current network configuration of the Kubo daemon. Production deployments must
    /// report <c>NetworkProfile == "private"</c>.
    /// </summary>
    ValueTask<KuboNetworkInfo> GetConfigAsync(CancellationToken ct);
}

/// <summary>
/// Snapshot of the relevant Kubo daemon configuration for startup checks.
/// </summary>
/// <param name="NetworkProfile">Either <c>"private"</c> (with swarm key) or <c>"public"</c>.</param>
/// <param name="Version">Reported daemon version string (diagnostics only).</param>
public sealed record KuboNetworkInfo(string NetworkProfile, string Version);
