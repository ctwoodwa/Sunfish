namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>Event args for Gantt create operations.</summary>
public class GanttCreateEventArgs
{
    /// <summary>The new item being created. Cast to your model type.</summary>
    public object Item { get; set; } = default!;
    /// <summary>The parent item under which the new item is being created, or null for root.</summary>
    public object? ParentItem { get; set; }
}

/// <summary>Event args for Gantt update operations.</summary>
public class GanttUpdateEventArgs
{
    /// <summary>The updated item. Cast to your model type.</summary>
    public object Item { get; set; } = default!;
}

/// <summary>Event args for Gantt delete operations.</summary>
public class GanttDeleteEventArgs
{
    /// <summary>The item being deleted. Cast to your model type.</summary>
    public object Item { get; set; } = default!;
}

/// <summary>Event args for Gantt expand operations.</summary>
public class GanttExpandEventArgs
{
    /// <summary>The expanded item. Cast to your model type.</summary>
    public object Item { get; set; } = default!;
    /// <summary>Whether the component should re-render after this event.</summary>
    public bool ShouldRender { get; set; } = true;
}

/// <summary>Event args for Gantt collapse operations.</summary>
public class GanttCollapseEventArgs
{
    /// <summary>The collapsed item. Cast to your model type.</summary>
    public object Item { get; set; } = default!;
    /// <summary>Whether the component should re-render after this event.</summary>
    public bool ShouldRender { get; set; } = true;
}

/// <summary>Event args for the OnTaskEdit pre-edit event. Set IsCancelled to prevent edit mode.</summary>
public class GanttEditEventArgs
{
    /// <summary>The item about to be edited.</summary>
    public object Item { get; set; } = default!;
    /// <summary>Set to true to cancel entering edit mode.</summary>
    public bool IsCancelled { get; set; }
}
