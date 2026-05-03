using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sunfish.Foundation.Data;
using Sunfish.Foundation.Enums;
using Sunfish.UIAdapters.Blazor.Base;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Represents one rendered group in a grouped <see cref="SunfishListView{TItem}"/>.
/// </summary>
/// <typeparam name="TItem">The list item type.</typeparam>
public class ListViewGroup<TItem>
{
    /// <summary>The field the items are grouped by.</summary>
    public string Field { get; init; } = "";

    /// <summary>The group key value (formatted).</summary>
    public string Header { get; init; } = "";

    /// <summary>The raw group key value.</summary>
    public object? Key { get; init; }

    /// <summary>The items in this group.</summary>
    public List<TItem> Items { get; init; } = [];
}

/// <summary>
/// Templated list-view component that repeats an item template for each element in its data
/// source. Supports paging, selection, grouping, virtualization, edit templates, and full
/// edit-flow events (<c>OnCreate</c>, <c>OnUpdate</c>, <c>OnDelete</c>, <c>OnEdit</c>,
/// <c>OnCancel</c>).
/// </summary>
/// <typeparam name="TItem">The list item type.</typeparam>
public partial class SunfishListView<TItem> : SunfishComponentBase
{
    // ── Data ────────────────────────────────────────────────────────────────

    /// <summary>The data source for the list.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>
    /// Fired when the list needs data. Bind this to perform server-side paging/grouping; set
    /// <see cref="ListViewReadEventArgs{TItem}.Data"/> and <c>Total</c> in the handler.
    /// </summary>
    [Parameter] public EventCallback<ListViewReadEventArgs<TItem>> OnRead { get; set; }

    // ── Templates ───────────────────────────────────────────────────────────

    /// <summary>
    /// Spec-aligned item template (preferred name). When both <c>Template</c> and
    /// <c>ItemTemplate</c> are supplied, <c>Template</c> wins.
    /// </summary>
    [Parameter] public RenderFragment<TItem>? Template { get; set; }

    /// <summary>Item template (legacy name, retained for back-compat).</summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Template rendered in place of <see cref="Template"/> for odd-index rows.</summary>
    [Parameter] public RenderFragment<TItem>? AlternatingItemTemplate { get; set; }

    /// <summary>Template rendered in place of the item template when an item is in edit mode.</summary>
    [Parameter] public RenderFragment<TItem>? EditTemplate { get; set; }

    /// <summary>Optional content rendered above the items.</summary>
    [Parameter] public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>Optional content rendered below the items and above the pager.</summary>
    [Parameter] public RenderFragment? FooterTemplate { get; set; }

    /// <summary>Optional template for a group header. When null, a default header is rendered.</summary>
    [Parameter] public RenderFragment<ListViewGroup<TItem>>? GroupHeaderTemplate { get; set; }

    // ── Selection ───────────────────────────────────────────────────────────

    /// <summary>Selection mode: None, Single, or Multiple.</summary>
    [Parameter] public GridSelectionMode SelectionMode { get; set; } = GridSelectionMode.None;

    /// <summary>The currently selected items (two-way bindable).</summary>
    [Parameter] public IEnumerable<TItem>? SelectedItems { get; set; }

    /// <summary>Fired when the selection changes.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> SelectedItemsChanged { get; set; }

    // ── Paging ──────────────────────────────────────────────────────────────

    /// <summary>Whether the list uses a pager.</summary>
    [Parameter] public bool Pageable { get; set; }

    /// <summary>Number of items per page.</summary>
    [Parameter] public int PageSize { get; set; } = 10;

    /// <summary>The current page index (zero-based).</summary>
    [Parameter] public int Page { get; set; }

    /// <summary>Fired when the user pages the list.</summary>
    [Parameter] public EventCallback<int> PageChanged { get; set; }

    /// <summary>Fired when the user changes the page size via the pager dropdown.</summary>
    [Parameter] public EventCallback<int> PageSizeChanged { get; set; }

    /// <summary>Optional page-size choices. When provided, a page-size dropdown is rendered in the pager.</summary>
    [Parameter] public IList<int>? PageSizes { get; set; }

    // ── Grouping ────────────────────────────────────────────────────────────

    /// <summary>Whether grouping is enabled.</summary>
    [Parameter] public bool Groupable { get; set; }

    /// <summary>The group descriptors applied to the list. Takes precedence over <see cref="GroupField"/>.</summary>
    [Parameter] public IList<GroupDescriptor>? Group { get; set; }

    /// <summary>
    /// Shortcut for single-field grouping. When set and <see cref="Group"/> is null/empty,
    /// a single <see cref="GroupDescriptor"/> with this field is applied.
    /// </summary>
    [Parameter] public string? GroupField { get; set; }

    // ── Virtualization ──────────────────────────────────────────────────────

    /// <summary>Enable DOM virtualization via <c>Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize</c>.</summary>
    [Parameter] public bool Virtual { get; set; }

    /// <summary>Row height in pixels used by <c>Virtualize.ItemSize</c>. Defaults to <c>28</c>.</summary>
    [Parameter] public int VirtualItemHeight { get; set; } = 28;

    /// <summary>CSS height for the virtualization scroll container.</summary>
    [Parameter] public string VirtualContainerHeight { get; set; } = "400px";

    // ── Edit flow ──────────────────────────────────────────────────────────

    /// <summary>
    /// Optional factory that produces a new <typeparamref name="TItem"/> when the user initiates
    /// an Add command. Useful when <typeparamref name="TItem"/> has no parameterless constructor.
    /// </summary>
    [Parameter] public Func<TItem>? OnModelInit { get; set; }

    /// <summary>Fired when a new item is being created (e.g., an Add button invoked). Cancellable.</summary>
    [Parameter] public EventCallback<ListViewCommandEventArgs<TItem>> OnCreate { get; set; }

    /// <summary>Fired when an existing item enters edit mode. Cancellable.</summary>
    [Parameter] public EventCallback<ListViewCommandEventArgs<TItem>> OnEdit { get; set; }

    /// <summary>Fired when an item is saved from the edit template. Cancellable.</summary>
    [Parameter] public EventCallback<ListViewCommandEventArgs<TItem>> OnUpdate { get; set; }

    /// <summary>Fired when an item is deleted. Cancellable.</summary>
    [Parameter] public EventCallback<ListViewCommandEventArgs<TItem>> OnDelete { get; set; }

    /// <summary>Fired when the user cancels an in-progress edit or create. Cancellable.</summary>
    [Parameter] public EventCallback<ListViewCommandEventArgs<TItem>> OnCancel { get; set; }

    // ── Sizing + misc ───────────────────────────────────────────────────────

    /// <summary>Width of the list container.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Height of the list container.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Whether to show an overlay while <c>OnRead</c> runs.</summary>
    [Parameter] public bool EnableLoaderContainer { get; set; } = true;

    // ── Internal state ──────────────────────────────────────────────────────

    private List<TItem> _allItems = [];
    private IEnumerable<TItem>? _pagedItems;
    private List<TItem> _pagedItemsList = [];
    private List<ListViewGroup<TItem>>? _groupedItems;
    private HashSet<TItem> _selectedItems = new();
    private TItem? _editingItem;
    private bool _editingIsNew;
    private int _currentPage;
    private int _totalCount;
    private bool _isLoading;

    private int TotalPages => _totalCount > 0 ? (int)Math.Ceiling((double)_totalCount / PageSize) : 1;

    private string? SizeStyle
    {
        get
        {
            var parts = new List<string>();
            if (Width != null) parts.Add($"width:{Width};");
            if (Height != null) parts.Add($"height:{Height};overflow:auto;");
            return parts.Count > 0 ? string.Join("", parts) : null;
        }
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────

    protected override async Task OnParametersSetAsync()
    {
        if (SelectedItems != null)
        {
            _selectedItems = new HashSet<TItem>(SelectedItems);
        }

        _currentPage = Page;

        if (OnRead.HasDelegate)
        {
            await LoadServerDataAsync();
        }
        else if (Data != null)
        {
            _allItems = Data.ToList();
            _totalCount = _allItems.Count;
            ApplyPagingAndGrouping();
        }
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>Forces the list view to re-read its data source.</summary>
    public async Task Rebind()
    {
        if (OnRead.HasDelegate)
        {
            await LoadServerDataAsync();
        }
        else if (Data != null)
        {
            _allItems = Data.ToList();
            _totalCount = _allItems.Count;
            ApplyPagingAndGrouping();
        }
        StateHasChanged();
    }

    /// <summary>Forces the list view to re-read its data source (async alias).</summary>
    public Task RebindAsync() => Rebind();

    /// <summary>
    /// Enters edit mode for the supplied item. Fires <see cref="OnEdit"/> and respects
    /// <c>IsCancelled</c>.
    /// </summary>
    public async Task BeginEditAsync(TItem item)
    {
        var args = new ListViewCommandEventArgs<TItem> { Item = item, IsNew = false };
        if (OnEdit.HasDelegate) await OnEdit.InvokeAsync(args);
        if (args.IsCancelled) return;

        _editingItem = item;
        _editingIsNew = false;
        StateHasChanged();
    }

    /// <summary>
    /// Creates a new item (via <see cref="OnModelInit"/> if set) and enters edit mode.
    /// Fires <see cref="OnCreate"/>; the handler may cancel the creation.
    /// </summary>
    public async Task BeginInsertAsync()
    {
        TItem newItem;
        if (OnModelInit != null)
        {
            newItem = OnModelInit();
        }
        else
        {
            try { newItem = Activator.CreateInstance<TItem>(); }
            catch { return; }
        }

        var args = new ListViewCommandEventArgs<TItem> { Item = newItem, IsNew = true };
        if (OnCreate.HasDelegate) await OnCreate.InvokeAsync(args);
        if (args.IsCancelled) return;

        _editingItem = newItem;
        _editingIsNew = true;
        StateHasChanged();
    }

    /// <summary>
    /// Saves the currently edited item. Fires <see cref="OnUpdate"/> (or <see cref="OnCreate"/>
    /// when the item is new). Handlers may cancel the save via <c>IsCancelled</c>.
    /// </summary>
    public async Task SaveEditAsync()
    {
        if (_editingItem == null) return;

        var args = new ListViewCommandEventArgs<TItem>
        {
            Item = _editingItem,
            IsNew = _editingIsNew
        };

        if (_editingIsNew)
        {
            if (OnCreate.HasDelegate) await OnCreate.InvokeAsync(args);
        }
        else
        {
            if (OnUpdate.HasDelegate) await OnUpdate.InvokeAsync(args);
        }

        if (args.IsCancelled) return;

        _editingItem = default;
        _editingIsNew = false;
        StateHasChanged();
    }

    /// <summary>
    /// Cancels the current edit/insert. Fires <see cref="OnCancel"/>; handlers may cancel the
    /// cancellation via <c>IsCancelled</c> (rare, but spec-compatible).
    /// </summary>
    public async Task CancelEditAsync()
    {
        if (_editingItem == null) return;

        var args = new ListViewCommandEventArgs<TItem>
        {
            Item = _editingItem,
            IsNew = _editingIsNew
        };
        if (OnCancel.HasDelegate) await OnCancel.InvokeAsync(args);
        if (args.IsCancelled) return;

        _editingItem = default;
        _editingIsNew = false;
        StateHasChanged();
    }

    /// <summary>
    /// Deletes the supplied item. Fires <see cref="OnDelete"/>; handlers may cancel via
    /// <c>IsCancelled</c>.
    /// </summary>
    public async Task DeleteItemAsync(TItem item)
    {
        var args = new ListViewCommandEventArgs<TItem> { Item = item, IsNew = false };
        if (OnDelete.HasDelegate) await OnDelete.InvokeAsync(args);
    }

    // ── Data processing ────────────────────────────────────────────────────

    private async Task LoadServerDataAsync()
    {
        _isLoading = true;
        StateHasChanged();
        try
        {
            var request = new DataRequest
            {
                Paging = new PageState
                {
                    PageIndex = _currentPage,
                    PageSize = PageSize
                }
            };

            // Flow grouping into the request
            var descriptors = BuildGroupDescriptors();
            if (descriptors.Count > 0)
            {
                request.Grouping = descriptors.ToList();
            }

            var args = new ListViewReadEventArgs<TItem> { Request = request };
            await OnRead.InvokeAsync(args);

            _allItems = args.Data.ToList();
            _totalCount = args.Total > 0 ? args.Total : _allItems.Count;
            ApplyPagingAndGrouping(skipPaging: true);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyPagingAndGrouping(bool skipPaging = false)
    {
        IEnumerable<TItem> source = _allItems;

        if (Pageable && !skipPaging)
        {
            source = _allItems.Skip(_currentPage * PageSize).Take(PageSize);
        }

        _pagedItems = source;
        _pagedItemsList = source.ToList();

        if (Groupable)
        {
            var descriptors = BuildGroupDescriptors();
            _groupedItems = descriptors.Count == 0 ? null : BuildGroups(_pagedItemsList, descriptors);
        }
        else
        {
            _groupedItems = null;
        }
    }

    private List<GroupDescriptor> BuildGroupDescriptors()
    {
        if (Group != null && Group.Count > 0) return Group.ToList();
        if (!string.IsNullOrWhiteSpace(GroupField))
        {
            return new List<GroupDescriptor> { new() { Field = GroupField } };
        }
        return [];
    }

    private static List<ListViewGroup<TItem>> BuildGroups(
        List<TItem> items,
        List<GroupDescriptor> descriptors)
    {
        // Only the first descriptor is rendered inline (flat grouping);
        // nested grouping is deferred until a spec demo requires it.
        var d = descriptors[0];
        var groups = new Dictionary<object, ListViewGroup<TItem>>(new NullSafeComparer());
        foreach (var item in items)
        {
            var key = GetFieldValue(item, d.Field) ?? "(null)";
            if (!groups.TryGetValue(key, out var g))
            {
                g = new ListViewGroup<TItem>
                {
                    Field = d.Field,
                    Header = key.ToString() ?? "(null)",
                    Key = key,
                };
                groups[key] = g;
            }
            g.Items.Add(item);
        }

        return d.Direction == SortDirection.Descending
            ? groups.Values.OrderByDescending(g => g.Header).ToList()
            : groups.Values.OrderBy(g => g.Header).ToList();
    }

    private static object? GetFieldValue(TItem item, string field)
    {
        if (item is null || string.IsNullOrWhiteSpace(field)) return null;
        var prop = typeof(TItem).GetProperty(field);
        return prop?.GetValue(item);
    }

    private sealed class NullSafeComparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) => x?.Equals(y) ?? y is null;
        public int GetHashCode(object obj) => obj?.GetHashCode() ?? 0;
    }

    // ── Selection ───────────────────────────────────────────────────────────

    private async Task HandleItemClick(TItem item)
    {
        if (SelectionMode == GridSelectionMode.None) return;

        if (SelectionMode == GridSelectionMode.Single)
        {
            _selectedItems.Clear();
            _selectedItems.Add(item);
        }
        else
        {
            if (!_selectedItems.Remove(item)) _selectedItems.Add(item);
        }

        await SelectedItemsChanged.InvokeAsync(_selectedItems);
    }

    // ── Paging ──────────────────────────────────────────────────────────────

    private async Task PreviousPage()
    {
        if (_currentPage > 0)
        {
            _currentPage--;
            await HandlePageChangeAsync();
        }
    }

    private async Task NextPage()
    {
        if (_currentPage < TotalPages - 1)
        {
            _currentPage++;
            await HandlePageChangeAsync();
        }
    }

    private async Task HandlePageChangeAsync()
    {
        if (OnRead.HasDelegate)
        {
            await LoadServerDataAsync();
        }
        else
        {
            ApplyPagingAndGrouping();
        }
        if (PageChanged.HasDelegate) await PageChanged.InvokeAsync(_currentPage);
    }

    private async Task HandlePageSizeChanged(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), out var newSize)) return;
        PageSize = newSize;
        _currentPage = 0;

        if (OnRead.HasDelegate)
        {
            await LoadServerDataAsync();
        }
        else
        {
            ApplyPagingAndGrouping();
        }
        if (PageSizeChanged.HasDelegate) await PageSizeChanged.InvokeAsync(newSize);
    }
}
