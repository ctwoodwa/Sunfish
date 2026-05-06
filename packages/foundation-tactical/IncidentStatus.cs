using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Lifecycle state for an <see cref="IncidentRecord"/> per ADR 0081 §1.
/// Allowed transitions: <see cref="Open"/> → <see cref="Resolved"/>
/// (direct close); <see cref="Open"/> → <see cref="Investigating"/> →
/// <see cref="Resolved"/> (reserved-future investigation transition).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IncidentStatus
{
    /// <summary>Newly opened incident.</summary>
    Open,

    /// <summary>Incident under active investigation (reserved for v2 — Phase 1 ships the surface but no v1 path drives this transition).</summary>
    Investigating,

    /// <summary>Incident closed with a resolution note.</summary>
    Resolved,
}
