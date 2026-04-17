using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.Components.Blazor.Components.DataGrid;

/// <summary>
/// Event arguments for grid row click events (<c>OnRowClick</c>, <c>OnRowDoubleClick</c>).
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public class GridRowClickEventArgs<TItem>
{
    /// <summary>The data item for the clicked row.</summary>
    public TItem Item { get; init; } = default!;

    /// <summary>The field name of the clicked column, if available.</summary>
    public string? Field { get; init; }

    /// <summary>The original mouse event args from the browser.</summary>
    public MouseEventArgs EventArgs { get; init; } = default!;
}

/// <summary>
/// Event arguments for the <c>OnRead</c> server-side data callback.
/// The consumer must set <see cref="Data"/> and <see cref="Total"/> in their handler.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public class GridReadEventArgs<TItem>
{
    /// <summary>The data request containing sort, filter, group, and page descriptors.</summary>
    public GridState Request { get; init; } = default!;

    /// <summary>Set this to the data items for the current page/view. The grid will display these.</summary>
    public IEnumerable<TItem> Data { get; set; } = [];

    /// <summary>Set this to the total number of items (before paging) so the pager can calculate page count.</summary>
    public int Total { get; set; }

    /// <summary>Cancellation token that is cancelled if a new data request starts before this one completes.</summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Event arguments for the <c>OnRowRender</c> callback, allowing per-row CSS customization.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public class GridRowRenderEventArgs<TItem>
{
    /// <summary>The data item for the row being rendered.</summary>
    public TItem Item { get; init; } = default!;

    /// <summary>Set additional CSS class(es) to apply to this row.</summary>
    public string? Class { get; set; }

    /// <summary>Set additional inline style(s) to apply to this row.</summary>
    public string? Style { get; set; }
}

/// <summary>
/// Event arguments for the <c>OnCellRender</c> callback on <see cref="SunfishGridColumn{TItem}"/>.
/// </summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public class GridCellRenderEventArgs<TItem>
{
    /// <summary>The data item for the row containing this cell.</summary>
    public TItem Item { get; init; } = default!;

    /// <summary>The field name of the column.</summary>
    public string? Field { get; init; }

    /// <summary>The cell value.</summary>
    public object? Value { get; init; }

    /// <summary>Set additional CSS class(es) to apply to this cell.</summary>
    public string? Class { get; set; }

    /// <summary>Set additional inline style(s) to apply to this cell.</summary>
    public string? Style { get; set; }
}

/// <summary>
/// Event arguments for the <c>OnStateChanged</c> event.
/// </summary>
public class GridStateChangedEventArgs
{
    /// <summary>The name of the property that changed (e.g. "Page", "Sort", "Filter").</summary>
    public string PropertyName { get; init; } = "";

    /// <summary>A snapshot of the current grid state.</summary>
    public GridState State { get; init; } = default!;
}

/// <summary>
/// Represents a group of items in a grouped data grid.
/// </summary>
public class GridGroupRow<TItem>
{
    /// <summary>The field name this group is grouped by.</summary>
    public string Field { get; init; } = "";

    /// <summary>The group key value.</summary>
    public object? Key { get; init; }

    /// <summary>Display text for the group key.</summary>
    public string KeyText => Key?.ToString() ?? "(null)";

    /// <summary>The items in this group.</summary>
    public List<TItem> Items { get; init; } = [];

    /// <summary>Number of items in this group.</summary>
    public int Count => Items.Count;

    /// <summary>The nesting depth (0 = top-level group).</summary>
    public int Depth { get; init; }

    /// <summary>A unique key for this group used for collapse state tracking.</summary>
    public string GroupKey => $"{Field}:{Key}";

    /// <summary>Child groups when multiple grouping levels are applied.</summary>
    public List<GridGroupRow<TItem>> ChildGroups { get; init; } = [];

    /// <summary>Whether this group has child groups (multi-level grouping).</summary>
    public bool HasChildGroups => ChildGroups.Count > 0;
}

/// <summary>
/// Context provided to <c>GroupHeaderTemplate</c> and <c>GroupFooterTemplate</c>.
/// </summary>
public class GridGroupHeaderContext<TItem>
{
    /// <summary>The field name being grouped.</summary>
    public string Field { get; init; } = "";

    /// <summary>The group key value.</summary>
    public object? Value { get; init; }

    /// <summary>The items in this group.</summary>
    public IReadOnlyList<TItem> Items { get; init; } = [];

    /// <summary>Number of items in this group.</summary>
    public int Count => Items.Count;

    /// <summary>The nesting depth (0 = top-level).</summary>
    public int Depth { get; init; }

    /// <summary>Whether this group is collapsed.</summary>
    public bool IsCollapsed { get; init; }

    /// <summary>Computes the sum of a decimal property across items in this group.</summary>
    public decimal Sum(Func<TItem, decimal> selector) => Items.Sum(selector);

    /// <summary>Computes the average of a decimal property across items in this group.</summary>
    public decimal Average(Func<TItem, decimal> selector) => Items.Count > 0 ? Items.Average(selector) : 0;

    /// <summary>Computes the sum of an int property across items in this group.</summary>
    public int Sum(Func<TItem, int> selector) => Items.Sum(selector);

    /// <summary>Computes the average of an int property across items in this group.</summary>
    public double Average(Func<TItem, int> selector) => Items.Count > 0 ? Items.Average(selector) : 0;

    /// <summary>Gets the minimum value of a property across items in this group.</summary>
    public TResult Min<TResult>(Func<TItem, TResult> selector) => Items.Min(selector)!;

    /// <summary>Gets the maximum value of a property across items in this group.</summary>
    public TResult Max<TResult>(Func<TItem, TResult> selector) => Items.Max(selector)!;
}

/// <summary>
/// Event arguments for export lifecycle events (OnBeforeExport, OnAfterExport).
/// </summary>
public class GridExportEventArgs
{
    /// <summary>The export format (e.g. "csv").</summary>
    public string Format { get; init; } = "csv";

    /// <summary>Set to true to cancel the export.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>The exported data (populated in OnAfterExport).</summary>
    public string? Data { get; set; }

    /// <summary>The number of rows exported.</summary>
    public int RowCount { get; set; }
}

/// <summary>Event arguments for row drag-and-drop reorder.</summary>
/// <typeparam name="TItem">The row data type.</typeparam>
public class GridRowDropEventArgs<TItem>
{
    /// <summary>The item being dragged.</summary>
    public TItem Item { get; init; } = default!;

    /// <summary>The item at the drop destination.</summary>
    public TItem? DestinationItem { get; init; }

    /// <summary>Index in the displayed data where the row was dropped.</summary>
    public int DestinationIndex { get; init; }

    /// <summary>Whether dropped before or after the destination item.</summary>
    public GridRowDropPosition DropPosition { get; init; }

    /// <summary>Set to true to cancel the drop.</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>Whether a row was dropped before or after the destination item.</summary>
public enum GridRowDropPosition
{
    /// <summary>Dropped before the destination item.</summary>
    Before,

    /// <summary>Dropped after the destination item.</summary>
    After
}
