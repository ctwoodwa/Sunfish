using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Severity classification for a <see cref="QuarterdeckAlert"/> per
/// ADR 0080 §2.3. Enum order is the canonical sort priority — lower
/// ordinal sorts first when the data provider orders the
/// <c>PendingAlerts</c> list.
/// </summary>
/// <remarks>
/// <b>Live-region politeness mapping</b> (consumed by Phase 3a alert
/// ticker against <c>Sunfish.UICore.Primitives.LiveRegionPoliteness</c>):
/// <list type="bullet">
/// <item><description><see cref="Emergency"/> →
/// <c>LiveRegionPoliteness.Critical</c> (security/destructive-action
/// politeness per ADR 0077 §4 council F-1)</description></item>
/// <item><description><see cref="High"/> →
/// <c>LiveRegionPoliteness.Assertive</c></description></item>
/// <item><description><see cref="Normal"/> →
/// <c>LiveRegionPoliteness.Polite</c></description></item>
/// <item><description><see cref="Informational"/> →
/// <c>LiveRegionPoliteness.Polite</c></description></item>
/// </list>
/// Phase 3a renderers MUST use exactly this mapping; deviations require
/// an ADR 0080 §A1 amendment.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertSeverity
{
    /// <summary>
    /// Top-priority alert demanding immediate operator attention.
    /// Renders with <c>LiveRegionPoliteness.Critical</c> politeness;
    /// defaults to requiring acknowledgement.
    /// </summary>
    Emergency = 0,

    /// <summary>
    /// Materially elevated alert (e.g., quorum-breach pending, watch
    /// expiry imminent). Renders with
    /// <c>LiveRegionPoliteness.Assertive</c> politeness; may or may
    /// not require acknowledgement.
    /// </summary>
    High = 1,

    /// <summary>
    /// Standard operational alert. Renders with
    /// <c>LiveRegionPoliteness.Polite</c> politeness; ticker rotation.
    /// </summary>
    Normal = 2,

    /// <summary>
    /// Background informational alert (e.g., scheduled maintenance
    /// window opens in 4 hours). Renders with
    /// <c>LiveRegionPoliteness.Polite</c> politeness; rotates last in
    /// the ticker.
    /// </summary>
    Informational = 3,
}
