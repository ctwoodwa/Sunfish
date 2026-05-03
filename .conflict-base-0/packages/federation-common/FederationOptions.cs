namespace Sunfish.Federation.Common;

/// <summary>
/// Configuration for the local federation node. Bound from configuration via <c>AddSunfishFederation</c>.
/// </summary>
public sealed class FederationOptions
{
    /// <summary>
    /// Environment tag — governs the strictness of startup checks. Production requires a swarm key
    /// and (eventually) a private-network Kubo daemon.
    /// </summary>
    public FederationEnvironment Environment { get; set; } = FederationEnvironment.Development;

    /// <summary>
    /// Path to the IPFS swarm key file (32-byte hex-encoded key). Required in
    /// <see cref="FederationEnvironment.Production"/>.
    /// </summary>
    public string? SwarmKeyPath { get; set; }

    /// <summary>Endpoint of the Kubo daemon RPC API.</summary>
    public Uri? KuboRpcAddress { get; set; }

    /// <summary>Optional region label to stamp on outbound peer descriptors.</summary>
    public string? LocalPeerRegion { get; set; }
}

/// <summary>
/// Deployment environment for the federation node. Drives startup check severity.
/// </summary>
public enum FederationEnvironment
{
    /// <summary>Development — all private-network enforcement is skipped with a warning.</summary>
    Development,

    /// <summary>Staging — warnings are logged but checks do not fail startup.</summary>
    Staging,

    /// <summary>Production — private-network posture is mandatory; violations throw at startup.</summary>
    Production,
}
