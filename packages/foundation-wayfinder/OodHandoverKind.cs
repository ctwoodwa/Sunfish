using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Distinguishes a routine watch-change from an authority-ordered relief.
/// Per ADR 0078 §1 + W#49 P2 amendment R3 (XO post-merge council).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OodHandoverKind
{
    /// <summary>
    /// Outgoing watch-keeper voluntarily transfers; both parties are present.
    /// Audit severity is <c>"Normal"</c>.
    /// </summary>
    Voluntary,

    /// <summary>
    /// Commanding authority relieves the watch-keeper (incapacitation, emergency,
    /// disciplinary). Audit severity is <c>"High"</c>.
    /// </summary>
    CommandRelieved,
}
