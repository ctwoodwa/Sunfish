using System.Text.Json.Serialization;

namespace Sunfish.UICore.Conformance;

/// <summary>
/// WCAG 2.2 conformance levels per W3C definition + ADR 0077 §7.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Wcag22Level
{
    /// <summary>Level A — minimum.</summary>
    A,

    /// <summary>Level AA — the Sunfish baseline target.</summary>
    AA,

    /// <summary>Level AAA — enhanced; not all surfaces meet AAA.</summary>
    AAA,
}
