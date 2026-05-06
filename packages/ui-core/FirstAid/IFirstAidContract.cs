using Sunfish.UICore.Primitives;

namespace Sunfish.UICore.FirstAid;

/// <summary>
/// First-Aid baseline contract per ADR 0077 §4. Every interactive
/// Sunfish surface inherits this baseline by default; surfaces opting
/// out require an ADR amendment + Stage 07 audit waiver. Composes the
/// WCAG 2.2 AA requirements onto every surface; adapters implement
/// against this contract.
/// </summary>
public interface IFirstAidContract
{
    /// <summary>
    /// Localization key for the contextual help text rendered alongside
    /// the surface (visible + announced through <c>aria-describedby</c>).
    /// Required.
    /// </summary>
    string HelpKey { get; }

    /// <summary>
    /// Error display contract — labels, validation messages, suggested
    /// next action per WCAG SC 3.3.1 + SC 3.3.3. Null when the surface
    /// is non-form.
    /// </summary>
    IFormControlContract? FormControl { get; }

    /// <summary>
    /// Localization key for the suggested-next-action when the surface
    /// is in an empty / error / denied state. Null when no hint applies.
    /// </summary>
    string? NextActionHintKey { get; }

    /// <summary>
    /// Help available in a consistent location across surfaces per
    /// WCAG SC 3.2.6.
    /// </summary>
    HelpLocation HelpLocation { get; }

    /// <summary>
    /// Target-size declaration — the surface MUST satisfy ≥24×24 CSS px
    /// (web) / ≥44pt (iOS) / ≥48dp (Android) per WCAG SC 2.5.8.
    /// </summary>
    TargetSizeCompliance TargetSize { get; }

    /// <summary>Redundant-entry exemption per WCAG SC 3.3.7.</summary>
    bool ExemptFromRedundantEntry { get; }

    /// <summary>
    /// Default live-announcement politeness for this surface per
    /// ADR 0077 §4 council F-1. Passed to
    /// <see cref="ILiveAnnouncer"/> when the surface emits status
    /// changes. Read-only / dashboard surfaces default to
    /// <see cref="LiveRegionPoliteness.Polite"/>; denial surfaces +
    /// alert surfaces override to <see cref="LiveRegionPoliteness.Assertive"/>;
    /// security / destructive-action surfaces use
    /// <see cref="LiveRegionPoliteness.Critical"/>.
    /// </summary>
    LiveRegionPoliteness LiveAnnouncementPolicy { get; }
}
