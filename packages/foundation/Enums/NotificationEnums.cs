namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies the vertical position of a notification host on screen.
/// </summary>
public enum NotificationVerticalPosition
{
    /// <summary>Notifications appear at the top of the viewport.</summary>
    Top,

    /// <summary>Notifications appear at the bottom of the viewport.</summary>
    Bottom
}

/// <summary>
/// Specifies the horizontal position of a notification host on screen.
/// </summary>
public enum NotificationHorizontalPosition
{
    /// <summary>Notifications appear on the left side.</summary>
    Left,

    /// <summary>Notifications appear in the center.</summary>
    Center,

    /// <summary>Notifications appear on the right side.</summary>
    Right
}

/// <summary>
/// Specifies the animation type used when a notification appears.
/// </summary>
public enum NotificationAnimation
{
    /// <summary>No animation.</summary>
    None,

    /// <summary>Fade-in animation.</summary>
    Fade,

    /// <summary>Slide-in animation from the edge.</summary>
    SlideIn
}
