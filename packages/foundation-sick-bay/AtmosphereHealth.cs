using System.Text.Json.Serialization;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Aggregate atmospheric health discriminator for the Sick Bay
/// Atmosphere tab per ADR 0082 §1. The Atmosphere tab summarizes overall
/// probe-result health; individual probe details surface in the Lab tab.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AtmosphereHealth
{
    /// <summary>All probes reporting healthy.</summary>
    Green,

    /// <summary>One or more probes warning; no critical states.</summary>
    Yellow,

    /// <summary>Multiple warnings or one critical probe.</summary>
    Orange,

    /// <summary>Multiple critical probes; immediate intervention required.</summary>
    Red,
}
