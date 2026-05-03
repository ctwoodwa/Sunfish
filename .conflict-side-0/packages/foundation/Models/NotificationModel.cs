using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Models;

/// <summary>
/// Represents a notification message to be displayed by the notification host.
/// </summary>
public class NotificationModel
{
    /// <summary>
    /// Unique identifier for this notification instance.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The notification message text.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The severity/theme of the notification.
    /// </summary>
    public ToastSeverity Severity { get; set; } = ToastSeverity.Info;

    /// <summary>
    /// Duration in milliseconds before auto-close. Zero or negative means no auto-close.
    /// </summary>
    public int CloseAfterMs { get; set; } = 5000;

    /// <summary>
    /// Whether the notification can be dismissed by the user.
    /// </summary>
    public bool Closeable { get; set; } = true;

    /// <summary>
    /// Optional custom theme color CSS value (e.g. "#ff5722", "var(--brand-primary)").
    /// When set, overrides the default severity-based coloring.
    /// </summary>
    public string? ThemeColor { get; set; }
}
