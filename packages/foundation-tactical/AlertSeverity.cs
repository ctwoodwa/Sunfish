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
/// <remarks>
/// <b>Live-region politeness mapping</b> (consumed by Phase 3a Lookout
/// renderer against <c>Sunfish.UICore.Primitives.LiveRegionPoliteness</c>):
/// <list type="bullet">
/// <item><description><see cref="Critical"/> →
/// <c>LiveRegionPoliteness.Critical</c> (security/destructive-action
/// politeness per ADR 0077 §4 council F-1)</description></item>
/// <item><description><see cref="High"/> →
/// <c>LiveRegionPoliteness.Assertive</c></description></item>
/// <item><description><see cref="Medium"/> →
/// <c>LiveRegionPoliteness.Polite</c></description></item>
/// <item><description><see cref="Low"/> →
/// <c>LiveRegionPoliteness.Polite</c></description></item>
/// <item><description><see cref="Informational"/> →
/// <c>LiveRegionPoliteness.Polite</c> (ticker rotation last)</description></item>
/// </list>
/// Phase 3a renderers MUST use exactly this mapping; deviations require
/// an ADR 0081 §A1 amendment.
/// </remarks>
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
