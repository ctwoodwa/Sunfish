namespace Sunfish.Compat.Syncfusion.Enums;

/// <summary>
/// Mirrors Syncfusion.Blazor.DataForm.ValidationDisplayMode. <see cref="ToolTip"/> throws —
/// Sunfish has no tooltip-based validation display. See docs/compat-syncfusion-mapping.md.
/// </summary>
public enum ValidationDisplayMode
{
    Inline = 0,
    ToolTip = 1,
    None = 2
}
