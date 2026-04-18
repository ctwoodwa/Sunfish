using System.Text.Json.Serialization;

namespace Sunfish.Federation.BlobReplication.Kubo;

/// <summary>
/// Response from <c>POST /api/v0/add</c>. Kubo returns one JSON object per added file
/// (potentially streamed as NDJSON if directories are added).
/// </summary>
/// <param name="Name">The name Kubo assigned to the entry (typically the form-data filename).</param>
/// <param name="Hash">The content identifier — a CID v1 string when the request includes
/// <c>cid-version=1</c>.</param>
/// <param name="Size">Kubo-reported payload size in bytes, encoded as a string.</param>
public sealed record KuboAddResponse(
    [property: JsonPropertyName("Name")] string Name,
    [property: JsonPropertyName("Hash")] string Hash,
    [property: JsonPropertyName("Size")] string Size);

/// <summary>Response from <c>POST /api/v0/id</c>.</summary>
public sealed record KuboIdResponse(
    [property: JsonPropertyName("ID")] string ID,
    [property: JsonPropertyName("AgentVersion")] string AgentVersion,
    [property: JsonPropertyName("Addresses")] string[] Addresses);

/// <summary>Response from <c>POST /api/v0/config/show</c>.</summary>
public sealed record KuboConfigResponse(
    [property: JsonPropertyName("Swarm")] SwarmConfig Swarm);

/// <summary>Swarm section of the Kubo config — the <see cref="SwarmKey"/> field is the only part
/// Sunfish inspects to determine whether the daemon is in private-network mode.</summary>
public sealed record SwarmConfig(
    [property: JsonPropertyName("SwarmKey")] string? SwarmKey);

/// <summary>Response from <c>POST /api/v0/pin/add</c> and <c>POST /api/v0/pin/rm</c>.</summary>
public sealed record KuboPinResponse(
    [property: JsonPropertyName("Pins")] string[] Pins);

/// <summary>Response from <c>POST /api/v0/pin/ls</c>.</summary>
public sealed record KuboPinListResponse(
    [property: JsonPropertyName("Keys")] Dictionary<string, KuboPinListEntry> Keys);

/// <summary>Pin-list entry. <see cref="Type"/> is typically <c>"recursive"</c>, <c>"direct"</c>,
/// or <c>"indirect"</c>.</summary>
public sealed record KuboPinListEntry(
    [property: JsonPropertyName("Type")] string Type);

/// <summary>Response from <c>POST /api/v0/swarm/peers</c>.</summary>
public sealed record KuboSwarmPeersResponse(
    [property: JsonPropertyName("Peers")] KuboPeer[] Peers);

/// <summary>A single connected swarm peer.</summary>
public sealed record KuboPeer(
    [property: JsonPropertyName("Addr")] string Addr,
    [property: JsonPropertyName("Peer")] string Peer);
