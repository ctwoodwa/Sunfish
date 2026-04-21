using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Models;

[Obsolete("Use SunfishTreeListColumn child components instead.")]
public class TreeListColumn
{
    public string Title { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string? Width { get; set; }
}

public class TreeListCommandEventArgs<TItem>
{
    public TItem Item { get; set; } = default!;
    public TItem? ParentItem { get; set; }
    public bool IsNew { get; set; }
}

/// <summary>
/// Event arguments for the <c>OnExpand</c> event on <c>SunfishTreeList</c>.
/// Set <see cref="IsCancelled"/> to <c>true</c> in the handler to prevent the expand from taking effect.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public class TreeListExpandEventArgs<TItem>
{
    /// <summary>The item whose row is being expanded.</summary>
    public TItem Item { get; set; } = default!;

    /// <summary>Set to <c>true</c> in the handler to cancel the expand.</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Event arguments for the <c>OnCollapse</c> event on <c>SunfishTreeList</c>.
/// Set <see cref="IsCancelled"/> to <c>true</c> in the handler to prevent the collapse from taking effect.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public class TreeListCollapseEventArgs<TItem>
{
    /// <summary>The item whose row is being collapsed.</summary>
    public TItem Item { get; set; } = default!;

    /// <summary>Set to <c>true</c> in the handler to cancel the collapse.</summary>
    public bool IsCancelled { get; set; }
}

public class TreeListSortEventArgs
{
    public string? Field { get; set; }
    public SortDirection? Direction { get; set; }
}

public class TreeListSelectionEventArgs<TItem>
{
    public IReadOnlyList<TItem> SelectedItems { get; set; } = Array.Empty<TItem>();
}

public class TreeListColumnReorderEventArgs
{
    /// <summary>The field name of the column that was moved.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>The original index of the column before the move.</summary>
    public int OldIndex { get; set; }

    /// <summary>The new index of the column after the move.</summary>
    public int NewIndex { get; set; }
}

/// <summary>
/// Event arguments for the <c>OnRead</c> server-side data callback on <c>SunfishTreeList</c>.
/// The consumer must set <see cref="Data"/> and <see cref="Total"/> in their handler.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public class TreeListReadEventArgs<TItem>
{
    /// <summary>The current page number (1-based).</summary>
    public int Page { get; init; }

    /// <summary>The number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>The field currently sorted on, or null if no sort is active.</summary>
    public string? SortField { get; init; }

    /// <summary>The current sort direction, or null if no sort is active.</summary>
    public SortDirection? SortDirection { get; init; }

    /// <summary>The current filter values keyed by field name.</summary>
    public IReadOnlyDictionary<string, string> FilterValues { get; init; } = new Dictionary<string, string>();

    /// <summary>Cancellation token that is cancelled if a new data request starts before this one completes.</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Set this to the data items for the current page/view. The tree list will display these.</summary>
    public IEnumerable<TItem> Data { get; set; } = [];

    /// <summary>Set this to the total number of top-level items (before paging) so the pager can calculate page count.</summary>
    public int Total { get; set; }
}

/// <summary>
/// Event arguments for the <c>OnRowDrop</c> row drag-and-drop callback on <c>SunfishTreeList</c>.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public class TreeListRowDropEventArgs<TItem>
{
    /// <summary>The dragged item.</summary>
    public TItem Item { get; set; } = default!;

    /// <summary>The item over which the dragged item was dropped, or default if dropped at the end.</summary>
    public TItem? DestinationItem { get; set; }

    /// <summary>The drop position relative to the <see cref="DestinationItem"/>.</summary>
    public Enums.TreeListDropPosition DropPosition { get; set; }

    /// <summary>The flat index where the drop occurred.</summary>
    public int DestinationIndex { get; set; }
}
