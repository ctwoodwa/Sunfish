using System;

namespace Sunfish.UIAdapters.Blazor.Shell;

/// <summary>Single row in a <see cref="SunfishUserMenu"/>.</summary>
public class PopupMenuItem
{
    /// <summary>Optional icon name (provider-resolved) rendered at the start of the row.</summary>
    public string? Icon { get; set; }

    /// <summary>Display label for the row.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional shortcut hint (for example, <c>Ctrl+P</c>) rendered to the right of the label.</summary>
    public string? Shortcut { get; set; }

    /// <summary>Whether this row opens a submenu (renders an expand affordance).</summary>
    public bool IsExpandable { get; set; }

    /// <summary>Optional secondary value rendered on the right edge (for example, the active selection).</summary>
    public string? CurrentValue { get; set; }

    /// <summary>Optional click handler invoked when the row is activated.</summary>
    public Action? OnClick { get; set; }

    /// <summary>Whether a divider should be rendered after this row.</summary>
    public bool IsDividerAfter { get; set; }

    /// <summary>Optional navigation target. When set, activating the row navigates here.</summary>
    public string? Href { get; set; }

    /// <summary>Whether the row is rendered in a disabled (non-interactive) state.</summary>
    public bool IsDisabled { get; set; }
}

/// <summary>Single notification entry rendered in <see cref="SunfishNotificationBell"/>.</summary>
public class NotificationItem
{
    /// <summary>Stable identifier used for keyed rendering and read/unread tracking. Defaults to a new GUID.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    /// <summary>Optional icon name (provider-resolved) rendered alongside the notification.</summary>
    public string? Icon { get; set; }

    /// <summary>Optional title rendered above the message.</summary>
    public string? Title { get; set; }

    /// <summary>Notification body text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>When the notification was raised. Defaults to <see cref="DateTimeOffset.Now"/>.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    /// <summary>Whether the user has acknowledged this notification.</summary>
    public bool IsRead { get; set; }

    /// <summary>Optional click handler invoked when the notification is activated.</summary>
    public Action? OnClick { get; set; }
}
