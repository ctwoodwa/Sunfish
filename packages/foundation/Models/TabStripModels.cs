namespace Sunfish.Foundation.Models;

/// <summary>
/// Represents the serialisable state of a <c>SunfishTabStrip</c> component.
/// </summary>
public class TabStripState
{
    /// <summary>The ID of the currently active tab.</summary>
    public string ActiveTabId { get; set; } = string.Empty;

    /// <summary>
    /// The state of each tab in the order they currently appear in the strip.
    /// Reorder this collection and pass it to <c>SetState</c> to reorder tabs programmatically.
    /// </summary>
    public List<TabStripTabState> TabStates { get; set; } = new();
}

/// <summary>
/// Represents the serialisable state of a single <c>TabStripTab</c>.
/// </summary>
public class TabStripTabState
{
    /// <summary>The unique identifier of the tab.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Whether the tab is visible.</summary>
    public bool Visible { get; set; }

    /// <summary>Whether the tab is pinned to the start of the tab list.</summary>
    public bool Pinned { get; set; }
}

/// <summary>
/// Event arguments passed to the <c>OnStateInit</c> and <c>OnStateChanged</c> events.
/// </summary>
public class TabStripStateEventArgs
{
    /// <summary>The current state of the TabStrip.</summary>
    public TabStripState TabStripState { get; set; } = new();
}

/// <summary>
/// Event arguments passed to the <c>OnTabReorder</c> event.
/// </summary>
public class TabStripTabReorderEventArgs
{
    /// <summary>The ID of the tab that was moved.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The zero-based index the tab was dragged from.</summary>
    public int OldIndex { get; set; }

    /// <summary>The zero-based index the tab was dropped at.</summary>
    public int NewIndex { get; set; }
}
