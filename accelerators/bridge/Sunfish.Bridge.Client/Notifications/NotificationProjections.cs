using Sunfish.Components.Blazor.Shell;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;
using Sunfish.Foundation.Notifications;
using Sunfish.Foundation.Services;

namespace Sunfish.Bridge.Client.Notifications;

/// <summary>
/// Maps a canonical <see cref="UserNotification"/> to the bell/feed view shape
/// (<see cref="NotificationItem"/>, owned by Sunfish.Components.Blazor.Shell).
///
/// This is a one-way projection — the bell never mutates the canonical store directly;
/// "mark read" / "delete read" actions go through <see cref="IUserNotificationService"/>
/// using the <see cref="UserNotification.Id"/> carried on each projected item.
/// </summary>
public static class NotificationFeedProjection
{
    public static NotificationItem ToFeedItem(UserNotification n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Message = n.Message,
        Timestamp = n.CreatedAtUtc.ToLocalTime(),
        IsRead = n.IsRead,
        Icon = IconForSource(n.Source),
    };

    private static string? IconForSource(NotificationSource source) => source switch
    {
        // Inline 16px SVGs keep the bell self-contained without an icon library lookup.
        NotificationSource.Tasks      => "<svg viewBox='0 0 20 20' width='16' height='16'><path d='M4 5h12M4 10h12M4 15h8' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linecap='round'/></svg>",
        NotificationSource.Risks      => "<svg viewBox='0 0 20 20' width='16' height='16'><path d='M10 3l8 14H2zM10 8v4M10 15v.01' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linecap='round' stroke-linejoin='round'/></svg>",
        NotificationSource.Budget     => "<svg viewBox='0 0 20 20' width='16' height='16'><path d='M3 17V8M8 17V4M13 17v-7M18 17V6' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linecap='round'/></svg>",
        NotificationSource.Comments   => "<svg viewBox='0 0 20 20' width='16' height='16'><path d='M3 5h14v9H8l-4 3v-3H3z' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linejoin='round'/></svg>",
        NotificationSource.Milestones => "<svg viewBox='0 0 20 20' width='16' height='16'><path d='M5 3v14M5 4h10l-2 3 2 3H5' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linejoin='round'/></svg>",
        NotificationSource.Files      => "<svg viewBox='0 0 20 20' width='16' height='16'><path d='M5 3h7l4 4v10H5zM12 3v4h4' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linejoin='round'/></svg>",
        NotificationSource.Mentions   => "<svg viewBox='0 0 20 20' width='16' height='16'><circle cx='10' cy='10' r='3.2' fill='none' stroke='currentColor' stroke-width='1.6'/><path d='M13.2 10v1.2a2 2 0 0 0 4 0V10a7 7 0 1 0-3 5.7' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linecap='round'/></svg>",
        NotificationSource.Assistant  => "<svg viewBox='0 0 20 20' width='16' height='16'><path d='M10 3l1.8 4.2L16 9l-4.2 1.8L10 15l-1.8-4.2L4 9l4.2-1.8z' fill='none' stroke='currentColor' stroke-width='1.6' stroke-linejoin='round'/></svg>",
        _                             => null,
    };
}

/// <summary>
/// Adapts the canonical notification record into a transient toast on the existing
/// Sunfish presentation host. The toast model owns close-after, severity colour, etc.;
/// the canonical record stays free of presentation concerns.
///
/// Registered as <see cref="IUserNotificationToastForwarder"/> in DI; the canonical
/// service calls <see cref="Forward"/> only when delivery policy includes
/// <see cref="NotificationDelivery.ToastOnly"/>.
/// </summary>
public sealed class SunfishToastUserNotificationForwarder : IUserNotificationToastForwarder
{
    private readonly ISunfishNotificationService _toastHost;

    public SunfishToastUserNotificationForwarder(ISunfishNotificationService toastHost)
    {
        _toastHost = toastHost;
    }

    public void Forward(UserNotification notification)
    {
        var severity = MapSeverity(notification.Importance);
        var message = string.IsNullOrWhiteSpace(notification.Title)
            ? notification.Message
            : $"{notification.Title} — {notification.Message}";

        // Critical events stay on screen until dismissed; everything else uses the host default.
        var durationMs = notification.Importance == NotificationImportance.Critical ? 0 : 5000;

        _toastHost.Show(new NotificationModel
        {
            Id = notification.Id,
            Message = message,
            Severity = severity,
            CloseAfterMs = durationMs,
            Closeable = true,
        });
    }

    private static ToastSeverity MapSeverity(NotificationImportance importance) => importance switch
    {
        NotificationImportance.Low      => ToastSeverity.Info,
        NotificationImportance.Normal   => ToastSeverity.Info,
        NotificationImportance.High     => ToastSeverity.Warning,
        NotificationImportance.Critical => ToastSeverity.Error,
        _                               => ToastSeverity.Info,
    };
}
