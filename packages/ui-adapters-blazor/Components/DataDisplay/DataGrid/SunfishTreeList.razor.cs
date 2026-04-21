using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;

#pragma warning disable ASP0006 // seq++ is intentional in manual RenderTreeBuilder code — each render fragment builds a complete tree from scratch

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

public partial class SunfishTreeList<TItem> : SunfishComponentBase, IColumnHost, ITreeListEditController
{
    [Parameter] public IEnumerable<TItem> Data { get; set; } = Enumerable.Empty<TItem>();
    [Parameter] public string? IdField { get; set; }
    [Parameter] public string? ParentIdField { get; set; }
    [Parameter] public string? ItemsField { get; set; }
    [Parameter] public string? HasChildrenField { get; set; }
    [Parameter] public bool Sortable { get; set; }
    [Parameter] public EventCallback<TreeListSortEventArgs> OnSortChanged { get; set; }

    /// <summary>
    /// Fires when the user expands a row. The handler receives a
    /// <see cref="TreeListExpandEventArgs{TItem}"/> and may set
    /// <c>IsCancelled = true</c> to prevent the expand.
    /// </summary>
    [Parameter] public EventCallback<TreeListExpandEventArgs<TItem>> OnExpand { get; set; }

    /// <summary>
    /// Fires when the user collapses a row. The handler receives a
    /// <see cref="TreeListCollapseEventArgs{TItem}"/> and may set
    /// <c>IsCancelled = true</c> to prevent the collapse.
    /// </summary>
    [Parameter] public EventCallback<TreeListCollapseEventArgs<TItem>> OnCollapse { get; set; }

    /// <summary>
    /// Two-way bindable set of currently expanded items. When null, expansion state is tracked internally.
    /// When bound, this is the source of truth — consumers may pre-expand or programmatically control
    /// expansion without needing a <c>@ref</c> to the tree list.
    /// </summary>
    [Parameter] public HashSet<TItem>? ExpandedItems { get; set; }

    /// <summary>Raised when <see cref="ExpandedItems"/> changes after an expand or collapse.</summary>
    [Parameter] public EventCallback<HashSet<TItem>> ExpandedItemsChanged { get; set; }

    [Parameter] public TreeListSelectionMode SelectionMode { get; set; } = TreeListSelectionMode.None;
    [Parameter] public IReadOnlyList<TItem>? SelectedItems { get; set; }
    [Parameter] public EventCallback<IReadOnlyList<TItem>> SelectedItemsChanged { get; set; }
    [Parameter] public EventCallback<TreeListSelectionEventArgs<TItem>> OnSelectionChanged { get; set; }
    [Parameter] public TreeListFilterMode FilterMode { get; set; } = TreeListFilterMode.None;
#pragma warning disable CS0618
    [Parameter][Obsolete("Use <SunfishTreeListColumn> child components instead.")]
    public List<TreeListColumn>? Columns { get; set; }
#pragma warning restore CS0618
    [Parameter] public EventCallback<TItem> OnRowClick { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public TreeListEditMode EditMode { get; set; } = TreeListEditMode.None;
    [Parameter] public EventCallback<TreeListCommandEventArgs<TItem>> OnCreate { get; set; }
    [Parameter] public EventCallback<TreeListCommandEventArgs<TItem>> OnUpdate { get; set; }
    [Parameter] public EventCallback<TreeListCommandEventArgs<TItem>> OnDelete { get; set; }

    /// <summary>When true, keyboard navigation is enabled on the tree list (Arrow keys, Enter, Escape, Home, End).</summary>
    [Parameter] public bool Navigable { get; set; }

    /// <summary>When true, columns can be resized by dragging the right edge of header cells.</summary>
    [Parameter] public bool Resizable { get; set; }

    /// <summary>When true, columns can be reordered by dragging header cells.</summary>
    [Parameter] public bool Reorderable { get; set; }

    /// <summary>Fires after a column is reordered via drag-and-drop.</summary>
    [Parameter] public EventCallback<TreeListColumnReorderEventArgs> OnColumnReordered { get; set; }

    /// <summary>When true, a pager is shown below the tree list. Pagination applies to top-level items only.</summary>
    [Parameter] public bool Pageable { get; set; }

    /// <summary>The number of top-level items to display per page. Default is 10.</summary>
    [Parameter] public int PageSize { get; set; } = 10;

    /// <summary>The current page number (1-based). Supports two-way binding.</summary>
    [Parameter] public int Page { get; set; } = 1;

    /// <summary>Fires when the current page changes.</summary>
    [Parameter] public EventCallback<int> PageChanged { get; set; }

    /// <summary>
    /// Server-side data callback. When set, the component delegates sorting, filtering, and paging
    /// to the consumer instead of performing client-side operations. The consumer must set
    /// <see cref="TreeListReadEventArgs{TItem}.Data"/> and <see cref="TreeListReadEventArgs{TItem}.Total"/>.
    /// </summary>
    [Parameter] public EventCallback<TreeListReadEventArgs<TItem>> OnRead { get; set; }

    /// <summary>When true, uses Blazor's built-in Virtualize component for the row list instead of rendering all rows.</summary>
    [Parameter] public bool EnableVirtualization { get; set; }

    /// <summary>The pixel height of each row, used by the Virtualize component's ItemSize. Default is 36.</summary>
    [Parameter] public int ItemHeight { get; set; } = 36;

    /// <summary>When true, data rows are draggable for reordering within the tree.</summary>
    [Parameter] public bool RowDraggable { get; set; }

    /// <summary>Fires when a row is dropped onto a new position. The consumer must update the data source.</summary>
    [Parameter] public EventCallback<TreeListRowDropEventArgs<TItem>> OnRowDropped { get; set; }

    private List<TreeListNode> _rootItems = new();
    private HashSet<string> _expandedIds = new();
    private string? _sortField;
    private SortDirection? _sortDirection;
    private HashSet<TItem> _selectedItemsSet = new();
    private readonly Dictionary<string, string> _filterValues = new();
    internal TItem? _editingItem;
    internal Dictionary<string, object?> _editingValues = new();
    internal bool _isNewItem;
    private readonly List<SunfishColumnBase> _registeredColumns = new();
    private int _totalItemCount;
    private CancellationTokenSource? _readCts;
    internal record TreeListNode(string Id, TItem Item, List<TreeListNode> Children, bool HasChildren);

    /// <summary>Total number of pages based on top-level item count and page size.</summary>
    internal int TotalPages => Pageable && PageSize > 0 ? Math.Max(1, (int)Math.Ceiling((double)_totalItemCount / PageSize)) : 1;

    void IColumnHost.RegisterColumn(SunfishColumnBase column)
    {
        if (!_registeredColumns.Contains(column)) { _registeredColumns.Add(column); InvokeAsync(StateHasChanged); }
    }

    void IColumnHost.UnregisterColumn(SunfishColumnBase column)
    {
        if (_registeredColumns.Remove(column)) InvokeAsync(StateHasChanged);
    }

    internal List<IColumnDescriptor> EffectiveColumns
    {
        get
        {
            if (_registeredColumns.Count > 0) return _registeredColumns.Cast<IColumnDescriptor>().ToList();
#pragma warning disable CS0618
            if (Columns is { Count: > 0 }) return Columns.Select(c => (IColumnDescriptor)new LegacyColumnAdapter(c)).ToList();
#pragma warning restore CS0618
            return new();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (SelectedItems is not null) _selectedItemsSet = new HashSet<TItem>(SelectedItems);

        if (OnRead.HasDelegate)
        {
            await LoadServerDataAsync();
        }
        else
        {
            _rootItems = BuildTree();
        }

        // Mirror the caller-owned ExpandedItems set into the internal _expandedIds index.
        if (ExpandedItems is not null && ExpandedItems.Count > 0)
        {
            RehydrateExpandedIdsFromItems(_rootItems);
        }
    }

    private void RehydrateExpandedIdsFromItems(List<TreeListNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (ExpandedItems!.Contains(node.Item)) _expandedIds.Add(node.Id);
            if (node.Children.Count > 0) RehydrateExpandedIdsFromItems(node.Children);
        }
    }

    /// <summary>Index of the column that carries the expand chevron (first with <c>Expandable="true"</c>, else 0).</summary>
    internal int ExpandableColumnIndex
    {
        get
        {
            var cols = OrderedColumns;
            for (var i = 0; i < cols.Count; i++)
            {
                if (cols[i] is SunfishTreeListColumn { Expandable: true }) return i;
            }
            return 0;
        }
    }

    private async Task LoadServerDataAsync()
    {
        _readCts?.Cancel();
        var cts = new CancellationTokenSource();
        _readCts = cts;

        var args = new TreeListReadEventArgs<TItem>
        {
            Page = Page,
            PageSize = PageSize,
            SortField = _sortField,
            SortDirection = _sortDirection,
            FilterValues = new Dictionary<string, string>(_filterValues),
            CancellationToken = cts.Token
        };

        await OnRead.InvokeAsync(args);

        if (cts.Token.IsCancellationRequested) return;

        _totalItemCount = args.Total;
        var serverItems = args.Data.ToList();
        int idx = 0;
        _rootItems = serverItems.Select(i => new TreeListNode($"srv-{idx++}", i, new(), false)).ToList();
    }

    private List<TreeListNode> BuildTree()
    {
        var items = Data.ToList();
        if (!items.Any()) return new();
        items = ApplyFilter(items);
        items = ApplySort(items);

        List<TreeListNode> nodes;
        if (!string.IsNullOrEmpty(ItemsField)) nodes = BuildHierarchical(items.Cast<object>(), 0);
        else if (!string.IsNullOrEmpty(IdField) && !string.IsNullOrEmpty(ParentIdField)) nodes = BuildFlat(items);
        else { int idx = 0; nodes = items.Select(i => new TreeListNode($"auto-{idx++}", i, new(), false)).ToList(); }

        _totalItemCount = nodes.Count;

        if (Pageable && PageSize > 0)
        {
            var page = Math.Max(1, Math.Min(Page, TotalPages));
            nodes = nodes.Skip((page - 1) * PageSize).Take(PageSize).ToList();
        }

        return nodes;
    }

    private List<TItem> ApplySort(List<TItem> items)
    {
        if (string.IsNullOrEmpty(_sortField) || _sortDirection is null) return items;
        var prop = typeof(TItem).GetProperty(_sortField);
        if (prop is null) return items;
        return _sortDirection == SortDirection.Ascending
            ? items.OrderBy(i => prop.GetValue(i)).ToList()
            : items.OrderByDescending(i => prop.GetValue(i)).ToList();
    }

    private bool IsColumnSortable(IColumnDescriptor col)
    {
        if (col is SunfishColumnBase mcb && mcb.Sortable.HasValue) return mcb.Sortable.Value;
        return Sortable;
    }

    private async Task HandleHeaderClick(IColumnDescriptor col)
    {
        if (!IsColumnSortable(col)) return;
        var field = col.Field;
        if (_sortField == field)
        {
            _sortDirection = _sortDirection switch
            {
                SortDirection.Ascending => SortDirection.Descending,
                SortDirection.Descending => null,
                _ => SortDirection.Ascending
            };
            if (_sortDirection is null) _sortField = null;
        }
        else { _sortField = field; _sortDirection = SortDirection.Ascending; }

        if (OnRead.HasDelegate)
        {
            await LoadServerDataAsync();
        }
        else
        {
            _rootItems = BuildTree();
        }

        if (OnSortChanged.HasDelegate)
            await OnSortChanged.InvokeAsync(new TreeListSortEventArgs { Field = _sortField, Direction = _sortDirection });
    }

    private List<TreeListNode> BuildHierarchical(IEnumerable<object> items, int depth)
    {
        var result = new List<TreeListNode>();
        int idx = 0;
        foreach (var item in items)
        {
            var id = GetProp(item, IdField) ?? $"h-{depth}-{idx++}";
            var children = new List<TreeListNode>();
            var childItems = item.GetType().GetProperty(ItemsField!)?.GetValue(item);
            if (childItems is System.Collections.IEnumerable en)
            {
                var list = en.Cast<object>().ToList();
                if (list.Any()) children = BuildHierarchical(list, depth + 1);
            }
            var hasKids = children.Any();
            if (!string.IsNullOrEmpty(HasChildrenField))
            {
                var v = item.GetType().GetProperty(HasChildrenField)?.GetValue(item);
                if (v is bool b) hasKids = b;
            }
            result.Add(new TreeListNode(id, (TItem)item, children, hasKids));
        }
        return result;
    }

    private List<TreeListNode> BuildFlat(List<TItem> items)
    {
        var lookup = new Dictionary<string, TreeListNode>();
        var roots = new List<TreeListNode>();
        foreach (var item in items)
        {
            var id = GetProp(item!, IdField) ?? "";
            var hasKids = false;
            if (!string.IsNullOrEmpty(HasChildrenField))
            {
                var v = item!.GetType().GetProperty(HasChildrenField)?.GetValue(item);
                if (v is bool b) hasKids = b;
            }
            lookup[id] = new TreeListNode(id, item, new(), hasKids);
        }
        foreach (var item in items)
        {
            var id = GetProp(item!, IdField) ?? "";
            var parentId = GetProp(item!, ParentIdField);
            if (string.IsNullOrEmpty(parentId) || !lookup.ContainsKey(parentId)) roots.Add(lookup[id]);
            else lookup[parentId].Children.Add(lookup[id]);
        }
        return roots;
    }

    private string? GetProp(object item, string? propName)
    {
        if (string.IsNullOrEmpty(propName)) return null;
        return item.GetType().GetProperty(propName)?.GetValue(item)?.ToString();
    }

    private List<TItem> ApplyFilter(List<TItem> items)
    {
        if (FilterMode == TreeListFilterMode.None || !_filterValues.Any(kv => !string.IsNullOrEmpty(kv.Value))) return items;
        var activeFilters = _filterValues.Where(kv => !string.IsNullOrEmpty(kv.Value)).ToList();
        if (!activeFilters.Any()) return items;
        if (!string.IsNullOrEmpty(IdField) && !string.IsNullOrEmpty(ParentIdField)) return ApplyFilterWithHierarchy(items, activeFilters);
        return items.Where(item => MatchesAllFilters(item, activeFilters)).ToList();
    }

    private List<TItem> ApplyFilterWithHierarchy(List<TItem> items, List<KeyValuePair<string, string>> activeFilters)
    {
        var matchingIds = new HashSet<string>();
        var parentIds = new HashSet<string>();
        foreach (var item in items)
        {
            if (MatchesAllFilters(item, activeFilters))
            {
                var id = GetProp(item!, IdField);
                if (id != null) matchingIds.Add(id);
                var parentId = GetProp(item!, ParentIdField);
                while (!string.IsNullOrEmpty(parentId))
                {
                    parentIds.Add(parentId);
                    var parent = items.FirstOrDefault(i => GetProp(i!, IdField) == parentId);
                    if (parent == null) break;
                    parentId = GetProp(parent!, ParentIdField);
                }
            }
        }
        return items.Where(item => { var id = GetProp(item!, IdField); return (id != null && matchingIds.Contains(id)) || (id != null && parentIds.Contains(id)); }).ToList();
    }

    private bool MatchesAllFilters(TItem item, List<KeyValuePair<string, string>> activeFilters)
    {
        foreach (var filter in activeFilters)
        {
            var prop = typeof(TItem).GetProperty(filter.Key);
            if (prop is null) continue;
            var value = prop.GetValue(item)?.ToString() ?? string.Empty;
            if (!value.Contains(filter.Value, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private bool IsColumnFilterable(IColumnDescriptor col)
    {
        if (col is SunfishColumnBase mcb && mcb.Filterable.HasValue) return mcb.Filterable.Value;
        return true;
    }

    private async Task HandleFilterInput(string field, string value)
    {
        if (string.IsNullOrEmpty(value)) _filterValues.Remove(field);
        else _filterValues[field] = value;

        if (OnRead.HasDelegate)
        {
            await LoadServerDataAsync();
        }
        else
        {
            _rootItems = BuildTree();
        }
    }

    private bool IsItemSelected(TItem item) => _selectedItemsSet.Contains(item);

    private async Task HandleRowClick(TItem item)
    {
        if (OnRowClick.HasDelegate) await OnRowClick.InvokeAsync(item);
        if (SelectionMode == TreeListSelectionMode.None) return;
        if (SelectionMode == TreeListSelectionMode.Single)
        {
            if (_selectedItemsSet.Contains(item)) _selectedItemsSet.Clear();
            else { _selectedItemsSet.Clear(); _selectedItemsSet.Add(item); }
        }
        else { if (!_selectedItemsSet.Remove(item)) _selectedItemsSet.Add(item); }
        var selectedList = _selectedItemsSet.ToList().AsReadOnly();
        if (SelectedItemsChanged.HasDelegate) await SelectedItemsChanged.InvokeAsync(selectedList);
        if (OnSelectionChanged.HasDelegate) await OnSelectionChanged.InvokeAsync(new TreeListSelectionEventArgs<TItem> { SelectedItems = selectedList });
    }

    private int _currentRenderRowIndex;

    private RenderFragment RenderRows(List<TreeListNode> nodes, int depth) => builder =>
    {
        int seq = 0;
        var columns = OrderedColumns;
        var expandCol = ExpandableColumnIndex;
        foreach (var node in nodes)
        {
            var isExpanded = _expandedIds.Contains(node.Id);
            var hasKids = node.Children.Any() || node.HasChildren;
            var nodeId = node.Id; var nodeItem = node.Item;
            var isSelected = IsItemSelected(node.Item);
            var isEditingRow = IsEditing(node.Item);
            var isFocused = Navigable && _focusedRowIndex == _currentRenderRowIndex;
            var rowIndex = _currentRenderRowIndex;
            _currentRenderRowIndex++;
            var rowCss = isEditingRow ? "mar-treelist__row mar-treelist__row--editing" : isSelected ? "mar-treelist__row mar-treelist__row--selected" : "mar-treelist__row";
            if (isFocused) rowCss += " mar-treelist__row--focused";
            if (RowDraggable && _rowDragSourceIndex == rowIndex) rowCss += " mar-treelist__row--dragging";
            if (RowDraggable && _rowDragOverIndex == rowIndex && _rowDragSourceIndex != rowIndex) rowCss += " mar-treelist__row--drop-target";
            builder.OpenElement(seq++, "tr");
            builder.AddAttribute(seq++, "class", rowCss);
            builder.AddAttribute(seq++, "role", "row");
            builder.AddAttribute(seq++, "aria-level", depth + 1);
            if (hasKids) builder.AddAttribute(seq++, "aria-expanded", isExpanded ? "true" : "false");
            if (isSelected) builder.AddAttribute(seq++, "aria-selected", "true");
            if (RowDraggable) builder.AddAttribute(seq++, "draggable", "true");
            builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => HandleRowClick(node.Item)));
            if (EditMode == TreeListEditMode.Inline) builder.AddAttribute(seq++, "ondblclick", EventCallback.Factory.Create(this, () => HandleRowDoubleClick(node.Item)));
            if (RowDraggable)
            {
                var ri = rowIndex; var item = node.Item;
                builder.AddAttribute(seq++, "ondragstart", EventCallback.Factory.Create<DragEventArgs>(this, e => OnRowDragStart(e, ri)));
                builder.AddAttribute(seq++, "ondragover", EventCallback.Factory.Create<DragEventArgs>(this, e => OnRowDragOver(e, ri)));
                builder.AddAttribute(seq++, "ondragover:preventDefault", true);
                builder.AddAttribute(seq++, "ondrop", EventCallback.Factory.Create<DragEventArgs>(this, e => OnRowDrop(e, ri, item)));
                builder.AddAttribute(seq++, "ondragend", EventCallback.Factory.Create<DragEventArgs>(this, e => OnRowDragEnd(e)));
            }
            for (var ci = 0; ci < columns.Count; ci++)
            {
                var col = columns[ci]; var cellTemplate = (col as SunfishTreeListColumn)?.Template;
                builder.OpenElement(seq++, "td"); builder.AddAttribute(seq++, "class", "mar-treelist__td"); builder.AddAttribute(seq++, "role", "gridcell");
                var tdStyle = GetColumnWidthStyle(col);
                if (!string.IsNullOrEmpty(tdStyle)) builder.AddAttribute(seq++, "style", tdStyle);
                if (isEditingRow)
                {
                    var fieldName = col.Field; var currentVal = GetEditValue(fieldName)?.ToString() ?? "";
                    var editTpl = (col as SunfishTreeListColumn)?.EditTemplate;
                    if (ci == expandCol)
                    {
                        builder.OpenElement(seq++, "span"); builder.AddAttribute(seq++, "style", $"padding-left: {depth * 20}px; display: inline-flex; align-items: center; gap: 4px;");
                        builder.OpenElement(seq++, "span"); builder.AddAttribute(seq++, "style", "width: 20px;"); builder.CloseElement();
                        if (editTpl is not null) { builder.AddContent(seq++, editTpl((object)node.Item!)); }
                        else
                        {
                            builder.OpenElement(seq++, "input"); builder.AddAttribute(seq++, "type", "text"); builder.AddAttribute(seq++, "class", "mar-treelist__edit-input");
                            builder.AddAttribute(seq++, "value", currentVal);
                            builder.AddAttribute(seq++, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e => SetEditValue(fieldName, e.Value?.ToString() ?? "")));
                            builder.CloseElement();
                        }
                        builder.CloseElement();
                    }
                    else if (editTpl is not null)
                    {
                        builder.AddContent(seq++, editTpl((object)node.Item!));
                    }
                    else
                    {
                        builder.OpenElement(seq++, "input"); builder.AddAttribute(seq++, "type", "text"); builder.AddAttribute(seq++, "class", "mar-treelist__edit-input");
                        builder.AddAttribute(seq++, "value", currentVal);
                        builder.AddAttribute(seq++, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e => SetEditValue(fieldName, e.Value?.ToString() ?? "")));
                        builder.CloseElement();
                    }
                }
                else if (ci == expandCol)
                {
                    builder.OpenElement(seq++, "span"); builder.AddAttribute(seq++, "style", $"padding-left: {depth * 20}px; display: inline-flex; align-items: center; gap: 4px;");
                    if (hasKids) { builder.OpenElement(seq++, "button"); builder.AddAttribute(seq++, "type", "button"); builder.AddAttribute(seq++, "class", "mar-tree-item__toggle"); builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => ToggleExpand(nodeId, nodeItem))); builder.AddEventStopPropagationAttribute(seq++, "onclick", true); builder.AddContent(seq++, isExpanded ? "\u25BC" : "\u25B6"); builder.CloseElement(); }
                    else { builder.OpenElement(seq++, "span"); builder.AddAttribute(seq++, "style", "width: 20px;"); builder.CloseElement(); }
                    if (cellTemplate is not null) builder.AddContent(seq++, cellTemplate((object)node.Item!));
                    else builder.AddContent(seq++, col.GetDisplayValue(node.Item));
                    builder.CloseElement();
                }
                else
                {
                    if (cellTemplate is not null) builder.AddContent(seq++, cellTemplate((object)node.Item!));
                    else builder.AddContent(seq++, col.GetDisplayValue(node.Item));
                }
                builder.CloseElement();
            }
            if (EditMode == TreeListEditMode.Inline)
            {
                builder.OpenElement(seq++, "td"); builder.AddAttribute(seq++, "class", "mar-treelist__td mar-treelist__td--commands"); builder.AddAttribute(seq++, "role", "gridcell");
                if (isEditingRow)
                {
                    builder.OpenElement(seq++, "button"); builder.AddAttribute(seq++, "type", "button"); builder.AddAttribute(seq++, "class", "mar-treelist__cmd-btn mar-treelist__cmd-btn--save"); builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, SaveEditInternalAsync)); builder.AddEventStopPropagationAttribute(seq++, "onclick", true); builder.AddContent(seq++, "Save"); builder.CloseElement();
                    builder.OpenElement(seq++, "button"); builder.AddAttribute(seq++, "type", "button"); builder.AddAttribute(seq++, "class", "mar-treelist__cmd-btn mar-treelist__cmd-btn--cancel"); builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, CancelEditInternal)); builder.AddEventStopPropagationAttribute(seq++, "onclick", true); builder.AddContent(seq++, "Cancel"); builder.CloseElement();
                }
                else
                {
                    builder.OpenElement(seq++, "button"); builder.AddAttribute(seq++, "type", "button"); builder.AddAttribute(seq++, "class", "mar-treelist__cmd-btn mar-treelist__cmd-btn--delete"); builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => DeleteItem(node.Item))); builder.AddEventStopPropagationAttribute(seq++, "onclick", true); builder.AddContent(seq++, "Delete"); builder.CloseElement();
                }
                builder.CloseElement();
            }
            builder.CloseElement();
            if (hasKids && isExpanded) builder.AddContent(seq++, RenderRows(node.Children, depth + 1));
        }
    };

    /// <summary>Renders a single row for use with virtualization (flattened visible rows).</summary>
    internal RenderFragment RenderSingleRow(TreeListNode node, int depth) => builder =>
    {
        int seq = 0;
        var columns = OrderedColumns;
        var expandCol = ExpandableColumnIndex;
        var isExpanded = _expandedIds.Contains(node.Id);
        var hasKids = node.Children.Any() || node.HasChildren;
        var nodeId = node.Id; var nodeItem = node.Item;
        var isSelected = IsItemSelected(node.Item);
        var isEditingRow = IsEditing(node.Item);
        var rowCss = isEditingRow ? "mar-treelist__row mar-treelist__row--editing" : isSelected ? "mar-treelist__row mar-treelist__row--selected" : "mar-treelist__row";
        builder.OpenElement(seq++, "tr");
        builder.AddAttribute(seq++, "class", rowCss);
        builder.AddAttribute(seq++, "style", $"height:{ItemHeight}px;");
        builder.AddAttribute(seq++, "role", "row");
        builder.AddAttribute(seq++, "aria-level", depth + 1);
        if (hasKids) builder.AddAttribute(seq++, "aria-expanded", isExpanded ? "true" : "false");
        if (isSelected) builder.AddAttribute(seq++, "aria-selected", "true");
        if (RowDraggable) builder.AddAttribute(seq++, "draggable", "true");
        builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => HandleRowClick(node.Item)));
        if (EditMode == TreeListEditMode.Inline) builder.AddAttribute(seq++, "ondblclick", EventCallback.Factory.Create(this, () => HandleRowDoubleClick(node.Item)));
        for (var ci = 0; ci < columns.Count; ci++)
        {
            var col = columns[ci]; var cellTemplate = (col as SunfishTreeListColumn)?.Template;
            builder.OpenElement(seq++, "td"); builder.AddAttribute(seq++, "class", "mar-treelist__td"); builder.AddAttribute(seq++, "role", "gridcell");
            var tdStyle = GetColumnWidthStyle(col);
            if (!string.IsNullOrEmpty(tdStyle)) builder.AddAttribute(seq++, "style", tdStyle);
            if (ci == expandCol)
            {
                builder.OpenElement(seq++, "span"); builder.AddAttribute(seq++, "style", $"padding-left: {depth * 20}px; display: inline-flex; align-items: center; gap: 4px;");
                if (hasKids) { builder.OpenElement(seq++, "button"); builder.AddAttribute(seq++, "type", "button"); builder.AddAttribute(seq++, "class", "mar-tree-item__toggle"); builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () => ToggleExpand(nodeId, nodeItem))); builder.AddEventStopPropagationAttribute(seq++, "onclick", true); builder.AddContent(seq++, isExpanded ? "\u25BC" : "\u25B6"); builder.CloseElement(); }
                else { builder.OpenElement(seq++, "span"); builder.AddAttribute(seq++, "style", "width: 20px;"); builder.CloseElement(); }
                if (cellTemplate is not null) builder.AddContent(seq++, cellTemplate((object)node.Item!));
                else builder.AddContent(seq++, col.GetDisplayValue(node.Item));
                builder.CloseElement();
            }
            else
            {
                if (cellTemplate is not null) builder.AddContent(seq++, cellTemplate((object)node.Item!));
                else builder.AddContent(seq++, col.GetDisplayValue(node.Item));
            }
            builder.CloseElement();
        }
        builder.CloseElement();
    };

    private RenderFragment RenderEditRow(TItem item, int depth) => builder =>
    {
        int seq = 0; var columns = OrderedColumns;
        builder.OpenElement(seq++, "tr"); builder.AddAttribute(seq++, "class", "mar-treelist__row mar-treelist__row--editing mar-treelist__row--new"); builder.AddAttribute(seq++, "role", "row"); builder.AddAttribute(seq++, "aria-level", 1);
        for (var ci = 0; ci < columns.Count; ci++)
        {
            var col = columns[ci]; var fieldName = col.Field; var currentVal = GetEditValue(fieldName)?.ToString() ?? "";
            builder.OpenElement(seq++, "td"); builder.AddAttribute(seq++, "class", "mar-treelist__td"); builder.AddAttribute(seq++, "role", "gridcell");
            builder.OpenElement(seq++, "input"); builder.AddAttribute(seq++, "type", "text"); builder.AddAttribute(seq++, "class", "mar-treelist__edit-input"); builder.AddAttribute(seq++, "value", currentVal);
            builder.AddAttribute(seq++, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e => SetEditValue(fieldName, e.Value?.ToString() ?? "")));
            builder.CloseElement(); builder.CloseElement();
        }
        if (EditMode == TreeListEditMode.Inline)
        {
            builder.OpenElement(seq++, "td"); builder.AddAttribute(seq++, "class", "mar-treelist__td mar-treelist__td--commands"); builder.AddAttribute(seq++, "role", "gridcell");
            builder.OpenElement(seq++, "button"); builder.AddAttribute(seq++, "type", "button"); builder.AddAttribute(seq++, "class", "mar-treelist__cmd-btn mar-treelist__cmd-btn--save"); builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, SaveEditInternalAsync)); builder.AddContent(seq++, "Save"); builder.CloseElement();
            builder.OpenElement(seq++, "button"); builder.AddAttribute(seq++, "type", "button"); builder.AddAttribute(seq++, "class", "mar-treelist__cmd-btn mar-treelist__cmd-btn--cancel"); builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, CancelEditInternal)); builder.AddContent(seq++, "Cancel"); builder.CloseElement();
            builder.CloseElement();
        }
        builder.CloseElement();
    };

    private async Task ToggleExpand(string id, TItem item)
    {
        if (_expandedIds.Contains(id))
        {
            var args = new TreeListCollapseEventArgs<TItem> { Item = item };
            if (OnCollapse.HasDelegate) await OnCollapse.InvokeAsync(args);
            if (args.IsCancelled) return;
            _expandedIds.Remove(id);
            await SyncExpandedItemsAsync(item, add: false);
        }
        else
        {
            var args = new TreeListExpandEventArgs<TItem> { Item = item };
            if (OnExpand.HasDelegate) await OnExpand.InvokeAsync(args);
            if (args.IsCancelled) return;
            _expandedIds.Add(id);
            await SyncExpandedItemsAsync(item, add: true);
        }
    }

    private async Task SyncExpandedItemsAsync(TItem item, bool add)
    {
        if (ExpandedItems is null) return;
        if (add) ExpandedItems.Add(item); else ExpandedItems.Remove(item);
        if (ExpandedItemsChanged.HasDelegate) await ExpandedItemsChanged.InvokeAsync(ExpandedItems);
    }

    internal bool IsEditing(TItem item) => EditMode == TreeListEditMode.Inline && _editingItem is not null && EqualityComparer<TItem>.Default.Equals(_editingItem, item);

    private void BeginEdit(TItem item)
    {
        if (EditMode != TreeListEditMode.Inline) return;
        _editingItem = item; _isNewItem = false; _editingValues = new Dictionary<string, object?>();
        foreach (var col in EffectiveColumns) { var prop = typeof(TItem).GetProperty(col.Field); if (prop is not null) _editingValues[col.Field] = prop.GetValue(item); }
    }

    private async Task HandleRowDoubleClick(TItem item)
    {
        if (EditMode == TreeListEditMode.Inline) { BeginEdit(item); await InvokeAsync(StateHasChanged); }
    }

    internal void SetEditValue(string field, object? value) => _editingValues[field] = value;
    internal object? GetEditValue(string field) => _editingValues.TryGetValue(field, out var val) ? val : null;

    async Task ITreeListEditController.BeginAddAsync()
    {
        if (EditMode != TreeListEditMode.Inline) return;
        var newItem = Activator.CreateInstance<TItem>(); _editingItem = newItem; _isNewItem = true; _editingValues = new Dictionary<string, object?>();
        foreach (var col in EffectiveColumns) { var prop = typeof(TItem).GetProperty(col.Field); if (prop is not null) _editingValues[col.Field] = prop.GetValue(newItem); }
        await InvokeAsync(StateHasChanged);
    }

    async Task ITreeListEditController.SaveEditAsync() => await SaveEditInternalAsync();
    async Task ITreeListEditController.CancelEditAsync() { CancelEditInternal(); await InvokeAsync(StateHasChanged); }

    internal async Task SaveEditInternalAsync()
    {
        if (_editingItem is null) return;
        foreach (var kvp in _editingValues)
        {
            var prop = typeof(TItem).GetProperty(kvp.Key);
            if (prop is not null && prop.CanWrite)
            {
                try { var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType; prop.SetValue(_editingItem, kvp.Value is null ? null : Convert.ChangeType(kvp.Value, t)); }
                catch { }
            }
        }
        if (_isNewItem) { if (OnCreate.HasDelegate) await OnCreate.InvokeAsync(new TreeListCommandEventArgs<TItem> { Item = _editingItem, IsNew = true }); }
        else { if (OnUpdate.HasDelegate) await OnUpdate.InvokeAsync(new TreeListCommandEventArgs<TItem> { Item = _editingItem }); }
        _editingItem = default; _editingValues.Clear(); _isNewItem = false;
    }

    internal void CancelEditInternal() { _editingItem = default; _editingValues.Clear(); _isNewItem = false; }

    private async Task DeleteItem(TItem item)
    {
        if (OnDelete.HasDelegate) await OnDelete.InvokeAsync(new TreeListCommandEventArgs<TItem> { Item = item });
    }

    // ── Column resize ────────────────────────────────────────────
    private readonly Dictionary<string, double> _runtimeWidths = new();
    private bool _isResizing;
    private string? _resizingField;
    private double _resizeStartX;
    private double _resizeStartWidth;

    internal string GetColumnWidthStyle(IColumnDescriptor col)
    {
        if (_runtimeWidths.TryGetValue(col.Field, out var w))
            return $"width:{w}px;min-width:{w}px;";
        return col.Width != null ? $"width:{col.Width};" : "";
    }

    private void OnResizeMouseDown(MouseEventArgs e, IColumnDescriptor col)
    {
        if (!Resizable) return;
        _isResizing = true;
        _resizingField = col.Field;
        _resizeStartX = e.ClientX;
        _resizeStartWidth = _runtimeWidths.TryGetValue(col.Field, out var w) ? w : 150;
    }

    private void OnResizeMouseMove(MouseEventArgs e)
    {
        if (!_isResizing || _resizingField is null) return;
        var delta = e.ClientX - _resizeStartX;
        var newWidth = Math.Max(40, _resizeStartWidth + delta);
        _runtimeWidths[_resizingField] = newWidth;
    }

    private void OnResizeMouseUp(MouseEventArgs e)
    {
        _isResizing = false;
        _resizingField = null;
    }

    // ── Column reorder ───────────────────────────────────────────
    private readonly List<int> _columnOrder = new();
    private int _dragSourceIndex = -1;
    private int _dragOverIndex = -1;

    internal List<IColumnDescriptor> OrderedColumns
    {
        get
        {
            var cols = EffectiveColumns;
            if (_columnOrder.Count != cols.Count)
            {
                _columnOrder.Clear();
                for (var i = 0; i < cols.Count; i++) _columnOrder.Add(i);
            }
            return _columnOrder.Select(i => i < cols.Count ? cols[i] : cols[0]).ToList();
        }
    }

    private void OnDragStart(DragEventArgs e, int index)
    {
        if (!Reorderable) return;
        _dragSourceIndex = index;
    }

    private void OnDragOver(DragEventArgs e, int index)
    {
        _dragOverIndex = index;
    }

    private async Task OnDrop(DragEventArgs e, int targetIndex)
    {
        if (!Reorderable || _dragSourceIndex < 0 || _dragSourceIndex == targetIndex) { _dragSourceIndex = -1; _dragOverIndex = -1; return; }
        var cols = EffectiveColumns;
        if (_columnOrder.Count != cols.Count)
        {
            _columnOrder.Clear();
            for (var i = 0; i < cols.Count; i++) _columnOrder.Add(i);
        }
        var movingOriginalIndex = _columnOrder[_dragSourceIndex];
        _columnOrder.RemoveAt(_dragSourceIndex);
        _columnOrder.Insert(targetIndex, movingOriginalIndex);
        var movedCol = cols[movingOriginalIndex];
        var oldIdx = _dragSourceIndex;
        _dragSourceIndex = -1;
        _dragOverIndex = -1;
        if (OnColumnReordered.HasDelegate)
            await OnColumnReordered.InvokeAsync(new TreeListColumnReorderEventArgs { Field = movedCol.Field, OldIndex = oldIdx, NewIndex = targetIndex });
    }

    private void OnDragEnd(DragEventArgs e)
    {
        _dragSourceIndex = -1;
        _dragOverIndex = -1;
    }

    // ── Paging ──────────────────────────────────────────────────
    internal async Task GoToPageAsync(int page)
    {
        if (page < 1 || page > TotalPages || page == Page) return;
        Page = page;
        if (PageChanged.HasDelegate) await PageChanged.InvokeAsync(Page);

        if (OnRead.HasDelegate)
        {
            await LoadServerDataAsync();
        }
        else
        {
            _rootItems = BuildTree();
        }
    }

    private Task GoToPreviousPageAsync() => GoToPageAsync(Page - 1);
    private Task GoToNextPageAsync() => GoToPageAsync(Page + 1);

    /// <summary>
    /// Triggers a data re-read from the server. Only effective when <see cref="OnRead"/> is bound.
    /// </summary>
    public async Task RebindAsync()
    {
        if (OnRead.HasDelegate)
        {
            await LoadServerDataAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    // ── Keyboard navigation ─────────────────────────────────────
    internal int _focusedRowIndex = -1;

    /// <summary>Collects all visible rows (flattened, respecting expand state) for keyboard navigation.</summary>
    private List<(TreeListNode Node, int Depth)> GetFlattenedVisibleRows()
    {
        var result = new List<(TreeListNode, int)>();
        CollectVisibleRows(_rootItems, 0, result);
        return result;
    }

    private void CollectVisibleRows(List<TreeListNode> nodes, int depth, List<(TreeListNode, int)> result)
    {
        foreach (var node in nodes)
        {
            result.Add((node, depth));
            if (_expandedIds.Contains(node.Id) && node.Children.Any())
                CollectVisibleRows(node.Children, depth + 1, result);
        }
    }

    internal async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (!Navigable) return;

        var visibleRows = GetFlattenedVisibleRows();
        if (visibleRows.Count == 0) return;

        switch (e.Key)
        {
            case "ArrowDown":
                _focusedRowIndex = Math.Min(_focusedRowIndex + 1, visibleRows.Count - 1);
                break;

            case "ArrowUp":
                _focusedRowIndex = Math.Max(_focusedRowIndex - 1, 0);
                break;

            case "ArrowRight":
                if (_focusedRowIndex >= 0 && _focusedRowIndex < visibleRows.Count)
                {
                    var (node, _) = visibleRows[_focusedRowIndex];
                    var hasKids = node.Children.Any() || node.HasChildren;
                    if (hasKids && !_expandedIds.Contains(node.Id))
                    {
                        var args = new TreeListExpandEventArgs<TItem> { Item = node.Item };
                        if (OnExpand.HasDelegate) await OnExpand.InvokeAsync(args);
                        if (!args.IsCancelled)
                        {
                            _expandedIds.Add(node.Id);
                            await SyncExpandedItemsAsync(node.Item, add: true);
                        }
                    }
                    else if (hasKids && _expandedIds.Contains(node.Id))
                    {
                        // Move to first child
                        if (_focusedRowIndex + 1 < visibleRows.Count)
                            _focusedRowIndex++;
                    }
                }
                break;

            case "ArrowLeft":
                if (_focusedRowIndex >= 0 && _focusedRowIndex < visibleRows.Count)
                {
                    var (node, depth) = visibleRows[_focusedRowIndex];
                    var hasKids = node.Children.Any() || node.HasChildren;
                    if (hasKids && _expandedIds.Contains(node.Id))
                    {
                        var args = new TreeListCollapseEventArgs<TItem> { Item = node.Item };
                        if (OnCollapse.HasDelegate) await OnCollapse.InvokeAsync(args);
                        if (!args.IsCancelled)
                        {
                            _expandedIds.Remove(node.Id);
                            await SyncExpandedItemsAsync(node.Item, add: false);
                        }
                    }
                    else if (depth > 0)
                    {
                        // Move to parent
                        for (var i = _focusedRowIndex - 1; i >= 0; i--)
                        {
                            if (visibleRows[i].Depth < depth)
                            {
                                _focusedRowIndex = i;
                                break;
                            }
                        }
                    }
                }
                break;

            case "Enter":
                if (_focusedRowIndex >= 0 && _focusedRowIndex < visibleRows.Count)
                {
                    var (node, _) = visibleRows[_focusedRowIndex];
                    if (EditMode == TreeListEditMode.Inline && IsEditing(node.Item))
                    {
                        // Already editing — no-op, let input handle Enter
                    }
                    else if (EditMode == TreeListEditMode.Inline)
                    {
                        BeginEdit(node.Item);
                    }
                    else
                    {
                        await HandleRowClick(node.Item);
                    }
                }
                break;

            case "Escape":
                if (_editingItem is not null)
                {
                    CancelEditInternal();
                }
                break;

            case "Home":
                _focusedRowIndex = 0;
                break;

            case "End":
                _focusedRowIndex = visibleRows.Count - 1;
                break;

            default:
                return; // Don't re-render for unhandled keys
        }
    }

    // ── Row drag-and-drop ───────────────────────────────────────
    private int _rowDragSourceIndex = -1;
    private int _rowDragOverIndex = -1;

    private void OnRowDragStart(DragEventArgs e, int flatIndex)
    {
        if (!RowDraggable) return;
        _rowDragSourceIndex = flatIndex;
    }

    private void OnRowDragOver(DragEventArgs e, int flatIndex)
    {
        if (!RowDraggable) return;
        _rowDragOverIndex = flatIndex;
    }

    private async Task OnRowDrop(DragEventArgs e, int flatIndex, TItem targetItem)
    {
        if (!RowDraggable || _rowDragSourceIndex < 0 || _rowDragSourceIndex == flatIndex)
        {
            _rowDragSourceIndex = -1;
            _rowDragOverIndex = -1;
            return;
        }

        var visibleRows = GetFlattenedVisibleRows();
        TItem? sourceItem = _rowDragSourceIndex < visibleRows.Count ? visibleRows[_rowDragSourceIndex].Node.Item : default;
        if (sourceItem is null) { _rowDragSourceIndex = -1; _rowDragOverIndex = -1; return; }

        var position = flatIndex > _rowDragSourceIndex ? TreeListDropPosition.After : TreeListDropPosition.Before;
        _rowDragSourceIndex = -1;
        _rowDragOverIndex = -1;

        if (OnRowDropped.HasDelegate)
        {
            await OnRowDropped.InvokeAsync(new TreeListRowDropEventArgs<TItem>
            {
                Item = sourceItem,
                DestinationItem = targetItem,
                DropPosition = position,
                DestinationIndex = flatIndex
            });
        }
    }

    private void OnRowDragEnd(DragEventArgs e)
    {
        _rowDragSourceIndex = -1;
        _rowDragOverIndex = -1;
    }

    /// <summary>Flattened visible rows for virtualization.</summary>
    internal List<(TreeListNode Node, int Depth)> FlattenedVisibleRows => GetFlattenedVisibleRows();
}
