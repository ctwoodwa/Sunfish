namespace Sunfish.Federation.BlobReplication;

/// <summary>
/// Options for <see cref="IpfsBlobStore"/> and its Kubo HTTP RPC client.
/// </summary>
public sealed class KuboBlobStoreOptions
{
    /// <summary>
    /// The HTTP RPC endpoint of the Kubo daemon (typically <c>http://localhost:5001/</c>). The path
    /// root is expected to be the daemon root — the client appends <c>api/v0/*</c> suffixes.
    /// </summary>
    public Uri RpcEndpoint { get; set; } = new("http://localhost:5001/");

    /// <summary>
    /// Optional path to the IPFS swarm key on disk. Not used directly by the HTTP client — this is
    /// recorded so that operators can align the blob-store configuration with
    /// <c>FederationOptions.SwarmKeyPath</c> in consolidated configuration sources.
    /// </summary>
    public string? SwarmKeyPath { get; set; }

    /// <summary>
    /// When <see langword="true"/> (the default), <see cref="IpfsBlobStore.PutAsync"/> instructs
    /// Kubo to pin the added content so it survives garbage collection without an extra pin call.
    /// </summary>
    public bool PinOnPut { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/> (the default), the blob store refuses to start up unless the
    /// Kubo daemon reports a private-network profile. Intended for production federation nodes.
    /// The current implementation enforces this check at the <c>FederationStartupChecks</c> layer;
    /// this flag is wired through here so it can drive that policy from a single options source.
    /// </summary>
    public bool RequirePrivateNetwork { get; set; } = true;
}
