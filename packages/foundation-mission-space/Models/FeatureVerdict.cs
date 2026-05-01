using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>The verdict an <see cref="IFeatureGate{TFeature}"/> renders per A1.2.</summary>
public sealed record FeatureVerdict
{
    [JsonPropertyName("featureKey")]
    public required string FeatureKey { get; init; }

    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter<FeatureAvailabilityState>))]
    public required FeatureAvailabilityState State { get; init; }

    [JsonPropertyName("degradationKind")]
    [JsonConverter(typeof(JsonStringEnumConverter<DegradationKind>))]
    public DegradationKind? DegradationKind { get; init; }

    [JsonPropertyName("reason")]
    public LocalizedString? Reason { get; init; }

    [JsonPropertyName("contributingDimensions")]
    public IReadOnlyList<DimensionChangeKind> ContributingDimensions { get; init; } = Array.Empty<DimensionChangeKind>();
}
