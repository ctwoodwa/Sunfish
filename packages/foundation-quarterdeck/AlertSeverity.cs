using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Severity classification for a <see cref="QuarterdeckAlert"/> per
/// ADR 0080 §2.3. Enum order is the canonical sort priority — lower
/// ordinal sorts first when the data provider orders the
/// <c>PendingAlerts</c> list.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertSeverity
{
    /// <summary>
    /// Top-priority alert demanding immediate operator attention.
    /// Renders with the assertive live-region politeness; defaults to
    /// requiring acknowledgement.
    /// </summary>
    Emergency = 0,

    /// <summary>
    /// Materially elevated alert (e.g., quorum-breach pending, watch
    /// expiry imminent). Renders with assertive politeness; may or may
    /// not require acknowledgement.
    /// </summary>
    High = 1,

    /// <summary>
    /// Standard operational alert. Polite politeness; ticker rotation.
    /// </summary>
    Normal = 2,

    /// <summary>
    /// Background informational alert (e.g., scheduled maintenance
    /// window opens in 4 hours). Polite politeness; rotates last in
    /// the ticker.
    /// </summary>
    Informational = 3,
}
