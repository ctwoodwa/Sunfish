namespace Sunfish.Components.Blazor.Internal.Interop;

/// <summary>
/// Represents an element's bounding rectangle relative to the viewport.
/// </summary>
internal record ElementRect(double X, double Y, double Width, double Height)
{
    public double Top => Y;
    public double Left => X;
    public double Bottom => Y + Height;
    public double Right => X + Width;
}

/// <summary>
/// Represents the browser viewport dimensions.
/// </summary>
internal record ViewportRect(double Width, double Height, double ScrollX, double ScrollY);

/// <summary>
/// Options for starting a drag operation.
/// </summary>
internal record DragStartOptions
{
    /// <summary>Starting pointer X coordinate (clientX).</summary>
    public double ClientX { get; init; }

    /// <summary>Starting pointer Y coordinate (clientY).</summary>
    public double ClientY { get; init; }

    /// <summary>If set, constrain dragging within this CSS selector's bounds.</summary>
    public string? ContainmentSelector { get; init; }

    /// <summary>Disable text selection on body during drag.</summary>
    public bool DisableTextSelection { get; init; } = true;
}

/// <summary>
/// Reports the current position during a drag operation.
/// </summary>
internal record DragUpdate(double ClientX, double ClientY, double DeltaX, double DeltaY);

/// <summary>
/// Final result of a completed drag operation.
/// </summary>
internal record DragResult(double FinalX, double FinalY, double TotalDeltaX, double TotalDeltaY, bool WasCancelled);

/// <summary>
/// Identifies a resize handle direction.
/// </summary>
internal enum ResizeHandle
{
    None = 0,
    Top = 1,
    Right = 2,
    Bottom = 4,
    Left = 8,
    TopRight = Top | Right,
    BottomRight = Bottom | Right,
    BottomLeft = Bottom | Left,
    TopLeft = Top | Left
}

/// <summary>
/// Constraints for a resize operation.
/// </summary>
internal record ResizeConstraints
{
    public double MinWidth { get; init; } = 50;
    public double MinHeight { get; init; } = 30;
    public double MaxWidth { get; init; } = double.PositiveInfinity;
    public double MaxHeight { get; init; } = double.PositiveInfinity;
    public bool ClampToParent { get; init; }
}

/// <summary>
/// Reports the current state during a resize operation.
/// </summary>
internal record ResizeUpdate(double Width, double Height, double Top, double Left, ResizeHandle ActiveHandle);

/// <summary>
/// Anchor positioning options for popups/popovers.
/// </summary>
internal record PopupAnchorOptions
{
    /// <summary>Preferred placement relative to the anchor.</summary>
    public PopupPlacement Placement { get; init; } = PopupPlacement.Bottom;

    /// <summary>Offset in pixels from the anchor edge.</summary>
    public double Offset { get; init; } = 4;

    /// <summary>Whether to auto-flip if the popup would overflow the viewport.</summary>
    public bool AutoFlip { get; init; } = true;

    /// <summary>Viewport margin in pixels to maintain when flipping.</summary>
    public double ViewportMargin { get; init; } = 8;
}

/// <summary>
/// Preferred popup placement direction.
/// </summary>
internal enum PopupPlacement
{
    Top,
    TopStart,
    TopEnd,
    Bottom,
    BottomStart,
    BottomEnd,
    Left,
    LeftStart,
    LeftEnd,
    Right,
    RightStart,
    RightEnd
}

/// <summary>
/// Result of a popup positioning calculation.
/// </summary>
internal record PopupPositionResult
{
    public double Top { get; init; }
    public double Left { get; init; }
    public PopupPlacement ActualPlacement { get; init; }
    public bool WasFlipped { get; init; }
}

/// <summary>
/// Represents an observed intersection change.
/// </summary>
internal record IntersectionState
{
    public bool IsIntersecting { get; init; }
    public double IntersectionRatio { get; init; }
}

/// <summary>
/// Request to trigger a browser file download.
/// </summary>
internal record DownloadRequest
{
    /// <summary>The filename presented to the user.</summary>
    public required string FileName { get; init; }

    /// <summary>The MIME content type.</summary>
    public string ContentType { get; init; } = "application/octet-stream";

    /// <summary>Base64-encoded file content.</summary>
    public required string Base64Content { get; init; }
}

/// <summary>
/// Request to write content to the system clipboard.
/// </summary>
internal record ClipboardWriteRequest
{
    /// <summary>Plain text to write to the clipboard.</summary>
    public string? Text { get; init; }

    /// <summary>HTML content to write to the clipboard.</summary>
    public string? Html { get; init; }
}
