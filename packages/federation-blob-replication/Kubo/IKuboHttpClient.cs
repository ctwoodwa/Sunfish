namespace Sunfish.Federation.BlobReplication.Kubo;

/// <summary>
/// Thin client over the Kubo (go-ipfs) HTTP RPC API. Only the subset of endpoints needed by
/// <see cref="IpfsBlobStore"/> and <see cref="KuboHealthProbe"/> is exposed here — this is not a
/// general-purpose Kubo SDK.
/// </summary>
/// <remarks>
/// Kubo's HTTP RPC is POST-only for every endpoint, even read-like operations. All methods issue
/// POST requests; <see cref="CatAsync"/> uses the <c>arg</c> query parameter to pass a CID.
/// The <c>/api/v0/add</c> endpoint uses a multipart form upload — see
/// <see cref="AddAsync"/>.
/// </remarks>
public interface IKuboHttpClient
{
    /// <summary>Adds a byte payload and returns the resulting CID response. Uses
    /// <c>?cid-version=1&amp;raw-leaves=true&amp;pin={pin}</c> to match Sunfish's CID format.</summary>
    Task<KuboAddResponse> AddAsync(ReadOnlyMemory<byte> content, bool pin, CancellationToken ct);

    /// <summary>Fetches the raw bytes for the given CID. Returns <see langword="null"/> when Kubo
    /// reports the content is not locally available (typically a 500-level response with a
    /// "not found" error message, which Kubo does not model as 404).</summary>
    Task<byte[]?> CatAsync(string cid, CancellationToken ct);

    /// <summary>Pins a CID so that Kubo retains it across garbage-collection runs.</summary>
    Task<KuboPinResponse> PinAddAsync(string cid, CancellationToken ct);

    /// <summary>Removes a pin from a CID.</summary>
    Task<KuboPinResponse> PinRmAsync(string cid, CancellationToken ct);

    /// <summary>Lists currently pinned CIDs. Used by <see cref="IpfsBlobStore.ExistsLocallyAsync"/>
    /// to report whether a given CID is locally retained.</summary>
    Task<KuboPinListResponse> PinListAsync(string? cid, CancellationToken ct);

    /// <summary>Returns daemon identity, agent version, and dialable multiaddrs.</summary>
    Task<KuboIdResponse> IdAsync(CancellationToken ct);

    /// <summary>Returns the Kubo daemon configuration. Inspected for the <c>Swarm.SwarmKey</c>
    /// field to infer whether the daemon is running in private-network mode.</summary>
    Task<KuboConfigResponse> GetConfigAsync(CancellationToken ct);

    /// <summary>Lists currently connected swarm peers.</summary>
    Task<KuboSwarmPeersResponse> SwarmPeersAsync(CancellationToken ct);
}
