using System;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Migration;

/// <summary>
/// A re-profile event per ADR 0028-A5.3 — fired when the host's
/// hardware-tier profile changes (storage budget, network posture,
/// sensor permission, power profile, adapter upgrade/downgrade, or
/// manual operator reprofile). Drives the migration-table lookup +
/// sequestration transitions per A5.4 + A8.3.
/// </summary>
public sealed record HardwareTierChangeEvent
{
    [JsonPropertyName("nodeId")]
    public required string NodeId { get; init; }

    [JsonPropertyName("previousProfile")]
    public required FormFactorProfile PreviousProfile { get; init; }

    [JsonPropertyName("currentProfile")]
    public required FormFactorProfile CurrentProfile { get; init; }

    [JsonPropertyName("triggeringEvent")]
    [JsonConverter(typeof(JsonStringEnumConverter<TriggeringEventKind>))]
    public required TriggeringEventKind TriggeringEvent { get; init; }

    [JsonPropertyName("detectedAt")]
    public required DateTimeOffset DetectedAt { get; init; }
}
