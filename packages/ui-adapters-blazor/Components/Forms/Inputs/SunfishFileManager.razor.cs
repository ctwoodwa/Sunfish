using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;
using System.Reflection;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// A generic file manager component that displays and navigates a file/folder tree.
/// Bind your own data model via <c>Data</c> and field-mapping parameters, or use
/// <see cref="FileManagerEntry"/> directly as <typeparamref name="TItem"/>.
/// </summary>
public partial class SunfishFileManager<TItem> : SunfishComponentBase
{
    // ── Internal state ──────────────────────────────────────────────────────────

    private List<TItem> _selectedItems = new();
    private IEnumerable<TItem> _resolvedItems = Enumerable.Empty<TItem>();
    private CancellationTokenSource? _readCts;
    private string _searchFilter = string.Empty;

    // Phase F: sort state
    internal string _sortField = "Name";
    internal bool _sortAscending = true;

    // Phase F: loading state
    internal bool _isLoading;

    // Phase D: context menu state
    private TItem? _contextMenuItem;
    private bool _contextMenuVisible;
    private double _contextMenuX;
    private double _contextMenuY;

    // Phase D: inline rename state
    private TItem? _renamingItem;
    internal string _renameText = string.Empty;

    // Phase D: delete confirmation state
    private TItem? _deleteConfirmItem;

    // Phase E: preview pane state
    internal bool _previewPaneVisible;

    // Phase E: upload dialog state
    internal bool _uploadDialogVisible;

    // ── Parameters: Data ────────────────────────────────────────────────────────

    /// <summary>
    /// The data items to display. For server-side loading, use <see cref="OnRead"/> instead.
    /// </summary>
    [Parameter] public IEnumerable<TItem> Data { get; set; } = Enumerable.Empty<TItem>();

    /// <summary>
    /// Fires when the component needs data (on init and path changes).
    /// Assign <see cref="FileManagerReadEventArgs.Data"/> in the handler.
    /// When bound, the <see cref="Data"/> parameter is ignored.
    /// </summary>
    [Parameter] public EventCallback<FileManagerReadEventArgs> OnRead { get; set; }

    // ── Parameters: Field Bindings ──────────────────────────────────────────────

    /// <summary>Property name for the item id. Default: "Id".</summary>
    [Parameter] public string IdField { get; set; } = "Id";

    /// <summary>Property name for the parent item id. Default: "ParentId".</summary>
    [Parameter] public string ParentIdField { get; set; } = "ParentId";

    /// <summary>Property name for the display name. Default: "Name".</summary>
    [Parameter] public string NameField { get; set; } = "Name";

    /// <summary>Property name for the full path. Default: "Path".</summary>
    [Parameter] public string PathField { get; set; } = "Path";

    /// <summary>Property name for the file extension. Default: "Extension".</summary>
    [Parameter] public string ExtensionField { get; set; } = "Extension";

    /// <summary>Property name for the directory flag. Default: "IsDirectory".</summary>
    [Parameter] public string IsDirectoryField { get; set; } = "IsDirectory";

    /// <summary>Property name for the has-directories flag. Default: "HasDirectories".</summary>
    [Parameter] public string HasDirectoriesField { get; set; } = "HasDirectories";

    /// <summary>Property name for the file size. Default: "Size".</summary>
    [Parameter] public string SizeField { get; set; } = "Size";

    /// <summary>Property name for the local date created. Default: "DateCreated".</summary>
    [Parameter] public string DateCreatedField { get; set; } = "DateCreated";

    /// <summary>Property name for the UTC date created. Default: "DateCreatedUtc".</summary>
    [Parameter] public string DateCreatedUtcField { get; set; } = "DateCreatedUtc";

    /// <summary>Property name for the local date modified. Default: "DateModified".</summary>
    [Parameter] public string DateModifiedField { get; set; } = "DateModified";

    /// <summary>Property name for the UTC date modified. Default: "DateModifiedUtc".</summary>
    [Parameter] public string DateModifiedUtcField { get; set; } = "DateModifiedUtc";

    /// <summary>Property name for the child directories collection. Default: "Directories".</summary>
    [Parameter] public string DirectoriesField { get; set; } = "Directories";

    /// <summary>Property name for the child items collection. Default: "Items".</summary>
    [Parameter] public string ItemsField { get; set; } = "Items";

    // ── Parameters: Navigation ──────────────────────────────────────────────────

    /// <summary>The current navigation path. Supports two-way binding.</summary>
    [Parameter] public string Path { get; set; } = "/";

    /// <summary>Fires when the path changes.</summary>
    [Parameter] public EventCallback<string> PathChanged { get; set; }

    // ── Parameters: View ────────────────────────────────────────────────────────

    /// <summary>The current view type. Supports two-way binding.</summary>
    [Parameter] public FileManagerViewType View { get; set; } = FileManagerViewType.ListView;

    /// <summary>Fires when the view type changes.</summary>
    [Parameter] public EventCallback<FileManagerViewType> ViewChanged { get; set; }

    // ── Parameters: Toolbar ─────────────────────────────────────────────────────

    /// <summary>
    /// Optional custom toolbar content. When null, the default toolbar is rendered
    /// with Up, breadcrumb, view toggle, New Folder (if AllowCreate), and search.
    /// </summary>
    [Parameter] public RenderFragment? ToolBarTemplate { get; set; }

    // ── Parameters: Layout ──────────────────────────────────────────────────────

    /// <summary>Optional height (e.g. "400px", "60vh"). Applied as inline style.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Optional width (e.g. "800px", "100%"). Applied as inline style.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>
    /// When true, a loading overlay is rendered over the file list while data is being fetched.
    /// </summary>
    [Parameter] public bool EnableLoaderContainer { get; set; }

    /// <summary>When true, displays the folder-tree sidebar.</summary>
    [Parameter] public bool ShowFolderTree { get; set; } = true;

    // ── Parameters: Upload (Phase E) ─────────────────────────────────────────────

    /// <summary>
    /// Optional child configuration fragment (e.g. <c>&lt;FileManagerSettings&gt;</c>).
    /// When provided the component wraps its content in a settings cascade.
    /// </summary>
    [Parameter] public RenderFragment? FileManagerSettings { get; set; }

    /// <summary>
    /// Upload settings. When non-null, an Upload button appears in the default toolbar.
    /// See <see cref="FileManagerUploadSettings"/> for configuration options.
    /// </summary>
    [Parameter] public FileManagerUploadSettings? UploadSettings { get; set; }

    // ── Parameters: Preview pane (Phase E) ───────────────────────────────────────

    /// <summary>
    /// When true the Details toggle button is visible in the default toolbar and
    /// the preview pane can be opened. Defaults to false.
    /// </summary>
    [Parameter] public bool ShowPreviewPane { get; set; }

    // ── Parameters: Permissions ─────────────────────────────────────────────────

    /// <summary>Enables the New Folder button.</summary>
    [Parameter] public bool AllowCreate { get; set; }

    /// <summary>Enables the Delete action.</summary>
    [Parameter] public bool AllowDelete { get; set; }

    /// <summary>Enables the Rename action.</summary>
    [Parameter] public bool AllowRename { get; set; }

    // ── Parameters: Selection ───────────────────────────────────────────────────

    /// <summary>The currently selected items. Supports two-way binding.</summary>
    [Parameter] public IEnumerable<TItem> SelectedItems { get; set; } = Enumerable.Empty<TItem>();

    /// <summary>Fires when the selection changes.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> SelectedItemsChanged { get; set; }

    // ── Parameters: Events ──────────────────────────────────────────────────────

    /// <summary>Fires when the user single-clicks an item.</summary>
    [Parameter] public EventCallback<TItem> OnSelect { get; set; }

    /// <summary>Fires when the user double-clicks an item (opens directories, opens files).</summary>
    [Parameter] public EventCallback<TItem> OnOpen { get; set; }

    /// <summary>Fires when the user creates a new folder.</summary>
    [Parameter] public EventCallback<FileManagerCreateEventArgs<TItem>> OnCreate { get; set; }

    /// <summary>Fires when the user deletes an item.</summary>
    [Parameter] public EventCallback<FileManagerDeleteEventArgs<TItem>> OnDelete { get; set; }

    /// <summary>Fires when the user starts renaming an item (Phase D wires the rename UI).</summary>
    [Parameter] public EventCallback<FileManagerEditEventArgs<TItem>> OnEdit { get; set; }

    /// <summary>Fires when a rename operation completes.</summary>
    [Parameter] public EventCallback<FileManagerUpdateEventArgs<TItem>> OnUpdate { get; set; }

    /// <summary>
    /// Fires before a file download is initiated. Set IsCancelled = true to suppress the download.
    /// </summary>
    [Parameter] public EventCallback<FileManagerDownloadEventArgs<TItem>> OnDownload { get; set; }

    /// <summary>
    /// Factory callback invoked when a new TItem instance is needed (e.g. folder creation).
    /// When null, default(TItem) is used.
    /// </summary>
    [Parameter] public Func<TItem>? OnModelInit { get; set; }

    // ── Derived state ───────────────────────────────────────────────────────────

    internal bool CanNavigateUp => Path != "/" && Path.Contains('/');

    /// <summary>Returns the first selected item (or null when nothing is selected).</summary>
    internal TItem? SelectedItem => _selectedItems.Count > 0 ? _selectedItems[0] : default;

    // ── PropertyInfo cache ──────────────────────────────────────────────────────

    private readonly Dictionary<string, PropertyInfo?> _propCache = new();

    private PropertyInfo? GetProp(string fieldName)
    {
        if (!_propCache.TryGetValue(fieldName, out var prop))
        {
            prop = typeof(TItem).GetProperty(fieldName);
            _propCache[fieldName] = prop;
        }
        return prop;
    }

    private T? GetFieldValue<T>(TItem item, string fieldName)
    {
        if (item is null) return default;
        var prop = GetProp(fieldName);
        if (prop is null) return default;
        var val = prop.GetValue(item);
        if (val is T typed) return typed;
        return default;
    }

    // ── Field accessor helpers ──────────────────────────────────────────────────

    internal string GetName(TItem item) => GetFieldValue<string>(item, NameField) ?? string.Empty;
    internal string GetPath(TItem item) => GetFieldValue<string>(item, PathField) ?? string.Empty;
    internal bool GetIsDirectory(TItem item) => GetFieldValue<bool>(item, IsDirectoryField);
    internal long GetSize(TItem item) => GetFieldValue<long>(item, SizeField);
    internal DateTime? GetDateModified(TItem item) => GetFieldValue<DateTime?>(item, DateModifiedField);
    internal DateTime? GetDateCreated(TItem item) => GetFieldValue<DateTime?>(item, DateCreatedField);
    internal string? GetExtension(TItem item) => GetFieldValue<string>(item, ExtensionField);

    // ── Lifecycle ───────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadDataAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        // If OnRead is not bound, sync _resolvedItems from the Data parameter
        if (!OnRead.HasDelegate)
            _resolvedItems = Data;

        // Sync internal selection list from the bound SelectedItems parameter
        _selectedItems = SelectedItems.ToList();
    }

    // ── Data loading ────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers a data reload. Call this from outside the component to force a rebind.
    /// </summary>
    public Task Rebind() => LoadDataAsync();

    private async Task LoadDataAsync()
    {
        if (OnRead.HasDelegate)
        {
            _readCts?.Cancel();
            _readCts = new CancellationTokenSource();

            _isLoading = true;
            await InvokeAsync(StateHasChanged);

            var args = new FileManagerReadEventArgs
            {
                Path = Path,
                CancellationToken = _readCts.Token
            };

            await OnRead.InvokeAsync(args);

            _isLoading = false;

            if (!_readCts.Token.IsCancellationRequested && args.Data is not null)
            {
                _resolvedItems = args.Data.OfType<TItem>();
                await InvokeAsync(StateHasChanged);
            }
        }
        else
        {
            _resolvedItems = Data;
        }
    }

    // ── Navigation ──────────────────────────────────────────────────────────────

    internal IEnumerable<TItem> GetCurrentItems()
    {
        // Directories always float to the top regardless of sort field
        var filtered = _resolvedItems
            .Where(i => GetParentPath(GetPath(i)) == Path.TrimEnd('/'));

        if (!string.IsNullOrWhiteSpace(_searchFilter))
            filtered = filtered.Where(i => GetName(i).Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));

        // Apply sort: directories first, then by selected field + direction
        IEnumerable<TItem> items = _sortField switch
        {
            "Size" => _sortAscending
                ? filtered.OrderByDescending(GetIsDirectory).ThenBy(GetSize)
                : filtered.OrderByDescending(GetIsDirectory).ThenByDescending(GetSize),
            "DateModified" => _sortAscending
                ? filtered.OrderByDescending(GetIsDirectory).ThenBy(GetDateModified)
                : filtered.OrderByDescending(GetIsDirectory).ThenByDescending(GetDateModified),
            "Extension" => _sortAscending
                ? filtered.OrderByDescending(GetIsDirectory).ThenBy(GetExtension)
                : filtered.OrderByDescending(GetIsDirectory).ThenByDescending(GetExtension),
            "Type" => _sortAscending
                ? filtered.OrderBy(GetIsDirectory).ThenBy(GetName)
                : filtered.OrderByDescending(GetIsDirectory).ThenByDescending(GetName),
            _ => _sortAscending
                ? filtered.OrderByDescending(GetIsDirectory).ThenBy(GetName)
                : filtered.OrderByDescending(GetIsDirectory).ThenByDescending(GetName),
        };

        return items;
    }

    internal IEnumerable<TItem> GetRootFolders()
    {
        return _resolvedItems
            .Where(GetIsDirectory)
            .OrderBy(GetPath);
    }

    private static string GetParentPath(string path)
    {
        var lastSlash = path.TrimEnd('/').LastIndexOf('/');
        return lastSlash <= 0 ? "" : path[..lastSlash];
    }

    internal async Task NavigateTo(string path)
    {
        Path = path;
        await PathChanged.InvokeAsync(Path);
        await LoadDataAsync();
    }

    internal async Task NavigateUp()
    {
        var parent = GetParentPath(Path);
        await NavigateTo(string.IsNullOrEmpty(parent) ? "/" : parent);
    }

    // ── Item actions ────────────────────────────────────────────────────────────

    internal async Task SelectItem(TItem item)
    {
        _selectedItems = new List<TItem> { item };
        await SelectedItemsChanged.InvokeAsync(_selectedItems);
        await OnSelect.InvokeAsync(item);
    }

    internal async Task OpenItem(TItem item)
    {
        if (GetIsDirectory(item))
            await NavigateTo(GetPath(item));
        else
            await OnOpen.InvokeAsync(item);
    }

    internal async Task CreateFolder()
    {
        TItem? newItem = OnModelInit is not null ? OnModelInit() : default;
        var args = new FileManagerCreateEventArgs<TItem> { Item = newItem };
        await OnCreate.InvokeAsync(args);
    }

    internal async Task DeleteItem(TItem item)
    {
        if (!AllowDelete) return;
        var args = new FileManagerDeleteEventArgs<TItem> { Item = item };
        await OnDelete.InvokeAsync(args);
    }

    internal async Task EditItem(TItem item)
    {
        var args = new FileManagerEditEventArgs<TItem> { Item = item };
        await OnEdit.InvokeAsync(args);
    }

    internal async Task UpdateItem(TItem item)
    {
        var args = new FileManagerUpdateEventArgs<TItem> { Item = item };
        await OnUpdate.InvokeAsync(args);
    }

    /// <summary>
    /// Initiates a file download. Fires OnDownload; if the handler sets IsCancelled,
    /// the download is suppressed. Returns the event args so callers can inspect IsCancelled.
    /// </summary>
    internal async Task<FileManagerDownloadEventArgs<TItem>> DownloadItem(TItem item)
    {
        var args = new FileManagerDownloadEventArgs<TItem> { Item = item };
        await OnDownload.InvokeAsync(args);
        return args;
    }

    // ── Search ──────────────────────────────────────────────────────────────────

    internal string SearchFilter
    {
        get => _searchFilter;
        set
        {
            _searchFilter = value ?? string.Empty;
            StateHasChanged();
        }
    }

    // ── View Toggle ─────────────────────────────────────────────────────────────

    internal async Task SetViewType(FileManagerViewType viewType)
    {
        View = viewType;
        await ViewChanged.InvokeAsync(View);
        await InvokeAsync(StateHasChanged);
    }

    internal async Task ToggleView()
    {
        var next = View == FileManagerViewType.Grid
            ? FileManagerViewType.ListView
            : FileManagerViewType.Grid;
        await SetViewType(next);
    }

    // ── Breadcrumb ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the breadcrumb segments for the current path.
    /// Each element is (label, path). The root "/" is always the first segment.
    /// </summary>
    internal IEnumerable<(string Label, string SegmentPath)> GetBreadcrumbSegments()
    {
        yield return ("/", "/");

        var parts = Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var segPath = "/" + string.Join("/", parts[..^(parts.Length - 1 - i)]);
            yield return (parts[i], segPath);
        }
    }

    // ── Phase E: Preview pane ────────────────────────────────────────────────────

    /// <summary>
    /// Toggles the preview pane open or closed.
    /// Only functional when <see cref="ShowPreviewPane"/> is true.
    /// </summary>
    internal void TogglePreviewPane()
    {
        _previewPaneVisible = !_previewPaneVisible;
        StateHasChanged();
    }

    // ── Phase E: Upload dialog ───────────────────────────────────────────────────

    /// <summary>Opens the upload dialog. No-op when <see cref="UploadSettings"/> is null.</summary>
    internal void ShowUploadDialog()
    {
        if (UploadSettings is null) return;
        _uploadDialogVisible = true;
        StateHasChanged();
    }

    /// <summary>Closes the upload dialog.</summary>
    internal void CloseUploadDialog()
    {
        _uploadDialogVisible = false;
        StateHasChanged();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    internal bool IsSelected(TItem item) => _selectedItems.Contains(item);

    internal static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }

    internal string GetHeightStyle()
    {
        return string.IsNullOrEmpty(Height) ? "" : $"height:{Height};";
    }

    internal string GetWidthStyle()
    {
        return string.IsNullOrEmpty(Width) ? "" : $"width:{Width};";
    }

    /// <summary>Returns combined height + width inline styles for the root element.</summary>
    internal string GetContainerStyle()
    {
        return GetHeightStyle() + GetWidthStyle();
    }

    // ── Phase F: Sort controls ──────────────────────────────────────────────────

    internal void SetSortField(string field)
    {
        _sortField = field;
        StateHasChanged();
    }

    internal void HandleSortFieldChange(Microsoft.AspNetCore.Components.ChangeEventArgs e)
    {
        SetSortField(e.Value?.ToString() ?? "Name");
    }

    internal void ToggleSortDirection()
    {
        _sortAscending = !_sortAscending;
        StateHasChanged();
    }

    // ── Phase D: Context menu ───────────────────────────────────────────────────

    internal async Task ShowContextMenu(TItem item, Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        _contextMenuItem = item;
        _contextMenuX = e.ClientX;
        _contextMenuY = e.ClientY;
        _contextMenuVisible = true;
        await InvokeAsync(StateHasChanged);
    }

    internal async Task CloseContextMenu()
    {
        _contextMenuVisible = false;
        _contextMenuItem = default;
        await InvokeAsync(StateHasChanged);
    }

    // ── Phase D: Rename ─────────────────────────────────────────────────────────

    internal async Task StartRename(TItem item)
    {
        if (!AllowRename) return;
        _contextMenuVisible = false;
        _renamingItem = item;
        _renameText = GetName(item);
        await EditItem(item);
        await InvokeAsync(StateHasChanged);
    }

    internal async Task CommitRename()
    {
        if (_renamingItem is null) return;
        var item = _renamingItem;
        // Write the new name back via reflection if the property is settable
        var nameProp = GetProp(NameField);
        if (nameProp is not null && nameProp.CanWrite)
            nameProp.SetValue(item, _renameText);
        _renamingItem = default;
        await UpdateItem(item);
        await InvokeAsync(StateHasChanged);
    }

    internal async Task CancelRename()
    {
        _renamingItem = default;
        _renameText = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    internal bool IsRenaming(TItem item) =>
        _renamingItem is not null && ReferenceEquals(_renamingItem, item);

    internal async Task HandleRenameKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await CommitRename();
        else if (e.Key == "Escape")
            await CancelRename();
    }

    internal void UpdateRenameText(string value) => _renameText = value;

    internal async Task ContextMenuDownload()
    {
        if (_contextMenuItem is null) return;
        var item = _contextMenuItem!;
        await CloseContextMenu();
        await DownloadItem(item);
    }

    // ── Phase D: Delete confirmation ────────────────────────────────────────────

    internal async Task ConfirmDelete(TItem item)
    {
        if (!AllowDelete) return;
        _contextMenuVisible = false;
        _deleteConfirmItem = item;
        await InvokeAsync(StateHasChanged);
    }

    internal async Task ExecuteDelete()
    {
        if (_deleteConfirmItem is null) return;
        var item = _deleteConfirmItem;
        _deleteConfirmItem = default;
        await DeleteItem(item);
        await InvokeAsync(StateHasChanged);
    }

    internal async Task CancelDelete()
    {
        _deleteConfirmItem = default;
        await InvokeAsync(StateHasChanged);
    }

    // ── IDisposable ─────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _readCts?.Cancel();
            _readCts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
