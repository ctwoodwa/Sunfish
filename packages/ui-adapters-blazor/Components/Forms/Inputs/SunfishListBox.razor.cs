using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sunfish.Foundation.Enums;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Controls the position of the <see cref="SunfishListBox{TItem}"/> toolbar relative to the item list.
/// </summary>
public enum ListBoxToolBarPosition
{
    /// <summary>Toolbar is rendered above the item list.</summary>
    Top,

    /// <summary>Toolbar is rendered to the right of the item list. This is the default.</summary>
    Right,

    /// <summary>Toolbar is rendered below the item list.</summary>
    Bottom,

    /// <summary>Toolbar is rendered to the left of the item list.</summary>
    Left
}

/// <summary>
/// A generic, selectable list component with optional toolbar (MoveUp / MoveDown / Remove /
/// TransferTo / TransferFrom / TransferAllTo / TransferAllFrom), pairing with another ListBox
/// via <see cref="Connected"/>, and HTML5 drag-and-drop between connected instances.
/// </summary>
/// <typeparam name="TItem">The ListBox item model type.</typeparam>
public partial class SunfishListBox<TItem> : SunfishComponentBase
{
    // ---- Data & fields ----

    /// <summary>The ListBox data collection.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>Property name on <typeparamref name="TItem"/> that holds the display text. Default <c>"Text"</c>.</summary>
    [Parameter] public string TextField { get; set; } = "Text";

    /// <summary>Property name on <typeparamref name="TItem"/> that holds the unique value. Default <c>"Value"</c>.</summary>
    [Parameter] public string ValueField { get; set; } = "Value";

    /// <summary>Property name on <typeparamref name="TItem"/> that indicates a disabled row.</summary>
    [Parameter] public string? DisabledField { get; set; }

    // ---- Selection ----

    /// <summary>Single or Multiple selection mode. Default <see cref="ListBoxSelectionMode.Single"/>.</summary>
    [Parameter] public ListBoxSelectionMode SelectionMode { get; set; } = ListBoxSelectionMode.Single;

    /// <summary>The currently selected items. Supports two-way binding via <see cref="SelectedItemsChanged"/>.</summary>
    [Parameter] public IEnumerable<TItem>? SelectedItems { get; set; }

    /// <summary>Fires when the selection changes.</summary>
    [Parameter] public EventCallback<IEnumerable<TItem>> SelectedItemsChanged { get; set; }

    // ---- Toolbar ----

    /// <summary>Shows the toolbar when <c>true</c>. Default <c>false</c>.</summary>
    [Parameter] public bool Toolbar { get; set; }

    /// <summary>
    /// Explicit toolbar tool set and order. When non-null, overrides the defaults built from
    /// <see cref="Toolbar"/> + <see cref="Connected"/>.
    /// </summary>
    [Parameter] public ICollection<ListBoxToolName>? Tools { get; set; }

    /// <summary>Toolbar position relative to the item list. Default <see cref="ListBoxToolBarPosition.Right"/>.</summary>
    [Parameter] public ListBoxToolBarPosition ToolBarPosition { get; set; } = ListBoxToolBarPosition.Right;

    // ---- Connected (paired) ListBoxes ----

    /// <summary>
    /// The <c>Id</c> of the connected (sibling) ListBox. When set, transfer tools operate between
    /// the two instances. Either this or the typed <see cref="ConnectedRef"/> may be set.
    /// </summary>
    [Parameter] public string? Connected { get; set; }

    /// <summary>
    /// Direct reference to the connected (sibling) ListBox. Preferred over <see cref="Connected"/>
    /// when you have an <c>@ref</c> handy, because it avoids id-matching lookup.
    /// </summary>
    [Parameter] public SunfishListBox<TItem>? ConnectedRef { get; set; }

    /// <summary>
    /// Enables HTML5 drag-and-drop between this ListBox and its connected sibling.
    /// Users can drag selected items and drop them onto the other list. Requires
    /// either <see cref="Connected"/> or <see cref="ConnectedRef"/> to be set.
    /// </summary>
    [Parameter] public bool DraggableConnected { get; set; }

    // ---- Events ----

    /// <summary>Fires when any toolbar action is invoked, before the per-action event.</summary>
    [Parameter] public EventCallback<ListBoxActionClickEventArgs<TItem>> OnActionClick { get; set; }

    /// <summary>Fires after <see cref="ListBoxToolName.MoveUp"/> or <see cref="ListBoxToolName.MoveDown"/>.</summary>
    [Parameter] public EventCallback<ListBoxReorderEventArgs<TItem>> OnReorder { get; set; }

    /// <summary>Fires after <see cref="ListBoxToolName.Remove"/>.</summary>
    [Parameter] public EventCallback<ListBoxRemoveEventArgs<TItem>> OnRemove { get; set; }

    /// <summary>Fires after any transfer tool is invoked.</summary>
    [Parameter] public EventCallback<ListBoxTransferEventArgs<TItem>> OnTransfer { get; set; }

    /// <summary>Fires after a connected-list drag-drop operation completes on this (source) ListBox.</summary>
    [Parameter] public EventCallback<ListBoxDropEventArgs<TItem>> OnDrop { get; set; }

    // ---- Appearance ----

    /// <summary>The <c>id</c> attribute of the root element. Also used for connect / drop-target lookup.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Outer height (e.g. <c>"200px"</c>, <c>"auto"</c>). Default <c>"200px"</c>.</summary>
    [Parameter] public string? Height { get; set; } = "200px";

    /// <summary>Outer width (e.g. <c>"180px"</c>). Default is unset (fills parent).</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Disables selection and toolbar interaction when <c>false</c>. Default <c>true</c>.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>The <c>aria-label</c> attribute of the list element.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>The <c>aria-labelledby</c> attribute of the list element.</summary>
    [Parameter] public string? AriaLabelledBy { get; set; }

    // ---- Templates ----

    /// <summary>Template used to render each item. When null, <see cref="TextField"/> is used.</summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Template shown when <see cref="Data"/> is empty. Falls back to <see cref="Placeholder"/>.</summary>
    [Parameter] public RenderFragment? NoDataTemplate { get; set; }

    /// <summary>Plain-text fallback shown when <see cref="Data"/> is empty and <see cref="NoDataTemplate"/> is null.</summary>
    [Parameter] public string? Placeholder { get; set; }

    // ---- Registry (for connect-by-id) ----

    private static readonly Dictionary<string, WeakReference<SunfishListBox<TItem>>> s_registry = new();

    // ---- Internal state ----

    private readonly string _listId = $"lb-{Guid.NewGuid():N}";
    private List<TItem> _selection = new();

    // Drag-drop state (instance-local; cross-instance lookup goes through registry).
    private static SunfishListBox<TItem>? s_currentDragSource;
    private static List<TItem> s_currentDragItems = new();
    private bool _dragActive;

    // ---- Lifecycle ----

    protected override void OnInitialized()
    {
        if (!string.IsNullOrEmpty(Id))
        {
            s_registry[Id] = new WeakReference<SunfishListBox<TItem>>(this);
        }
    }

    protected override void OnParametersSet()
    {
        if (SelectedItems is not null)
        {
            _selection = SelectedItems.ToList();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !string.IsNullOrEmpty(Id))
        {
            s_registry.Remove(Id);
        }
        base.Dispose(disposing);
    }

    // ---- Public API ----

    /// <summary>Forces a re-render; call after mutating <see cref="Data"/> externally.</summary>
    public void Rebind() => StateHasChanged();

    // ---- Toolbar derivation ----

    private IReadOnlyList<ListBoxToolName> EffectiveTools
    {
        get
        {
            if (Tools is not null) return Tools.ToList();
            if (!Toolbar) return Array.Empty<ListBoxToolName>();

            var list = new List<ListBoxToolName> { ListBoxToolName.MoveUp, ListBoxToolName.MoveDown, ListBoxToolName.Remove };
            if (HasConnection())
            {
                list.Add(ListBoxToolName.TransferTo);
                list.Add(ListBoxToolName.TransferFrom);
                list.Add(ListBoxToolName.TransferAllTo);
                list.Add(ListBoxToolName.TransferAllFrom);
            }
            return list;
        }
    }

    private ListBoxToolBarPosition EffectiveToolbarPosition => ToolBarPosition;

    private bool HasConnection() => ConnectedRef is not null || !string.IsNullOrEmpty(Connected);

    private SunfishListBox<TItem>? ResolveConnected()
    {
        if (ConnectedRef is not null) return ConnectedRef;
        if (!string.IsNullOrEmpty(Connected)
            && s_registry.TryGetValue(Connected, out var weak)
            && weak.TryGetTarget(out var target))
        {
            return target;
        }
        return null;
    }

    // ---- CSS ----

    private string RootCss()
    {
        var css = "sf-listbox";
        if (!Enabled) css += " sf-listbox--disabled";
        return CombineClasses(css);
    }

    private string? RootStyle()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Width)) parts.Add($"width:{Width}");
        return parts.Count == 0 ? null : string.Join(';', parts) + ';';
    }

    private string ListStyle()
    {
        var parts = new List<string> { "flex:1", "min-width:0" };
        if (!string.IsNullOrEmpty(Height) && Height != "auto")
        {
            parts.Add($"height:{Height}");
            parts.Add("overflow-y:auto");
        }
        return string.Join(';', parts);
    }

    private string ItemCss(bool isSelected, bool isDisabled)
    {
        var css = "sf-listbox__item";
        if (isSelected) css += " sf-listbox__item--selected";
        if (isDisabled) css += " sf-listbox__item--disabled";
        return css;
    }

    // ---- Selection helpers ----

    private bool IsItemSelected(TItem item)
    {
        var v = GetFieldValue(item, ValueField);
        foreach (var s in _selection)
        {
            if (object.Equals(GetFieldValue(s, ValueField), v)) return true;
        }
        return false;
    }

    private bool IsItemDisabled(TItem item)
    {
        if (!Enabled) return true;
        if (string.IsNullOrEmpty(DisabledField)) return false;
        var v = GetFieldValue(item, DisabledField);
        return v is bool b && b;
    }

    private async Task HandleSelect(MouseEventArgs e, TItem item)
    {
        if (!Enabled || IsItemDisabled(item)) return;
        if (SelectionMode == ListBoxSelectionMode.None) return;

        if (SelectionMode == ListBoxSelectionMode.Single)
        {
            _selection = new List<TItem> { item };
        }
        else
        {
            var value = GetFieldValue(item, ValueField);
            var idx = _selection.FindIndex(s => object.Equals(GetFieldValue(s, ValueField), value));
            if (idx >= 0) _selection.RemoveAt(idx);
            else _selection.Add(item);
        }

        await SelectedItemsChanged.InvokeAsync(_selection);
    }

    private static object? GetFieldValue(TItem item, string? fieldName)
    {
        if (item is null || string.IsNullOrEmpty(fieldName)) return null;
        var prop = item.GetType().GetProperty(fieldName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(item);
    }

    // ---- Toolbar glyph/label ----

    private static string GetToolLabel(ListBoxToolName tool) => tool switch
    {
        ListBoxToolName.MoveUp => "Move up",
        ListBoxToolName.MoveDown => "Move down",
        ListBoxToolName.Remove => "Remove",
        ListBoxToolName.TransferTo => "Transfer to",
        ListBoxToolName.TransferFrom => "Transfer from",
        ListBoxToolName.TransferAllTo => "Transfer all to",
        ListBoxToolName.TransferAllFrom => "Transfer all from",
        _ => tool.ToString()
    };

    private static string GetToolGlyph(ListBoxToolName tool) => tool switch
    {
        ListBoxToolName.MoveUp => "↑",          // ↑
        ListBoxToolName.MoveDown => "↓",        // ↓
        ListBoxToolName.Remove => "✕",          // ✕
        ListBoxToolName.TransferTo => "→",      // →
        ListBoxToolName.TransferFrom => "←",    // ←
        ListBoxToolName.TransferAllTo => "⇒",   // ⇒
        ListBoxToolName.TransferAllFrom => "⇐", // ⇐
        _ => "?"
    };

    // ---- Toolbar actions ----

    private async Task InvokeToolAsync(ListBoxToolName tool)
    {
        if (!Enabled) return;

        // Always fire unified click first (with IsCancelled escape hatch).
        var clickArgs = new ListBoxActionClickEventArgs<TItem>
        {
            Tool = tool,
            Items = _selection.ToList()
        };
        await OnActionClick.InvokeAsync(clickArgs);
        if (clickArgs.IsCancelled) return;

        switch (tool)
        {
            case ListBoxToolName.MoveUp:
                await MoveSelectionAsync(-1);
                break;
            case ListBoxToolName.MoveDown:
                await MoveSelectionAsync(+1);
                break;
            case ListBoxToolName.Remove:
                await RemoveSelectionAsync();
                break;
            case ListBoxToolName.TransferTo:
                await TransferAsync(selectedOnly: true, fromConnected: false);
                break;
            case ListBoxToolName.TransferFrom:
                await TransferAsync(selectedOnly: true, fromConnected: true);
                break;
            case ListBoxToolName.TransferAllTo:
                await TransferAsync(selectedOnly: false, fromConnected: false);
                break;
            case ListBoxToolName.TransferAllFrom:
                await TransferAsync(selectedOnly: false, fromConnected: true);
                break;
        }
    }

    private async Task MoveSelectionAsync(int delta)
    {
        if (Data is null || _selection.Count == 0) return;
        var list = Data.ToList();
        var indexes = _selection
            .Select(s => list.FindIndex(i => object.Equals(GetFieldValue(i, ValueField), GetFieldValue(s, ValueField))))
            .Where(i => i >= 0)
            .OrderBy(i => i)
            .ToList();
        if (indexes.Count == 0) return;

        if (delta < 0)
        {
            if (indexes.First() == 0) return;
            foreach (var i in indexes)
            {
                (list[i - 1], list[i]) = (list[i], list[i - 1]);
            }
        }
        else
        {
            if (indexes.Last() == list.Count - 1) return;
            foreach (var i in indexes.OrderByDescending(x => x))
            {
                (list[i + 1], list[i]) = (list[i], list[i + 1]);
            }
        }

        Data = list;
        var from = indexes.First();
        var to = delta < 0 ? from - 1 : from + 1;
        await OnReorder.InvokeAsync(new ListBoxReorderEventArgs<TItem>
        {
            FromIndex = from,
            ToIndex = to,
            Items = _selection.ToList()
        });
        StateHasChanged();
    }

    private async Task RemoveSelectionAsync()
    {
        if (Data is null || _selection.Count == 0) return;
        var removed = _selection.ToList();
        var remaining = Data
            .Where(d => !_selection.Any(s =>
                object.Equals(GetFieldValue(s, ValueField), GetFieldValue(d, ValueField))))
            .ToList();
        Data = remaining;
        _selection.Clear();
        await OnRemove.InvokeAsync(new ListBoxRemoveEventArgs<TItem> { Items = removed });
        await SelectedItemsChanged.InvokeAsync(_selection);
        StateHasChanged();
    }

    private async Task TransferAsync(bool selectedOnly, bool fromConnected)
    {
        var other = ResolveConnected();
        if (other is null) return;

        List<TItem> items;
        if (fromConnected)
        {
            items = selectedOnly ? other._selection.ToList() : (other.Data?.ToList() ?? new());
            if (items.Count == 0) return;

            other.Data = (other.Data ?? Enumerable.Empty<TItem>())
                .Where(d => !items.Any(s =>
                    object.Equals(GetFieldValue(s, ValueField), GetFieldValue(d, ValueField))))
                .ToList();
            Data = (Data ?? Enumerable.Empty<TItem>()).Concat(items).ToList();
            other._selection.Clear();
            other.StateHasChanged();
        }
        else
        {
            items = selectedOnly ? _selection.ToList() : (Data?.ToList() ?? new());
            if (items.Count == 0) return;

            Data = (Data ?? Enumerable.Empty<TItem>())
                .Where(d => !items.Any(s =>
                    object.Equals(GetFieldValue(s, ValueField), GetFieldValue(d, ValueField))))
                .ToList();
            other.Data = (other.Data ?? Enumerable.Empty<TItem>()).Concat(items).ToList();
            _selection.Clear();
            other.StateHasChanged();
        }

        var destinationId = fromConnected ? Id : other.Id;
        var tool = (selectedOnly, fromConnected) switch
        {
            (true, false) => ListBoxToolName.TransferTo,
            (true, true) => ListBoxToolName.TransferFrom,
            (false, false) => ListBoxToolName.TransferAllTo,
            (false, true) => ListBoxToolName.TransferAllFrom,
        };

        await OnTransfer.InvokeAsync(new ListBoxTransferEventArgs<TItem>
        {
            Tool = tool,
            DestinationListBoxId = destinationId,
            Items = items
        });
        await SelectedItemsChanged.InvokeAsync(_selection);
        StateHasChanged();
    }

    // ---- Drag and drop ----

    private void HandleDragStart(DragEventArgs e, TItem item, int index)
    {
        if (!DraggableConnected || !Enabled) return;

        // If the dragged item is part of the current selection, drag the whole selection;
        // otherwise drag just this one item.
        var isInSelection = IsItemSelected(item);
        s_currentDragItems = isInSelection && _selection.Count > 0
            ? _selection.ToList()
            : new List<TItem> { item };
        s_currentDragSource = this;
        _dragActive = true;
    }

    private void HandleDragOver(DragEventArgs e)
    {
        if (!DraggableConnected) return;
        if (s_currentDragSource is null) return;
        _dragActive = true;
    }

    private void HandleDragOverItem(DragEventArgs e, int index)
    {
        if (!DraggableConnected) return;
        if (s_currentDragSource is null) return;
        _dragActive = true;
    }

    private void HandleDragEnd(DragEventArgs e)
    {
        _dragActive = false;
        s_currentDragSource = null;
        s_currentDragItems = new();
    }

    private async Task HandleDropOnItem(DragEventArgs e, int index)
    {
        await HandleDropCoreAsync(index);
    }

    private async Task HandleDropOnList(DragEventArgs e)
    {
        await HandleDropCoreAsync(null);
    }

    private async Task HandleDropCoreAsync(int? destinationIndex)
    {
        if (!DraggableConnected) return;
        var source = s_currentDragSource;
        if (source is null) return;
        var items = s_currentDragItems;
        if (items.Count == 0) return;

        // Remove from source, insert into this (destination).
        if (!ReferenceEquals(source, this))
        {
            source.Data = (source.Data ?? Enumerable.Empty<TItem>())
                .Where(d => !items.Any(s =>
                    object.Equals(GetFieldValue(s, ValueField), GetFieldValue(d, ValueField))))
                .ToList();
            source._selection.Clear();
        }

        var destList = (Data ?? Enumerable.Empty<TItem>()).ToList();
        // Remove any duplicates from destination first (so moving within same list works).
        destList = destList
            .Where(d => !items.Any(s =>
                object.Equals(GetFieldValue(s, ValueField), GetFieldValue(d, ValueField))))
            .ToList();

        if (destinationIndex is null || destinationIndex.Value < 0 || destinationIndex.Value > destList.Count)
        {
            destList.AddRange(items);
        }
        else
        {
            destList.InsertRange(destinationIndex.Value, items);
        }
        Data = destList;

        // Fire OnDrop from the SOURCE (per spec).
        await source.OnDrop.InvokeAsync(new ListBoxDropEventArgs<TItem>
        {
            DestinationIndex = destinationIndex,
            DestinationListBoxId = Id,
            Items = items
        });

        _dragActive = false;
        s_currentDragSource = null;
        s_currentDragItems = new();

        if (!ReferenceEquals(source, this)) source.StateHasChanged();
        StateHasChanged();
    }
}
