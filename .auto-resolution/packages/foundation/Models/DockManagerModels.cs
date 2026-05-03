namespace Sunfish.Foundation.Models;

/// <summary>
/// Specifies the type of a dock pane in the layout hierarchy.
/// </summary>
public enum DockPaneType
{
    /// <summary>A leaf pane that renders user content.</summary>
    Content,

    /// <summary>A container that groups child panes into a tab strip.</summary>
    TabGroup,

    /// <summary>A container that splits child panes horizontally or vertically.</summary>
    SplitContainer
}

/// <summary>
/// Specifies the split direction of a <see cref="DockPaneType.SplitContainer"/> pane.
/// </summary>
public enum DockSplitOrientation
{
    /// <summary>Children are arranged side by side (left to right).</summary>
    Horizontal,

    /// <summary>Children are arranged top to bottom.</summary>
    Vertical
}

/// <summary>
/// Describes the complete layout tree of a <c>SunfishDockManager</c>.
/// </summary>
public class DockLayoutDescriptor
{
    /// <summary>The root pane of the layout hierarchy.</summary>
    public DockPaneDescriptor Root { get; set; } = new();
}

/// <summary>
/// Describes a single pane node in the dock manager layout tree.
/// Content panes are leaves; TabGroup and SplitContainer panes contain children.
/// </summary>
public class DockPaneDescriptor
{
    /// <summary>Unique identifier for this pane. Auto-generated if not provided.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The structural type of this pane.</summary>
    public DockPaneType Type { get; set; } = DockPaneType.Content;

    /// <summary>Display title for content panes (shown in tab headers).</summary>
    public string? Title { get; set; }

    /// <summary>Whether the pane can be closed by the user. Defaults to true.</summary>
    public bool Closable { get; set; } = true;

    /// <summary>
    /// Split direction when <see cref="Type"/> is <see cref="DockPaneType.SplitContainer"/>.
    /// </summary>
    public DockSplitOrientation Orientation { get; set; } = DockSplitOrientation.Horizontal;

    /// <summary>
    /// Child pane descriptors. Only meaningful for TabGroup and SplitContainer types.
    /// </summary>
    public List<DockPaneDescriptor> Children { get; set; } = new();

    /// <summary>
    /// Flex proportion for sizing within a split container. Defaults to 1.0.
    /// </summary>
    public double Size { get; set; } = 1.0;

    /// <summary>
    /// Minimum width constraint for the pane (e.g. "100px"). Enforced during resize.
    /// Defaults to "100px".
    /// </summary>
    public string MinWidth { get; set; } = "100px";

    /// <summary>
    /// Minimum height constraint for the pane (e.g. "100px"). Enforced during resize.
    /// Defaults to "100px".
    /// </summary>
    public string MinHeight { get; set; } = "100px";

    /// <summary>
    /// Whether the pane is in floating (overlay) mode. Defaults to false.
    /// </summary>
    public bool IsFloating { get; set; }

    /// <summary>
    /// Position and size of the pane when floating. Ignored when docked.
    /// </summary>
    public DockFloatingSettings FloatingSettings { get; set; } = new();
}

/// <summary>
/// Position and size settings for a floating dock pane.
/// </summary>
public class DockFloatingSettings
{
    /// <summary>CSS top offset (e.g. "100px"). Defaults to "50px".</summary>
    public string Top { get; set; } = "50px";

    /// <summary>CSS left offset (e.g. "100px"). Defaults to "50px".</summary>
    public string Left { get; set; } = "50px";

    /// <summary>CSS width (e.g. "400px"). Defaults to "400px".</summary>
    public string Width { get; set; } = "400px";

    /// <summary>CSS height (e.g. "300px"). Defaults to "300px".</summary>
    public string Height { get; set; } = "300px";
}

/// <summary>
/// Event arguments for the <c>OnTabReordered</c> callback, fired when a tab is
/// reordered within a tab group via drag-and-drop.
/// </summary>
public class DockTabReorderEventArgs
{
    /// <summary>The Id of the tab group in which the reorder occurred.</summary>
    public string TabGroupId { get; set; } = string.Empty;

    /// <summary>The Id of the pane (tab) that was moved.</summary>
    public string PaneId { get; set; } = string.Empty;

    /// <summary>The original index of the tab before the drag.</summary>
    public int OldIndex { get; set; }

    /// <summary>The new index of the tab after the drop.</summary>
    public int NewIndex { get; set; }
}

/// <summary>
/// Event arguments for the <c>OnPaneMoved</c> callback, fired when a tab is
/// moved from one tab group to another via drag-and-drop.
/// </summary>
public class DockPaneMoveEventArgs
{
    /// <summary>The Id of the pane (tab) that was moved.</summary>
    public string PaneId { get; set; } = string.Empty;

    /// <summary>The Id of the source tab group from which the pane was moved.</summary>
    public string SourceGroupId { get; set; } = string.Empty;

    /// <summary>The Id of the target tab group to which the pane was moved.</summary>
    public string TargetGroupId { get; set; } = string.Empty;
}
