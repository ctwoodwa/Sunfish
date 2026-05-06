using System.Text.Json.Serialization;

namespace Sunfish.UICore.Primitives;

/// <summary>
/// Politeness discriminator for live-region announcements per ADR 0077
/// §4 First-Aid baseline + WCAG 2.2 SC 4.1.3 (Status Messages).
/// Renderers map this to the platform-canonical announcement primitive
/// (browser <c>aria-live</c>, Windows UIA Notification, MacCatalyst
/// NSAccessibilityAnnouncement).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LiveRegionPoliteness
{
    /// <summary>Non-disruptive — announced when the screen reader is idle.</summary>
    Polite,

    /// <summary>Interrupts current speech — for time-sensitive but non-emergency announcements.</summary>
    Assertive,

    /// <summary>Highest priority — security/destructive-action announcements per ADR 0077 §4 council F-1.</summary>
    Critical,
}
