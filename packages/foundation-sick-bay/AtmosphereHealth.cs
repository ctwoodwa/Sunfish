using System.Text.Json.Serialization;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Aggregate atmospheric health discriminator for the Sick Bay
/// Atmosphere tab per ADR 0082 §1. The Atmosphere tab summarizes overall
/// probe-result health; individual probe details surface in the Lab tab.
/// </summary>
/// <remarks>
/// <para><see cref="Unknown"/> is the zero-value sentinel (ADR 0082-A1). UI MUST render
/// a neutral pending state (e.g., spinner) when this value is observed — never Green.</para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AtmosphereHealth
{
    /// <summary>
    /// Provider has not yet projected real probe data (ADR 0082-A1). Returned by stub
    /// implementations before Mission Envelope integration is wired. UI MUST render a
    /// neutral pending state, not Green. <c>default(AtmosphereHealth)</c> yields this value.
    /// </summary>
    Unknown,

    /// <summary>All probes reporting healthy.</summary>
    Green,

    /// <summary>One or more probes warning; no critical states.</summary>
    Yellow,

    /// <summary>Multiple warnings or one critical probe.</summary>
    Orange,

    /// <summary>Multiple critical probes; immediate intervention required.</summary>
    Red,
}
