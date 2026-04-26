using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Components.Navigation;

public partial class SunfishTreeItem : SunfishComponentBase
{
    [CascadingParameter] public SunfishTreeView? TreeView { get; set; }

    /// <summary>Unique identifier for this item.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Display text for the tree item.</summary>
    [Parameter] public string Title { get; set; } = "";

    /// <summary>Icon name/class for the tree item.</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>Navigation URL. When set, the title renders as a link.</summary>
    [Parameter] public string? Url { get; set; }

    /// <summary>Whether this item is expanded.</summary>
    [Parameter] public bool IsExpanded { get; set; }

    /// <summary>Fires when the expanded state changes.</summary>
    [Parameter] public EventCallback<bool> IsExpandedChanged { get; set; }

    /// <summary>Whether this item is selected.</summary>
    [Parameter] public bool IsSelected { get; set; }

    /// <summary>Fires when clicked.</summary>
    [Parameter] public EventCallback OnClick { get; set; }

    /// <summary>Child tree items.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Whether this item is rendered inside a parent <c>role="tree"</c> container.
    /// When unset, derives from the cascaded <see cref="SunfishTreeView"/> presence to
    /// avoid emitting an orphan <c>role="treeitem"</c> (axe <c>aria-required-parent</c>).
    /// </summary>
    [Parameter] public bool? InTreeOverride { get; set; }

    private bool InTree => InTreeOverride ?? (TreeView is not null);

    private bool HasChildren => ChildContent != null;

    private string ItemClass =>
        CssProvider.TreeItemClass(IsExpanded, IsSelected)
        + (TreeView?.IsItemFocused(Id ?? Title) == true ? " mar-tree-item--focused" : "");

    private static string CheckboxClass(bool? checkState) =>
        "mar-tree-item__checkbox"
        + (checkState == null ? " mar-tree-item__checkbox--indeterminate" : "");

    private async Task ToggleExpanded()
    {
        if (TreeView?.Disabled == true || TreeView?.ReadOnly == true) return;
        IsExpanded = !IsExpanded;
        await IsExpandedChanged.InvokeAsync(IsExpanded);
    }

    private async Task HandleClick()
    {
        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync();
    }

    private void OnCheckboxChanged(ChangeEventArgs e)
    {
        TreeView?.ToggleItemChecked(Id ?? Title);
    }
}
