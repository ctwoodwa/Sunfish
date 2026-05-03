namespace Sunfish.Compat.Syncfusion.Enums;

/// <summary>
/// Mirrors Syncfusion.Blazor.Popups.Position (SfTooltip.Position).
/// Sunfish's <c>TooltipPosition</c> has only 4 cardinal values; the 12 Syncfusion values
/// collapse via LogAndFallback. See docs/compat-syncfusion-mapping.md.
/// </summary>
public enum Position
{
    TopLeft = 0,
    TopCenter = 1,
    TopRight = 2,
    BottomLeft = 3,
    BottomCenter = 4,
    BottomRight = 5,
    LeftTop = 6,
    LeftCenter = 7,
    LeftBottom = 8,
    RightTop = 9,
    RightCenter = 10,
    RightBottom = 11
}
