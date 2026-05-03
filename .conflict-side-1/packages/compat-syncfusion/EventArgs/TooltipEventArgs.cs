namespace Sunfish.Compat.Syncfusion;

/// <summary>
/// Syncfusion-shaped tooltip lifecycle event arguments (SfTooltip.Opened / .Closed / .BeforeOpen).
/// Mirrors <c>Syncfusion.Blazor.Popups.TooltipEventArgs</c>.
/// </summary>
public class TooltipEventArgs
{
    /// <summary>The target element reference.</summary>
    public object? Target { get; init; }

    /// <summary>The tooltip type (e.g. <c>"ToolTip"</c>).</summary>
    public string? Type { get; init; }

    /// <summary>Cancel the lifecycle transition (only honored on "Before*" events).</summary>
    public bool Cancel { get; set; }
}
