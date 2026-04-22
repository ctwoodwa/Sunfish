namespace Sunfish.UIAdapters.Blazor.Components.Layout.Dock;

/// <summary>
/// Event arguments fired by <c>SunfishDockManager</c> whenever the layout
/// tree changes (pane closed, proportion adjusted, or child moved). Handlers
/// receive a snapshot of the top-level root descriptor so they can persist
/// state.
/// </summary>
public class DockLayoutEventArgs
{
    /// <summary>Identifier of the pane or split whose change triggered the event (if any).</summary>
    public string? ChangedNodeId { get; set; }

    /// <summary>Short reason code (e.g. <c>"pane-closed"</c>, <c>"proportions-updated"</c>).</summary>
    public string Reason { get; set; } = "layout-changed";
}

/// <summary>
/// Cancellable event arguments fired before a <c>SunfishDockPane</c> is closed
/// by the user. Handlers may set <see cref="IsCancelled"/> to veto the close.
/// </summary>
public class DockPaneCloseEventArgs
{
    /// <summary>Identifier of the pane being closed.</summary>
    public string PaneId { get; set; } = string.Empty;

    /// <summary>Title of the pane being closed (for UX messages).</summary>
    public string? Title { get; set; }

    /// <summary>Set to <c>true</c> to cancel the close operation.</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Cancellable event arguments fired when a <c>SunfishDockPane</c> is moved
/// (reordered or re-homed into a different split/group). Handlers may set
/// <see cref="IsCancelled"/> to veto the move.
/// </summary>
public class DockPaneMovedEventArgs
{
    /// <summary>Identifier of the pane that moved.</summary>
    public string PaneId { get; set; } = string.Empty;

    /// <summary>Identifier of the container (split or pane group) the pane left.</summary>
    public string? FromContainerId { get; set; }

    /// <summary>Identifier of the container (split or pane group) the pane entered.</summary>
    public string? ToContainerId { get; set; }

    /// <summary>Set to <c>true</c> to cancel the move operation.</summary>
    public bool IsCancelled { get; set; }
}
