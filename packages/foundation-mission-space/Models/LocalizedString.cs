using System.Text.Json.Serialization;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Localizable string per ADR 0062-A1.11. <see cref="Key"/> is a stable
/// identifier resolvable through the host's localization pipeline;
/// <see cref="DefaultValue"/> is the en-US fallback.
/// </summary>
public sealed record LocalizedString
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("defaultValue")]
    public required string DefaultValue { get; init; }

    public override string ToString() => DefaultValue;
}
