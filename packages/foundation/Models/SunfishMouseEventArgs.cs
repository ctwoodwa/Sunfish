namespace Sunfish.Foundation.Models;

/// <summary>
/// Framework-agnostic representation of a mouse event.
/// Adapter packages map this to their platform's native mouse event type.
/// </summary>
public record SunfishMouseEventArgs
{
    /// <summary>X coordinate relative to the viewport.</summary>
    public double ClientX { get; init; }

    /// <summary>Y coordinate relative to the viewport.</summary>
    public double ClientY { get; init; }

    /// <summary>X coordinate relative to the screen.</summary>
    public double ScreenX { get; init; }

    /// <summary>Y coordinate relative to the screen.</summary>
    public double ScreenY { get; init; }

    /// <summary>X coordinate relative to the element that raised the event.</summary>
    public double OffsetX { get; init; }

    /// <summary>Y coordinate relative to the element that raised the event.</summary>
    public double OffsetY { get; init; }

    /// <summary>Whether the Alt key was held.</summary>
    public bool AltKey { get; init; }

    /// <summary>Whether the Ctrl key was held.</summary>
    public bool CtrlKey { get; init; }

    /// <summary>Whether the Shift key was held.</summary>
    public bool ShiftKey { get; init; }

    /// <summary>Whether the Meta (Win/Cmd) key was held.</summary>
    public bool MetaKey { get; init; }

    /// <summary>Which mouse button triggered the event (0 = left, 1 = middle, 2 = right).</summary>
    public int Button { get; init; }
}
