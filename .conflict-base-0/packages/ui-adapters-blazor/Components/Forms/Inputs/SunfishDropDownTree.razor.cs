using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Sunfish.Foundation.Models;
using Sunfish.UIAdapters.Blazor.Base;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// A dropdown that presents hierarchical (tree) data inside a popup panel.
/// Selection on a node sets <c>Value</c> and closes the popup.
///
/// <para>
/// MVP surface (ADR 0022 canonical API, Tier 3 W3-7):
/// <list type="bullet">
///   <item><c>Data</c> — root items; nested children live in <c>ItemsField</c>.</item>
///   <item><c>TextField</c>, <c>ValueField</c>, <c>ItemsField</c>, <c>IdField</c>,
///     <c>ParentIdField</c> — field-name parameters for reflection-based
///     property access on the item type.</item>
///   <item><c>Value</c> / <c>ValueChanged</c> — two-way binding of the selected
///     item's <c>ValueField</c> value.</item>
///   <item><c>Placeholder</c>, <c>Disabled</c>, <c>ReadOnly</c>,
///     <c>PopupWidth</c> (default <c>"300px"</c>), <c>PopupHeight</c>
///     (default <c>"200px"</c>).</item>
///   <item><c>OnOpen</c> / <c>OnClose</c> — cancellable via
///     <see cref="PopupEventArgs"/>.</item>
/// </list>
/// </para>
///
/// <para>
/// The popup composes an embedded tree view and a lightweight filter input
/// when <c>Filterable</c> is enabled.
/// </para>
/// </summary>
public partial class SunfishDropDownTree : SunfishComponentBase
{
    // ── Data & field bindings ──────────────────────────────────────────

    /// <summary>
    /// The hierarchical data to display. Items expose their children via
    /// <see cref="ItemsField"/> (nested) or via <see cref="IdField"/> /
    /// <see cref="ParentIdField"/> (flat).
    /// </summary>
    [Parameter] public IEnumerable<object>? Data { get; set; }

    /// <summary>The currently selected item's <see cref="ValueField"/> value.</summary>
    [Parameter] public object? Value { get; set; }

    /// <summary>Fires when <see cref="Value"/> changes (two-way binding).</summary>
    [Parameter] public EventCallback<object?> ValueChanged { get; set; }

    /// <summary>Fires after a successful selection (alias / extra hook).</summary>
    [Parameter] public EventCallback<object?> OnChange { get; set; }

    /// <summary>Property name that yields the display text. Default: <c>"Text"</c>.</summary>
    [Parameter] public string TextField { get; set; } = "Text";

    /// <summary>Property name that yields the logical value. Default: <c>"Value"</c>.</summary>
    [Parameter] public string ValueField { get; set; } = "Value";

    /// <summary>Property name for nested children. Default: <c>"Items"</c>.</summary>
    [Parameter] public string ItemsField { get; set; } = "Items";

    /// <summary>Property name for the item id (flat-data mode). Default: <c>"Id"</c>.</summary>
    [Parameter] public string IdField { get; set; } = "Id";

    /// <summary>Property name for the parent id (flat-data mode). Default: <c>"ParentId"</c>.</summary>
    [Parameter] public string ParentIdField { get; set; } = "ParentId";

    /// <summary>Property name for the has-children hint. Default: <c>"HasChildren"</c>.</summary>
    [Parameter] public string HasChildrenField { get; set; } = "HasChildren";

    // ── Presentation ───────────────────────────────────────────────────

    /// <summary>Placeholder shown when no item is selected.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>When <c>true</c>, shows a search box inside the popup.</summary>
    [Parameter] public bool Filterable { get; set; }

    /// <summary>Whether the component is enabled. Kept for source compatibility.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether the component is disabled. Mirrors <see cref="Enabled"/> = <c>false</c>.
    /// When both are set, <c>Disabled = true</c> wins.
    /// </summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// Read-only mode. The control is focusable and shows the current value,
    /// but the popup cannot be opened.
    /// </summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Control width (CSS).</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Popup height (CSS). Default: <c>"200px"</c>.</summary>
    [Parameter] public string? PopupHeight { get; set; } = "200px";

    /// <summary>Popup width (CSS). Default: <c>"300px"</c>.</summary>
    [Parameter] public string? PopupWidth { get; set; } = "300px";

    /// <summary>Optional per-item render template.</summary>
    [Parameter] public RenderFragment<object>? ItemTemplate { get; set; }

    // ── Popup lifecycle events ─────────────────────────────────────────

    /// <summary>
    /// Fires before the popup opens. Handlers may set
    /// <see cref="PopupEventArgs.IsCancelled"/> = <c>true</c> to keep the
    /// popup closed.
    /// </summary>
    [Parameter] public EventCallback<PopupEventArgs> OnOpen { get; set; }

    /// <summary>
    /// Fires before the popup closes. Handlers may set
    /// <see cref="PopupEventArgs.IsCancelled"/> = <c>true</c> to keep the
    /// popup open.
    /// </summary>
    [Parameter] public EventCallback<PopupEventArgs> OnClose { get; set; }

    // ── Internal state ─────────────────────────────────────────────────

    private bool _isOpen;
    private string _filterText = string.Empty;
    private object? _selectedItem;

    private bool IsInteractive => Enabled && !Disabled && !ReadOnly;

    private string? WidthStyle() =>
        !string.IsNullOrEmpty(Width) ? $"width:{Width}" : null;

    private string PopupStyles()
    {
        var styles = new List<string>();
        if (!string.IsNullOrEmpty(PopupHeight)) styles.Add($"max-height:{PopupHeight}");
        if (!string.IsNullOrEmpty(PopupWidth)) styles.Add($"width:{PopupWidth}");
        styles.Add("overflow-y:auto");
        return string.Join(";", styles);
    }

    private async Task ToggleDropdown()
    {
        if (!IsInteractive) return;

        var args = new PopupEventArgs();
        if (_isOpen)
        {
            await OnClose.InvokeAsync(args);
            if (args.IsCancelled) return;
            _isOpen = false;
        }
        else
        {
            await OnOpen.InvokeAsync(args);
            if (args.IsCancelled) return;
            _isOpen = true;
            _filterText = string.Empty;
        }
    }

    private void OnFilterInput(ChangeEventArgs e)
    {
        _filterText = e.Value?.ToString() ?? string.Empty;
    }

    private RenderFragment RenderNodes(IEnumerable<object> items, int depth) => __builder =>
    {
        foreach (var item in items)
        {
            var text = GetFieldValue(item, TextField);

            if (Filterable && !string.IsNullOrEmpty(_filterText) &&
                !text.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            __builder.OpenElement(0, "div");
            __builder.AddAttribute(1, "class", "sf-dropdowntree__node");
            __builder.AddAttribute(2, "style", $"padding-left:{depth * 20}px");
            __builder.AddAttribute(3, "role", "treeitem");
            __builder.AddAttribute(4, "onclick",
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, () => SelectItem(item)));
            __builder.AddEventStopPropagationAttribute(5, "onclick", true);

            if (ItemTemplate is not null)
            {
                __builder.AddContent(6, ItemTemplate(item));
            }
            else
            {
                __builder.OpenElement(7, "span");
                __builder.AddContent(8, text);
                __builder.CloseElement();
            }
            __builder.CloseElement();

            var children = GetChildren(item);
            if (children is not null)
            {
                __builder.AddContent(9, RenderNodes(children, depth + 1));
            }
        }
    };

    private async Task SelectItem(object item)
    {
        var value = GetFieldValue(item, ValueField);
        Value = value;
        _selectedItem = item;

        // Close via cancellable OnClose; selection proceeds regardless.
        var args = new PopupEventArgs();
        await OnClose.InvokeAsync(args);
        if (!args.IsCancelled)
        {
            _isOpen = false;
        }

        await ValueChanged.InvokeAsync(value);
        await OnChange.InvokeAsync(value);
    }

    private string GetFieldValue(object item, string fieldName)
    {
        var prop = item.GetType().GetProperty(fieldName);
        return prop?.GetValue(item)?.ToString() ?? string.Empty;
    }

    private IEnumerable<object>? GetChildren(object item)
    {
        var prop = item.GetType().GetProperty(ItemsField);
        return prop?.GetValue(item) as IEnumerable<object>;
    }
}
