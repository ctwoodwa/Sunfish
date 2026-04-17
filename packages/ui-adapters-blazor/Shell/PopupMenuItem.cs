using System;

namespace Sunfish.Components.Blazor.Shell;

/// <summary>Single row in a <see cref="SunfishUserMenu"/>.</summary>
public class PopupMenuItem
{
    public string? Icon { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Shortcut { get; set; }
    public bool IsExpandable { get; set; }
    public string? CurrentValue { get; set; }
    public Action? OnClick { get; set; }
    public bool IsDividerAfter { get; set; }
    public string? Href { get; set; }
    public bool IsDisabled { get; set; }
}

/// <summary>Single notification entry rendered in <see cref="SunfishNotificationBell"/>.</summary>
public class NotificationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string? Icon { get; set; }
    public string? Title { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public bool IsRead { get; set; }
    public Action? OnClick { get; set; }
}
