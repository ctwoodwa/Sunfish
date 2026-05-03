namespace Sunfish.Foundation.Models;

/// <summary>
/// Represents the position, size, and display state of a Window component.
/// </summary>
public class WindowState
{
    /// <summary>
    /// The top offset of the window in pixels.
    /// </summary>
    public double Top { get; set; }

    /// <summary>
    /// The left offset of the window in pixels.
    /// </summary>
    public double Left { get; set; }

    /// <summary>
    /// The width of the window (CSS value, e.g., "600px", "50%").
    /// </summary>
    public string? Width { get; set; }

    /// <summary>
    /// The height of the window (CSS value, e.g., "400px", "50%").
    /// </summary>
    public string? Height { get; set; }

    /// <summary>
    /// Whether the window is currently maximized.
    /// </summary>
    public bool IsMaximized { get; set; }

    /// <summary>
    /// Whether the window is currently minimized.
    /// </summary>
    public bool IsMinimized { get; set; }
}
