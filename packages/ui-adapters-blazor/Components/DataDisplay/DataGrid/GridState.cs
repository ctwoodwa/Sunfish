using Sunfish.Foundation.Data;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

/// <summary>
/// Represents the current state of a <see cref="SunfishDataGrid{TItem}"/>,
/// including paging, sorting, filtering, grouping, editing, and column state.
/// Can be used with <c>OnStateInit</c> and <c>OnStateChanged</c> to persist and restore state.
/// </summary>
public class GridState
{
    /// <summary>The current page number (1-based).</summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>The number of items per page.</summary>
    public int PageSize { get; set; } = 10;

    /// <summary>Active sort descriptors.</summary>
    public List<SortDescriptor> SortDescriptors { get; set; } = [];

    /// <summary>Active filter descriptors.</summary>
    public List<FilterDescriptor> FilterDescriptors { get; set; } = [];

    /// <summary>Composite filter groups (AND/OR combinations).</summary>
    public List<CompositeFilterDescriptor> CompositeFilterDescriptors { get; set; } = [];

    /// <summary>Active group descriptors.</summary>
    public List<GroupDescriptor> GroupDescriptors { get; set; } = [];

    /// <summary>The total number of items after filtering (before paging).</summary>
    public int TotalCount { get; set; }

    /// <summary>The currently selected item keys (for state persistence).</summary>
    public HashSet<object> SelectedKeys { get; set; } = [];

    /// <summary>The current search/filter text from the search box.</summary>
    public string? SearchFilter { get; set; }

    /// <summary>The item currently being edited (null if none).</summary>
    public object? EditItem { get; set; }

    /// <summary>The original item before editing began (for cancellation).</summary>
    public object? OriginalEditItem { get; set; }

    /// <summary>The item being created (null if not in add mode).</summary>
    public object? InsertedItem { get; set; }

    /// <summary>Keys of currently expanded detail rows.</summary>
    public HashSet<object> ExpandedItems { get; set; } = [];

    /// <summary>Keys of collapsed groups.</summary>
    public HashSet<string> CollapsedGroups { get; set; } = [];

    /// <summary>Persisted column states (width, order, visibility).</summary>
    public List<GridColumnState> ColumnStates { get; set; } = [];
}

/// <summary>
/// Persisted state for a single grid column.
/// </summary>
public class GridColumnState
{
    /// <summary>The column field name.</summary>
    public string Field { get; set; } = "";

    /// <summary>The column width.</summary>
    public string? Width { get; set; }

    /// <summary>The display order index.</summary>
    public int Order { get; set; }

    /// <summary>Whether the column is visible.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Whether the column exposes a column-menu trigger. Mirrors <see cref="SunfishGridColumn{TItem}.ShowColumnMenu"/>.</summary>
    public bool ShowColumnMenu { get; set; }

    /// <summary>Whether the column appears in the column chooser. Mirrors <see cref="SunfishGridColumn{TItem}.VisibleInColumnChooser"/>.</summary>
    public bool VisibleInColumnChooser { get; set; } = true;
}
