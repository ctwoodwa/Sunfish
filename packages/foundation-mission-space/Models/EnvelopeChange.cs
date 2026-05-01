using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>Diff between two consecutive <see cref="MissionEnvelope"/> snapshots per A1.2.</summary>
public sealed record EnvelopeChange
{
    [JsonPropertyName("previous")]
    public MissionEnvelope? Previous { get; init; }

    [JsonPropertyName("current")]
    public required MissionEnvelope Current { get; init; }

    [JsonPropertyName("changedDimensions")]
    public required IReadOnlyList<DimensionChangeKind> ChangedDimensions { get; init; }

    [JsonPropertyName("severity")]
    [JsonConverter(typeof(JsonStringEnumConverter<EnvelopeChangeSeverity>))]
    public required EnvelopeChangeSeverity Severity { get; init; }
}
