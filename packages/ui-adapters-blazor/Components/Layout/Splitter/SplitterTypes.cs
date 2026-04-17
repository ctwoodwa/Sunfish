namespace Sunfish.Components.Blazor.Components.Layout;

/// <summary>
/// Event arguments for splitter pane resize events.
/// </summary>
public class SplitterResizeEventArgs
{
    /// <summary>The index of the pane being resized.</summary>
    public int PaneIndex { get; set; }

    /// <summary>The new size of the pane (CSS value).</summary>
    public string NewSize { get; set; } = "";

    /// <summary>The new size of the adjacent pane (CSS value).</summary>
    public string AdjacentSize { get; set; } = "";
}

/// <summary>
/// Event arguments for splitter pane collapse and expand events.
/// </summary>
public class SplitterCollapseEventArgs
{
    /// <summary>The index of the pane being collapsed or expanded.</summary>
    public int PaneIndex { get; set; }
}

/// <summary>
/// Represents the persisted state of a splitter, including pane sizes and collapse state.
/// </summary>
public class SplitterState
{
    /// <summary>The sizes of each pane.</summary>
    public List<string> PaneSizes { get; set; } = [];

    /// <summary>The collapsed state of each pane.</summary>
    public List<bool> CollapsedPanes { get; set; } = [];
}
