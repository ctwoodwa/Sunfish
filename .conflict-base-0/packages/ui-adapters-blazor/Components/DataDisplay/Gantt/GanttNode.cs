using System.Collections.Generic;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Internal tree node used by <see cref="SunfishGantt{TItem}"/> to represent
/// a hierarchical view built from flat ParentId-linked data.
/// </summary>
internal sealed class GanttNode<TItem> where TItem : class
{
    public TItem Item { get; set; } = default!;
    public object? Id { get; set; }
    public object? ParentId { get; set; }
    public int Depth { get; set; }
    public List<GanttNode<TItem>> Children { get; } = new();
    public bool IsExpanded { get; set; } = true;
    public GanttNode<TItem>? Parent { get; set; }
    internal int OriginalIndex { get; set; }

    /// <summary>Computed start date for summary (parent) tasks. Null if leaf.</summary>
    internal DateTime? ComputedStart { get; set; }
    /// <summary>Computed end date for summary (parent) tasks. Null if leaf.</summary>
    internal DateTime? ComputedEnd { get; set; }
    /// <summary>Computed percent complete for summary (parent) tasks. Null if leaf.</summary>
    internal double? ComputedPercentComplete { get; set; }
}
