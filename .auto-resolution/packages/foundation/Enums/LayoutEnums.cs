namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies how accordion items expand (single or multiple expanded at once).
/// </summary>
public enum AccordionExpandMode
{
    /// <summary>Only one item can be expanded at a time. Expanding one collapses others.</summary>
    Single,

    /// <summary>Multiple items can be expanded simultaneously.</summary>
    Multiple
}

/// <summary>
/// Specifies the layout direction of a stack container.
/// </summary>
public enum StackDirection
{
    /// <summary>Items are arranged in a horizontal row.</summary>
    Horizontal,

    /// <summary>Items are arranged in a vertical column.</summary>
    Vertical
}

/// <summary>
/// Specifies how items are aligned within a stack container.
/// </summary>
public enum StackAlignment
{
    /// <summary>Items are aligned to the start of the container.</summary>
    Start,

    /// <summary>Items are centered within the container.</summary>
    Center,

    /// <summary>Items are aligned to the end of the container.</summary>
    End,

    /// <summary>Items are stretched to fill the container.</summary>
    Stretch,

    /// <summary>Items are evenly distributed with space between them.</summary>
    SpaceBetween,

    /// <summary>Items are evenly distributed with space around them.</summary>
    SpaceAround
}

/// <summary>
/// Specifies the display mode of a drawer.
/// </summary>
public enum DrawerMode
{
    /// <summary>Drawer overlays the page content with a backdrop.</summary>
    Overlay,

    /// <summary>Drawer pushes the page content aside.</summary>
    Push
}

/// <summary>
/// Specifies the position from which a drawer panel slides in.
/// </summary>
public enum DrawerPosition
{
    /// <summary>Drawer slides in from the left edge.</summary>
    Left,

    /// <summary>Drawer slides in from the right edge.</summary>
    Right,

    /// <summary>Drawer slides in from the top edge.</summary>
    Top,

    /// <summary>Drawer slides in from the bottom edge.</summary>
    Bottom
}

/// <summary>
/// Specifies the positioning behavior of an application bar.
/// </summary>
public enum AppBarPosition
{
    /// <summary>Positioned at the top of the page in normal flow.</summary>
    Top,

    /// <summary>Positioned at the bottom of the page in normal flow.</summary>
    Bottom,

    /// <summary>Fixed to the viewport and does not scroll with content.</summary>
    Fixed,

    /// <summary>Sticks to the top of the viewport when scrolled past.</summary>
    Sticky
}

/// <summary>
/// Specifies the position of the tab list relative to the tab content.
/// </summary>
public enum TabPosition
{
    /// <summary>Tabs are displayed above the content.</summary>
    Top,

    /// <summary>Tabs are displayed below the content.</summary>
    Bottom,

    /// <summary>Tabs are displayed to the left of the content.</summary>
    Left,

    /// <summary>Tabs are displayed to the right of the content.</summary>
    Right
}

/// <summary>
/// Specifies the alignment of tabs within the tab list.
/// </summary>
public enum TabAlignment
{
    /// <summary>Tabs are aligned to the start (default).</summary>
    Start,

    /// <summary>Tabs are aligned to the end.</summary>
    End,

    /// <summary>Tabs are centered.</summary>
    Center,

    /// <summary>Tabs are evenly distributed across the available space.</summary>
    Justify,

    /// <summary>Tabs stretch to fill the available space.</summary>
    Stretched
}

/// <summary>
/// Specifies the size of tabs in a tab strip.
/// </summary>
public enum TabSize
{
    /// <summary>Compact tabs with reduced padding.</summary>
    Small,

    /// <summary>Default tab size.</summary>
    Medium,

    /// <summary>Larger tabs with increased padding.</summary>
    Large
}

/// <summary>
/// Controls how the TabStrip handles tabs that exceed the available space.
/// </summary>
public enum TabStripOverflowMode
{
    /// <summary>No overflow handling; tabs wrap or overflow the container.</summary>
    None,

    /// <summary>Scroll buttons appear when tabs exceed the available width.</summary>
    Scroll,

    /// <summary>Overflowing tabs are collected into a dropdown menu.</summary>
    Menu
}

/// <summary>
/// Specifies the position of scroll buttons in the TabStrip when <see cref="TabStripOverflowMode.Scroll"/> is active.
/// </summary>
public enum TabStripScrollButtonsPosition
{
    /// <summary>Scroll buttons are rendered on both ends of the tab list (default).</summary>
    Split,

    /// <summary>Both scroll buttons are rendered before the tab list.</summary>
    Start,

    /// <summary>Both scroll buttons are rendered after the tab list.</summary>
    End
}

/// <summary>
/// Controls the visibility of scroll buttons when the TabStrip is in Scroll overflow mode.
/// </summary>
public enum TabStripScrollButtonsVisibility
{
    /// <summary>Scroll buttons are always visible; disabled when all tabs fit (default).</summary>
    Visible,

    /// <summary>Scroll buttons appear only when tabs overflow the available space.</summary>
    Auto,

    /// <summary>Scroll buttons are never displayed.</summary>
    Hidden
}

/// <summary>
/// Specifies the orientation of a splitter.
/// </summary>
public enum SplitterOrientation
{
    /// <summary>Panes are arranged horizontally (side by side).</summary>
    Horizontal,

    /// <summary>Panes are arranged vertically (stacked).</summary>
    Vertical
}

/// <summary>
/// Identifies which side of a splitter is being collapsed or restored.
/// </summary>
public enum SplitterSide
{
    /// <summary>The left (or top) pane.</summary>
    Left,

    /// <summary>The right (or bottom) pane.</summary>
    Right
}

/// <summary>
/// Specifies the layout orientation of a stepper control.
/// </summary>
public enum StepperOrientation
{
    /// <summary>Steps are arranged horizontally.</summary>
    Horizontal,

    /// <summary>Steps are arranged vertically.</summary>
    Vertical
}

/// <summary>
/// Specifies the current status of a step in a stepper control.
/// </summary>
public enum StepStatus
{
    /// <summary>The step has not yet been reached.</summary>
    Pending,

    /// <summary>The step is currently in progress.</summary>
    Active,

    /// <summary>The step has been successfully completed.</summary>
    Completed,

    /// <summary>The step encountered an error.</summary>
    Error
}

/// <summary>
/// Specifies the animation type for SunfishAnimationContainer.
/// </summary>
public enum AnimationType
{
    /// <summary>Fade in/out animation.</summary>
    Fade,

    /// <summary>Slide up animation.</summary>
    SlideUp,

    /// <summary>Slide down animation.</summary>
    SlideDown,

    /// <summary>Slide left animation.</summary>
    SlideLeft,

    /// <summary>Slide right animation.</summary>
    SlideRight,

    /// <summary>Zoom in/out animation.</summary>
    Zoom,

    /// <summary>Expand/collapse animation.</summary>
    Expand
}
