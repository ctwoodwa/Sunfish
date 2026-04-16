namespace Sunfish.Core.Enums;

/// <summary>
/// Specifies the visual display state of a window.
/// </summary>
public enum WindowDisplayState
{
    /// <summary>Normal sized and positioned window.</summary>
    Normal,

    /// <summary>Minimized to a collapsed state.</summary>
    Minimized,

    /// <summary>Maximized to fill the viewport.</summary>
    Maximized
}

/// <summary>
/// Specifies the available actions for a Window title bar.
/// </summary>
[Flags]
public enum WindowAction
{
    /// <summary>No actions.</summary>
    None = 0,

    /// <summary>The window can be closed.</summary>
    Close = 1,

    /// <summary>The window can be minimized.</summary>
    Minimize = 2,

    /// <summary>The window can be maximized or restored.</summary>
    Maximize = 4
}

/// <summary>
/// Specifies the animation type for window open/close transitions.
/// </summary>
public enum WindowAnimationType
{
    /// <summary>No animation.</summary>
    None,

    /// <summary>Fade in/out animation.</summary>
    Fade,

    /// <summary>Slide down animation.</summary>
    SlideDown,

    /// <summary>Slide up animation.</summary>
    SlideUp,

    /// <summary>Zoom/scale animation.</summary>
    Zoom
}

/// <summary>
/// Predefined window sizes.
/// </summary>
public enum WindowSize
{
    /// <summary>Small window (300px width).</summary>
    Small,

    /// <summary>Medium window (600px width). Default.</summary>
    Medium,

    /// <summary>Large window (900px width).</summary>
    Large
}

/// <summary>
/// Specifies the horizontal alignment of footer content.
/// </summary>
public enum WindowFooterLayoutAlign
{
    /// <summary>Align content to the start (left in LTR).</summary>
    Start,

    /// <summary>Center the content.</summary>
    Center,

    /// <summary>Align content to the end (right in LTR).</summary>
    End,

    /// <summary>Stretch content to fill the footer.</summary>
    Stretch
}
