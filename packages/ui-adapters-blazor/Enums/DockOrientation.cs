namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Split direction for a <c>SunfishDockSplit</c> container inside a
/// <c>SunfishDockManager</c>. Determines whether children are arranged
/// side-by-side (horizontal) or stacked top-to-bottom (vertical).
/// </summary>
public enum DockOrientation
{
    /// <summary>Children arranged left-to-right in a row.</summary>
    Horizontal,

    /// <summary>Children arranged top-to-bottom in a column.</summary>
    Vertical,
}
