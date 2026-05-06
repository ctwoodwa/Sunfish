namespace Sunfish.UICore.Primitives;

/// <summary>
/// Live-region announcement primitive per ADR 0077 §4 + §6. Adapter
/// implementations bridge to the platform-canonical announcement
/// surface: Blazor → <c>aria-live</c> + JS interop; React →
/// <c>aria-live</c> via React ref; MAUI → Windows UIA Notification +
/// MacCatalyst <c>NSAccessibilityAnnouncement</c>.
/// </summary>
/// <remarks>
/// <b>Side-effect-free contract</b> at the policy tier: implementations
/// MUST NOT block on <see cref="Announce"/>; the renderer queues the
/// announcement and returns. Per WCAG SC 4.1.3 + the W#46 §Trust audit
/// model, security-elevated announcements use
/// <see cref="LiveRegionPoliteness.Critical"/>.
/// </remarks>
public interface ILiveAnnouncer
{
    /// <summary>Queue <paramref name="message"/> for announcement at the supplied politeness.</summary>
    /// <param name="message">Localized human-readable announcement text.</param>
    /// <param name="politeness">Discriminator for screen-reader interruption posture.</param>
    void Announce(string message, LiveRegionPoliteness politeness);
}
