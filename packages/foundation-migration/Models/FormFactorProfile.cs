using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sunfish.Foundation.Versioning;

namespace Sunfish.Foundation.Migration;

/// <summary>
/// Snapshot of the host's hardware-tier characteristics per ADR 0028-A5.1.
/// Inputs to the <c>DerivedSurface</c> filter (which workspace data is
/// active vs. sequestered) and the migration-table lookup (what
/// transitions when this profile changes).
/// </summary>
/// <remarks>
/// Shape is canonical-JSON-stable — camelCase property names,
/// <see cref="JsonStringEnumConverter"/> on every enum so values
/// round-trip as their string names rather than numeric ordinals.
/// Mirrors the <see cref="VersionVector"/> wire-format pattern from
/// W#34 per A7.8.
/// </remarks>
public sealed record FormFactorProfile
{
    [JsonPropertyName("formFactor")]
    [JsonConverter(typeof(JsonStringEnumConverter<FormFactorKind>))]
    public required FormFactorKind FormFactor { get; init; }

    /// <summary>
    /// Persisted as a JSON array of strings (set-shape stability is the
    /// caller's contract; the wire form is order-insensitive but the
    /// canonical encoder may sort for determinism).
    /// </summary>
    [JsonPropertyName("inputModalities")]
    public required HashSet<InputModalityKind> InputModalities { get; init; }

    [JsonPropertyName("displayClass")]
    [JsonConverter(typeof(JsonStringEnumConverter<DisplayClassKind>))]
    public required DisplayClassKind DisplayClass { get; init; }

    [JsonPropertyName("networkPosture")]
    [JsonConverter(typeof(JsonStringEnumConverter<NetworkPostureKind>))]
    public required NetworkPostureKind NetworkPosture { get; init; }

    [JsonPropertyName("storageBudgetMb")]
    public required uint StorageBudgetMb { get; init; }

    [JsonPropertyName("powerProfile")]
    [JsonConverter(typeof(JsonStringEnumConverter<PowerProfileKind>))]
    public required PowerProfileKind PowerProfile { get; init; }

    /// <summary>Persisted as a JSON array of strings; see <see cref="InputModalities"/>.</summary>
    [JsonPropertyName("sensorSurface")]
    public required HashSet<SensorKind> SensorSurface { get; init; }

    /// <summary>Reuses the W#34-shipped enum per A7.6.</summary>
    [JsonPropertyName("instanceClass")]
    [JsonConverter(typeof(JsonStringEnumConverter<InstanceClassKind>))]
    public required InstanceClassKind InstanceClass { get; init; }
}
