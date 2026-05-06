using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Severity classification for a <see cref="TacticalAlert"/> per
/// ADR 0081 §1. Distinct from
/// <c>Sunfish.Foundation.Quarterdeck.AlertSeverity</c> — the
/// Quarterdeck severity drives ticker presentation; this severity
/// drives the Tactical rule-engine's severity-threshold gating
/// (<see cref="ThreatTriggerTemplate.MinimumSeverity"/>) and
/// the Sonar/Lookout disclosure split (high-priority alerts surface
/// to <see cref="ILookout"/>; informational alerts surface to
/// <see cref="ISonarStore"/>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertSeverity
{
    /// <summary>Top-priority anomaly demanding immediate action.</summary>
    Critical = 0,

    /// <summary>Materially elevated anomaly.</summary>
    High = 1,

    /// <summary>Standard operational anomaly.</summary>
    Medium = 2,

    /// <summary>Low-priority anomaly; surfaced to Sonar but not Lookout.</summary>
    Low = 3,

    /// <summary>Informational signal; record-only.</summary>
    Informational = 4,
}
