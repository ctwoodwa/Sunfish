namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Anchor position of a <c>SunfishDockPane</c> inside its enclosing dock split.
/// Used by future drag-to-dock behaviour; panes default to
/// <see cref="Center"/> which renders in the normal tab flow.
/// </summary>
public enum DockPosition
{
    /// <summary>Dock the pane to the left edge of its container.</summary>
    Left,

    /// <summary>Dock the pane to the right edge of its container.</summary>
    Right,

    /// <summary>Dock the pane to the top edge of its container.</summary>
    Top,

    /// <summary>Dock the pane to the bottom edge of its container.</summary>
    Bottom,

    /// <summary>Default tabbed/central placement inside the container.</summary>
    Center,
}
