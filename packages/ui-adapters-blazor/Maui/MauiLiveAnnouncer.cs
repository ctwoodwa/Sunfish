using System;
using Sunfish.UICore.Primitives;

namespace Sunfish.UIAdapters.Blazor.Maui;

/// <summary>
/// MAUI adapter for <see cref="ILiveAnnouncer"/> per ADR 0077 §4 + §6.
/// Windows: <c>AutomationNotificationEvent</c> via WinUI automation peers.
/// MacCatalyst/iOS: <c>UIAccessibility.PostNotification</c>
///   (<c>UIAccessibilityAnnouncementNotification</c>).
/// iOS/Android: deferred to W#23 (field-capture app follow-up).
/// </summary>
/// <remarks>
/// Uses an internal <see cref="IPlatformA11yNotifier"/> delegate so tests
/// can inject a recording stub without hitting actual platform APIs.
/// </remarks>
public sealed class MauiLiveAnnouncer : ILiveAnnouncer
{
    private readonly IPlatformA11yNotifier _notifier;

    /// <summary>Production constructor — auto-detects platform at compile time.</summary>
    public MauiLiveAnnouncer() : this(CreatePlatformNotifier()) { }

    /// <summary>Injection constructor for tests (internal visibility).</summary>
    internal MauiLiveAnnouncer(IPlatformA11yNotifier notifier)
        => _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));

    /// <inheritdoc/>
    public void Announce(string message, LiveRegionPoliteness politeness)
        => _notifier.Notify(message, politeness);

    private static IPlatformA11yNotifier CreatePlatformNotifier()
    {
#if WINDOWS
        return new WindowsA11yNotifier();
#elif MACCATALYST || IOS
        return new MacCatalystA11yNotifier();
#else
        return new NullA11yNotifier();
#endif
    }
}

/// <summary>
/// Abstraction over platform-specific accessibility notification.
/// Implementations are selected at compile time per-TFM via
/// <c>#if WINDOWS / MACCATALYST</c> guards.
/// </summary>
public interface IPlatformA11yNotifier
{
    void Notify(string message, LiveRegionPoliteness politeness);
}

/// <summary>Fallback no-op notifier for unsupported TFMs (Android, plain net11.0).</summary>
public sealed class NullA11yNotifier : IPlatformA11yNotifier
{
    public void Notify(string message, LiveRegionPoliteness politeness) { }
}

#if WINDOWS
/// <summary>Windows UIA notification via WinUI AutomationPeer.</summary>
public sealed class WindowsA11yNotifier : IPlatformA11yNotifier
{
    public void Notify(string message, LiveRegionPoliteness politeness)
    {
        var kind = politeness == LiveRegionPoliteness.Polite
            ? Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionCompleted
            : Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionAborted;

        var processing = politeness == LiveRegionPoliteness.Polite
            ? Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.CurrentThenMostRecent
            : Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationProcessing.MostRecent;

        var window = Microsoft.Maui.MauiWinUIApplication.Current?.MainWindow;
        window?.GetAutomationPeer()?.RaiseNotificationEvent(
            kind, processing, message, "sf-live-" + politeness.ToString());
    }
}
#endif

#if MACCATALYST || IOS
/// <summary>MacCatalyst/iOS UIAccessibility announcement.</summary>
public sealed class MacCatalystA11yNotifier : IPlatformA11yNotifier
{
    public void Notify(string message, LiveRegionPoliteness politeness)
    {
        UIKit.UIAccessibility.PostNotification(
            UIKit.UIAccessibilityPostNotification.Announcement,
            Foundation.NSObject.FromObject(message));
    }
}
#endif
