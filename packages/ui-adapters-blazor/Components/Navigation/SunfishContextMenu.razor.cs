using System.Reflection;
using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.UIAdapters.Blazor.Components.Navigation;

public partial class SunfishContextMenu : SunfishComponentBase
{
    private bool _isOpen;
    private double _x;
    private double _y;
    private int _focusedIndex = -1;

    // ── Parameters: Content ────────────────────────────────────────────

    /// <summary>Trigger content (the element that responds to right-click).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Menu content (rendered inside the context menu popup).</summary>
    [Parameter] public RenderFragment? MenuContent { get; set; }

    /// <summary>
    /// CSS selector for external target elements. When set, the inline trigger (ChildContent) is not used.
    /// Requires JS interop for selector-based targeting.
    /// </summary>
    [Parameter] public string? Selector { get; set; }

    // ── Parameters: Events ─────────────────────────────────────────────

    /// <summary>Fires when a data-bound menu item is clicked.</summary>
    [Parameter] public EventCallback<object> OnClick { get; set; }

    /// <summary>Fires when the context menu opens.</summary>
    [Parameter] public EventCallback OnShow { get; set; }

    /// <summary>Fires when the context menu closes.</summary>
    [Parameter] public EventCallback OnHide { get; set; }

    // ── Parameters: Templates ──────────────────────────────────────────

    /// <summary>Custom template for rendering individual menu items.</summary>
    [Parameter] public RenderFragment<object>? ItemTemplate { get; set; }

    // ── Parameters: Data Binding ────────────────────────────────────────

    /// <summary>Data source for menu items.</summary>
    [Parameter] public IEnumerable<object>? Data { get; set; }

    /// <summary>Field name for item text.</summary>
    [Parameter] public string TextField { get; set; } = "Text";

    /// <summary>Field name for item icon.</summary>
    [Parameter] public string IconField { get; set; } = "Icon";

    /// <summary>Field name for separator flag.</summary>
    [Parameter] public string SeparatorField { get; set; } = "Separator";

    /// <summary>Field name for disabled flag.</summary>
    [Parameter] public string DisabledField { get; set; } = "Disabled";

    /// <summary>Field name for child items collection (enables hierarchical menus).</summary>
    [Parameter] public string ItemsField { get; set; } = "Items";

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Shows the context menu at the specified position.</summary>
    public async Task ShowAsync(double x, double y)
    {
        _x = x;
        _y = y;
        _isOpen = true;
        _focusedIndex = -1;
        StateHasChanged();

        if (OnShow.HasDelegate)
            await OnShow.InvokeAsync();
    }

    /// <summary>Hides the context menu.</summary>
    public async Task HideAsync()
    {
        _isOpen = false;
        StateHasChanged();

        if (OnHide.HasDelegate)
            await OnHide.InvokeAsync();
    }

    // ── Rendering ───────────────────────────────────────────────────────

    internal RenderFragment RenderItems(IEnumerable<object> items) => builder =>
    {
        var index = 0;

        foreach (var item in items)
        {
            var isSeparator = GetBool(item, SeparatorField);
            var isDisabled = GetBool(item, DisabledField);
            var text = GetString(item, TextField);
            var icon = GetString(item, IconField);
            var children = GetChildren(item);
            var hasChildren = children != null && children.Any();

            if (isSeparator)
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", CssProvider.MenuDividerClass());
                builder.AddAttribute(2, "role", "separator");
                builder.CloseElement();
            }
            else
            {
                var clickItem = item;
                var currentIndex = index;
                var isFocused = currentIndex == _focusedIndex;
                var focusedClass = isFocused ? " mar-context-menu__item--focused" : "";
                var wrapperClass = "mar-context-menu__item-wrapper"
                    + (hasChildren ? " mar-context-menu__item-wrapper--has-children" : "");

                // Wrapper div
                builder.OpenElement(10, "div");
                builder.AddAttribute(11, "class", wrapperClass);

                // Button
                builder.OpenElement(20, "button");
                builder.AddAttribute(21, "type", "button");
                builder.AddAttribute(22, "class", CssProvider.MenuItemClass(isDisabled) + focusedClass);
                builder.AddAttribute(23, "role", "menuitem");
                builder.AddAttribute(24, "disabled", isDisabled);
                if (hasChildren)
                    builder.AddAttribute(25, "aria-haspopup", "true");
                builder.AddAttribute(26, "data-index", currentIndex);
                builder.AddAttribute(27, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => HandleItemClick(clickItem)));

                if (ItemTemplate != null)
                {
                    builder.AddContent(30, ItemTemplate(clickItem));
                }
                else
                {
                    if (!string.IsNullOrEmpty(icon))
                    {
                        builder.OpenElement(40, "span");
                        builder.AddAttribute(41, "class", "mar-menu-item__icon");
                        builder.AddContent(42, icon);
                        builder.CloseElement();
                    }

                    builder.OpenElement(50, "span");
                    builder.AddAttribute(51, "class", "mar-menu-item__text");
                    builder.AddContent(52, text);
                    builder.CloseElement();

                    if (hasChildren)
                    {
                        builder.OpenElement(60, "span");
                        builder.AddAttribute(61, "class", "mar-menu-item__arrow");
                        builder.AddContent(62, "▸");
                        builder.CloseElement();
                    }
                }

                builder.CloseElement(); // button

                // Submenu
                if (hasChildren)
                {
                    builder.OpenElement(70, "div");
                    builder.AddAttribute(71, "class", "mar-context-menu__submenu");
                    builder.AddAttribute(72, "role", "menu");
                    builder.AddContent(73, RenderItems(children!));
                    builder.CloseElement();
                }

                builder.CloseElement(); // wrapper div

                index++;
            }
        }
    };

    // ── Event handlers ──────────────────────────────────────────────────

    private async Task OnContextMenu(MouseEventArgs e)
    {
        _x = e.ClientX;
        _y = e.ClientY;
        _isOpen = true;
        _focusedIndex = -1;

        if (OnShow.HasDelegate)
            await OnShow.InvokeAsync();
    }

    private async Task HandleItemClick(object item)
    {
        var children = GetChildren(item);
        if (children != null && children.Any()) return; // Don't close on parent click

        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync(item);

        _isOpen = false;

        if (OnHide.HasDelegate)
            await OnHide.InvokeAsync();
    }

    private async Task Close()
    {
        _isOpen = false;

        if (OnHide.HasDelegate)
            await OnHide.InvokeAsync();
    }

    private async Task CloseOnContext(MouseEventArgs e)
    {
        _isOpen = false;

        if (OnHide.HasDelegate)
            await OnHide.InvokeAsync();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            await HideAsync();
            return;
        }

        if (Data == null) return;

        var flatItems = GetFlatItems();
        var itemCount = flatItems.Count;
        if (itemCount == 0) return;

        switch (e.Key)
        {
            case "ArrowDown":
                _focusedIndex = _focusedIndex < itemCount - 1 ? _focusedIndex + 1 : 0;
                SkipDisabledForward(flatItems, itemCount);
                break;

            case "ArrowUp":
                _focusedIndex = _focusedIndex > 0 ? _focusedIndex - 1 : itemCount - 1;
                SkipDisabledBackward(flatItems, itemCount);
                break;

            case "Home":
                _focusedIndex = 0;
                SkipDisabledForward(flatItems, itemCount);
                break;

            case "End":
                _focusedIndex = itemCount - 1;
                SkipDisabledBackward(flatItems, itemCount);
                break;

            case "Enter":
            case " ":
                if (_focusedIndex >= 0 && _focusedIndex < itemCount)
                {
                    await HandleItemClick(flatItems[_focusedIndex]);
                }
                break;

            case "ArrowRight":
                if (_focusedIndex >= 0 && _focusedIndex < itemCount)
                {
                    var children = GetChildren(flatItems[_focusedIndex]);
                    if (children != null && children.Any())
                    {
                        // Move focus into the submenu (first child)
                        _focusedIndex++;
                        SkipDisabledForward(flatItems, itemCount);
                    }
                }
                break;

            case "ArrowLeft":
                if (_focusedIndex > 0)
                {
                    // Move focus back to nearest parent-level item
                    _focusedIndex--;
                    SkipDisabledBackward(flatItems, itemCount);
                }
                break;
        }
    }

    // ── Keyboard navigation helpers ─────────────────────────────────────

    /// <summary>Builds a flat list of non-separator items in render order (depth-first).</summary>
    private List<object> GetFlatItems()
    {
        var result = new List<object>();
        if (Data != null)
            CollectFlatItems(Data, result);
        return result;
    }

    private void CollectFlatItems(IEnumerable<object> items, List<object> result)
    {
        foreach (var item in items)
        {
            if (GetBool(item, SeparatorField))
                continue;

            result.Add(item);

            var children = GetChildren(item);
            if (children != null && children.Any())
                CollectFlatItems(children, result);
        }
    }

    /// <summary>Advances <see cref="_focusedIndex"/> forward past disabled items, wrapping if needed.</summary>
    private void SkipDisabledForward(List<object> flatItems, int count)
    {
        var start = _focusedIndex;
        while (GetBool(flatItems[_focusedIndex], DisabledField))
        {
            _focusedIndex = (_focusedIndex + 1) % count;
            if (_focusedIndex == start) break; // all disabled
        }
    }

    /// <summary>Advances <see cref="_focusedIndex"/> backward past disabled items, wrapping if needed.</summary>
    private void SkipDisabledBackward(List<object> flatItems, int count)
    {
        var start = _focusedIndex;
        while (GetBool(flatItems[_focusedIndex], DisabledField))
        {
            _focusedIndex = (_focusedIndex - 1 + count) % count;
            if (_focusedIndex == start) break; // all disabled
        }
    }

    // ── Reflection helpers ──────────────────────────────────────────────

    private static string? GetString(object item, string field)
    {
        var prop = item.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(item)?.ToString();
    }

    private static bool GetBool(object item, string field)
    {
        var prop = item.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(item) is true;
    }

    private IEnumerable<object>? GetChildren(object item)
    {
        var prop = item.GetType().GetProperty(ItemsField, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop?.GetValue(item) is System.Collections.IEnumerable enumerable)
        {
            var list = enumerable.Cast<object>().ToList();
            return list.Count > 0 ? list : null;
        }
        return null;
    }
}
