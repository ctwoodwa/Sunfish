using System.Reflection;
using Sunfish.Foundation.Base;
using Sunfish.Components.Blazor.Base;
using Sunfish.Foundation.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.Components.Blazor.Components.Navigation;

public partial class SunfishMenu : SunfishComponentBase
{
    // ── Parameters ──────────────────────────────────────────────────────

    /// <summary>Whether the menu is open.</summary>
    [Parameter] public bool IsOpen { get; set; }

    /// <summary>Fires when the open state changes (supports two-way binding).</summary>
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    /// <summary>Whether clicking a menu item closes the menu. Defaults to true.</summary>
    [Parameter] public bool CloseOnClick { get; set; } = true;

    /// <summary>Fires when a menu item is clicked. Receives the data item.</summary>
    [Parameter] public EventCallback<object> OnClick { get; set; }

    /// <summary>Child content rendered when <see cref="Data"/> is not set.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Custom template for rendering individual menu items.</summary>
    [Parameter] public RenderFragment<object>? ItemTemplate { get; set; }

    /// <summary>Determines how the menu is triggered. Defaults to <see cref="MenuShowOn.Click"/>.</summary>
    [Parameter] public MenuShowOn ShowOn { get; set; } = MenuShowOn.Click;

    // ── Data-binding parameters ──────────────────────────────────────

    [Parameter] public IEnumerable<object>? Data { get; set; }
    [Parameter] public string TextField { get; set; } = "Text";
    [Parameter] public string IconField { get; set; } = "Icon";
    [Parameter] public string UrlField { get; set; } = "Url";
    [Parameter] public string SeparatorField { get; set; } = "Separator";
    [Parameter] public string DisabledField { get; set; } = "Disabled";
    [Parameter] public string IdField { get; set; } = "Id";
    [Parameter] public string ParentIdField { get; set; } = "ParentId";
    [Parameter] public string ItemsField { get; set; } = "Items";
    [Parameter] public string HasChildrenField { get; set; } = "HasChildren";

    // ── State ───────────────────────────────────────────────────────────

    private int _focusedIndex = -1;

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Shows the menu programmatically.</summary>
    public async Task ShowAsync()
    {
        IsOpen = true;
        await IsOpenChanged.InvokeAsync(true);
        StateHasChanged();
    }

    /// <summary>Hides the menu programmatically.</summary>
    public async Task HideAsync()
    {
        IsOpen = false;
        await IsOpenChanged.InvokeAsync(false);
        StateHasChanged();
    }

    // ── Hierarchical data building ──────────────────────────────────────

    private record MenuNode(object Item, string? Text, string? Icon, string? Url, bool IsSeparator, bool IsDisabled, List<MenuNode> Children);

    private List<MenuNode> BuildMenuTree()
    {
        if (Data == null) return [];

        var items = Data.ToList();

        // Check if hierarchical (has ItemsField)
        if (items.Any(i => GetValue(i, ItemsField) is System.Collections.IEnumerable))
            return BuildHierarchical(items);

        // Check if flat with parent/child
        if (items.Any(i => GetString(i, ParentIdField) != null))
            return BuildFlat(items);

        // Simple flat list
        return items.Select(ToMenuNode).ToList();
    }

    private List<MenuNode> BuildHierarchical(IEnumerable<object> items)
    {
        return items.Select(item =>
        {
            var children = new List<MenuNode>();
            if (GetValue(item, ItemsField) is System.Collections.IEnumerable childEnum)
                children = BuildHierarchical(childEnum.Cast<object>());
            return new MenuNode(item, GetString(item, TextField), GetString(item, IconField),
                GetString(item, UrlField), GetBool(item, SeparatorField), GetBool(item, DisabledField), children);
        }).ToList();
    }

    private List<MenuNode> BuildFlat(List<object> items)
    {
        var lookup = new Dictionary<string, MenuNode>();
        var roots = new List<MenuNode>();

        foreach (var item in items)
        {
            var id = GetString(item, IdField) ?? "";
            lookup[id] = new MenuNode(item, GetString(item, TextField), GetString(item, IconField),
                GetString(item, UrlField), GetBool(item, SeparatorField), GetBool(item, DisabledField), []);
        }

        foreach (var item in items)
        {
            var id = GetString(item, IdField) ?? "";
            var parentId = GetString(item, ParentIdField);
            if (string.IsNullOrEmpty(parentId) || !lookup.ContainsKey(parentId))
                roots.Add(lookup[id]);
            else
                lookup[parentId].Children.Add(lookup[id]);
        }

        return roots;
    }

    private MenuNode ToMenuNode(object item) =>
        new(item, GetString(item, TextField), GetString(item, IconField),
            GetString(item, UrlField), GetBool(item, SeparatorField), GetBool(item, DisabledField), []);

    // ── Rendering ───────────────────────────────────────────────────────

    private RenderFragment RenderMenuItems(List<MenuNode> nodes) => builder =>
    {
        var index = 0;
        foreach (var node in nodes)
        {
            if (node.IsSeparator)
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", CssProvider.MenuDividerClass());
                builder.AddAttribute(2, "role", "separator");
                builder.CloseElement();
                continue;
            }

            var hasChildren = node.Children.Any();
            var currentIndex = index;
            var focusedClass = _focusedIndex == currentIndex ? " mar-menu-item--focused" : "";

            if (!string.IsNullOrEmpty(node.Url))
            {
                builder.OpenElement(10, "a");
                builder.AddAttribute(11, "class", CssProvider.MenuItemClass(node.IsDisabled) + focusedClass);
                builder.AddAttribute(12, "href", node.Url);
                builder.AddAttribute(13, "role", "menuitem");
                builder.AddAttribute(14, "aria-disabled", node.IsDisabled);
                builder.AddAttribute(15, "data-index", currentIndex);
                var clickItem = node.Item;
                builder.AddAttribute(16, "onclick", EventCallback.Factory.Create(this, () => HandleItemClick(clickItem)));
            }
            else
            {
                builder.OpenElement(10, "button");
                builder.AddAttribute(11, "type", "button");
                builder.AddAttribute(12, "class", CssProvider.MenuItemClass(node.IsDisabled) + focusedClass);
                builder.AddAttribute(13, "role", "menuitem");
                builder.AddAttribute(14, "disabled", node.IsDisabled);
                builder.AddAttribute(15, "data-index", currentIndex);
                if (hasChildren) builder.AddAttribute(16, "aria-haspopup", "true");
                var clickItem = node.Item;
                builder.AddAttribute(17, "onclick", EventCallback.Factory.Create(this, () => HandleItemClick(clickItem)));
            }

            if (ItemTemplate != null)
            {
                builder.AddContent(20, ItemTemplate(node.Item));
            }
            else
            {
                if (!string.IsNullOrEmpty(node.Icon))
                {
                    builder.OpenElement(20, "span");
                    builder.AddAttribute(21, "class", "mar-menu-item__icon");
                    builder.AddContent(22, node.Icon);
                    builder.CloseElement();
                }

                builder.AddContent(25, node.Text);

                if (hasChildren)
                {
                    builder.OpenElement(30, "span");
                    builder.AddAttribute(31, "class", "mar-menu-item__arrow");
                    builder.AddContent(32, "\u25B6");
                    builder.CloseElement();
                }
            }

            builder.CloseElement(); // a or button

            // Render sub-menu
            if (hasChildren)
            {
                builder.OpenElement(40, "div");
                builder.AddAttribute(41, "class", "mar-menu__submenu");
                builder.AddAttribute(42, "role", "menu");
                builder.AddContent(43, RenderMenuItems(node.Children));
                builder.CloseElement();
            }

            index++;
        }
    };

    // ── Keyboard navigation ─────────────────────────────────────────────

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (Data == null) return;

        var nodes = BuildMenuTree().Where(n => !n.IsSeparator && !n.IsDisabled).ToList();
        var count = nodes.Count;
        if (count == 0) return;

        switch (e.Key)
        {
            case "Escape":
                await HandleClose();
                break;

            case "ArrowDown":
                _focusedIndex = _focusedIndex < count - 1 ? _focusedIndex + 1 : 0;
                break;

            case "ArrowUp":
                _focusedIndex = _focusedIndex > 0 ? _focusedIndex - 1 : count - 1;
                break;

            case "Home":
                _focusedIndex = 0;
                break;

            case "End":
                _focusedIndex = count - 1;
                break;

            case "Enter":
            case " ":
                if (_focusedIndex >= 0 && _focusedIndex < count)
                    await HandleItemClick(nodes[_focusedIndex].Item);
                break;
        }
    }

    // ── Event handlers ──────────────────────────────────────────────────

    private async Task HandleItemClick(object item)
    {
        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync(item);

        if (CloseOnClick)
            await HandleClose();
    }

    private async Task HandleClose()
    {
        IsOpen = false;
        _focusedIndex = -1;
        if (IsOpenChanged.HasDelegate)
            await IsOpenChanged.InvokeAsync(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

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

    private static object? GetValue(object item, string field)
    {
        var prop = item.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(item);
    }
}
