namespace Sunfish.Components.Blazor.Components.Overlays;

/// <summary>
/// Defines the preferred placement of a <see cref="SunfishPopup"/> relative to its anchor element.
/// </summary>
public enum PopupPlacement
{
    /// <summary>Display above the anchor.</summary>
    Top,

    /// <summary>Display below the anchor (default).</summary>
    Bottom,

    /// <summary>Display to the left of the anchor.</summary>
    Left,

    /// <summary>Display to the right of the anchor.</summary>
    Right,

    /// <summary>Automatically choose the best placement based on available space (deferred to Pass 4).</summary>
    Auto
}
