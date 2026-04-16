namespace Sunfish.Core.Enums;

/// <summary>
/// Specifies the trigger that shows a tooltip.
/// </summary>
public enum TooltipShowOn
{
    /// <summary>Show tooltip on mouse hover (default).</summary>
    Hover,

    /// <summary>Show tooltip on click/tap.</summary>
    Click,

    /// <summary>Show tooltip when the target receives focus.</summary>
    Focus
}
