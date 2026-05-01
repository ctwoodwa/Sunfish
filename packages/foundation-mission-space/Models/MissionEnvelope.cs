using System;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// 10-dimension snapshot of the host's runtime mission space per ADR
/// 0062-A1.2. <see cref="EnvelopeHash"/> is the SHA-256 hex of the
/// canonical-JSON of every other field.
/// </summary>
public sealed record MissionEnvelope
{
    [JsonPropertyName("hardware")]
    public required HardwareCapabilities Hardware { get; init; }

    [JsonPropertyName("user")]
    public required UserCapabilities User { get; init; }

    [JsonPropertyName("regulatory")]
    public required RegulatoryCapabilities Regulatory { get; init; }

    [JsonPropertyName("runtime")]
    public required RuntimeCapabilities Runtime { get; init; }

    [JsonPropertyName("formFactor")]
    public required FormFactorSnapshot FormFactor { get; init; }

    [JsonPropertyName("edition")]
    public required EditionCapabilities Edition { get; init; }

    [JsonPropertyName("network")]
    public required NetworkCapabilities Network { get; init; }

    [JsonPropertyName("trustAnchor")]
    public required TrustAnchorCapabilities TrustAnchor { get; init; }

    [JsonPropertyName("syncState")]
    public required SyncStateSnapshot SyncState { get; init; }

    [JsonPropertyName("versionVector")]
    public required VersionVectorSnapshot VersionVector { get; init; }

    [JsonPropertyName("snapshotAt")]
    public required DateTimeOffset SnapshotAt { get; init; }

    [JsonPropertyName("envelopeHash")]
    public string EnvelopeHash { get; init; } = string.Empty;

    /// <summary>Returns a new envelope with <see cref="EnvelopeHash"/> recomputed.</summary>
    public MissionEnvelope WithComputedHash()
    {
        var withoutHash = this with { EnvelopeHash = string.Empty };
        var bytes = CanonicalJson.Serialize(withoutHash);
        var hash = SHA256.HashData(bytes);
        return this with { EnvelopeHash = Convert.ToHexString(hash).ToLowerInvariant() };
    }

    /// <summary>Computes the hash without mutating (returns hex).</summary>
    public static string ComputeEnvelopeHash(MissionEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return envelope.WithComputedHash().EnvelopeHash;
    }
}
