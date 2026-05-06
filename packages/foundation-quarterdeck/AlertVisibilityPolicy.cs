using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Disclosure policy for an <see cref="IQuarterdeckAlertSource"/> per
/// ADR 0080 §2.3 rule 5. Governs whether the Quarterdeck ticker omits
/// alerts the actor cannot see (the default; preserves the
/// denied-not-hidden invariant for departments while keeping
/// authority-sensitive alert <i>contents</i> private) or shows every
/// alert from the source regardless.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertVisibilityPolicy
{
    /// <summary>
    /// Default: alerts from a source the actor cannot reach are omitted
    /// from the ticker payload. The denied-not-hidden invariant applies
    /// to <i>departments</i> (see <see cref="DepartmentLink"/>); per-alert
    /// content omission is the right trade-off for authority-sensitive
    /// emergency channels.
    /// </summary>
    OmitForDeniedActors,

    /// <summary>
    /// All alerts from the source are surfaced regardless of the actor's
    /// access decision. Reserved for ship-wide broadcast alert sources
    /// (mass-notification, Mission-Envelope-failed banners) where
    /// suppression would defeat the alert's safety purpose.
    /// </summary>
    ShowAll,
}
