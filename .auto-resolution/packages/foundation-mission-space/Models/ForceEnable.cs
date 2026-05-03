using System;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>Force-enable record per A1.2 + A1.9.</summary>
public sealed record ForceEnableRecord
{
    [JsonPropertyName("featureKey")]
    public required string FeatureKey { get; init; }

    [JsonPropertyName("dimension")]
    [JsonConverter(typeof(JsonStringEnumConverter<DimensionChangeKind>))]
    public required DimensionChangeKind Dimension { get; init; }

    [JsonPropertyName("operatorPrincipalId")]
    public required string OperatorPrincipalId { get; init; }

    [JsonPropertyName("requestedAt")]
    public required DateTimeOffset RequestedAt { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>Per A1.2 — request shape for <see cref="IFeatureForceEnableSurface.RequestAsync"/>.</summary>
public sealed record FeatureForceEnableRequest
{
    [JsonPropertyName("featureKey")]
    public required string FeatureKey { get; init; }

    [JsonPropertyName("dimension")]
    [JsonConverter(typeof(JsonStringEnumConverter<DimensionChangeKind>))]
    public required DimensionChangeKind Dimension { get; init; }

    [JsonPropertyName("operatorPrincipalId")]
    public required string OperatorPrincipalId { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>Thrown when force-enable is requested for a NotOverridable dimension per A1.9.</summary>
public sealed class ForceEnableNotPermittedException : Exception
{
    public DimensionChangeKind Dimension { get; }

    public ForceEnableNotPermittedException(DimensionChangeKind dimension)
        : base($"Dimension {dimension} is NotOverridable per ADR 0062-A1.9; force-enable is rejected.")
    {
        Dimension = dimension;
    }
}
