using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Internal;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.UIAdapters.Blazor.Components.Navigation;

public partial class SunfishTreeView : SunfishComponentBase
{
    // ── Internal state ──────────────────────────────────────────────────
    private HashSet<string> _expandedIds = new();
    private HashSet<string> _checkedIds = new();
    private HashSet<string> _selectedIds = new();
    private readonly HashSet<string> _loadingIds = new();
    private readonly HashSet<string> _loadedNodeIds = new();
    private readonly Dictionary<string, List<object>> _lazyLoadedChildren = new();
    private string? _draggedNodeId;
    private string? _dragOverNodeId;
    private string? _focusedNodeId;
    private bool _preventKeyDefault;
    private List<TreeNode>? _cachedTree;
    private string? _editingNodeId;
    private string _editingText = "";

    // ── Parameters: Content ─────────────────────────────────────────────

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public RenderFragment<object>? ItemTemplate { get; set; }

    // ── Parameters: Data Binding ────────────────────────────────────────

    [Parameter] public IEnumerable<object>? Data { get; set; }
    [Parameter] public string? IdField { get; set; }
    [Parameter] public string? ParentIdField { get; set; }
    [Parameter] public string? TextField { get; set; }
    [Parameter] public string? IconField { get; set; }
    [Parameter] public string? ItemsField { get; set; }
    [Parameter] public string? HasChildrenField { get; set; }

    // ── Parameters: Checkbox ────────────────────────────────────────────

    /// <summary>Controls checkbox display mode (None, Single, Multiple).</summary>
    [Parameter] public CheckBoxMode CheckBoxMode { get; set; } = CheckBoxMode.None;

    /// <summary>When true, checking a parent cascades to all descendants.</summary>
    [Parameter] public bool AllowCheckChildren { get; set; } = true;

    /// <summary>When true, child check changes update ancestor checked/indeterminate state.</summary>
    [Parameter] public bool AllowCheckParents { get; set; } = true;

    /// <summary>The currently checked item IDs. Supports two-way binding.</summary>
    [Parameter] public IEnumerable<string>? CheckedItems { get; set; }

    /// <summary>Fires when checked items change.</summary>
    [Parameter] public EventCallback<IEnumerable<string>> CheckedItemsChanged { get; set; }

    // ── Parameters: Selection ───────────────────────────────────────────

    /// <summary>Controls selection behavior (None, Single, Multiple).</summary>
    [Parameter] public TreeSelectionMode SelectionMode { get; set; } = TreeSelectionMode.Single;

    /// <summary>The currently selected item IDs. Supports two-way binding.</summary>
    [Parameter] public IEnumerable<string>? SelectedItems { get; set; }

    /// <summary>Fires when selected items change.</summary>
    [Parameter] public EventCallback<IEnumerable<string>> SelectedItemsChanged { get; set; }

    /// <summary>Fires when a tree item is clicked.</summary>
    [Parameter] public EventCallback<object> OnItemClick { get; set; }

    // ── Parameters: Expansion ───────────────────────────────────────────

    /// <summary>The currently expanded item IDs. Supports two-way binding.</summary>
    [Parameter] public IEnumerable<string>? ExpandedItems { get; set; }

    /// <summary>Fires when expanded items change.</summary>
    [Parameter] public EventCallback<IEnumerable<string>> ExpandedItemsChanged { get; set; }

    /// <summary>Expands a node when clicking anywhere on the item header.</summary>
    [Parameter] public bool ExpandOnClick { get; set; }

    /// <summary>Expands a node when double-clicking the item header.</summary>
    [Parameter] public bool ExpandOnDoubleClick { get; set; }

    /// <summary>Collapses sibling items when a node is expanded (accordion behavior).</summary>
    [Parameter] public bool SingleExpand { get; set; }

    /// <summary>Automatically expands ancestors of selected items.</summary>
    [Parameter] public bool AutoExpand { get; set; }

    // ── Parameters: Lazy Loading ────────────────────────────────────────

    /// <summary>Async callback invoked on first expand of a node with HasChildren=true and no loaded children.</summary>
    [Parameter] public Func<object, Task<IEnumerable<object>>>? LoadChildrenAsync { get; set; }

    // ── Parameters: Drag and Drop ───────────────────────────────────────

    /// <summary>Enables drag-and-drop reordering of tree items.</summary>
    [Parameter] public bool EnableDragDrop { get; set; }

    /// <summary>Fires when an item is dropped onto another.</summary>
    [Parameter] public EventCallback<(string DraggedId, string TargetId)> OnItemDrop { get; set; }

    // ── Parameters: Appearance ──────────────────────────────────────────

    /// <summary>The size of tree items (affects padding/font). E.g. "sm", "md", "lg".</summary>
    [Parameter] public string? Size { get; set; }

    /// <summary>Accessibility label for the tree.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    // ── Parameters: State ───────────────────────────────────────────────

    /// <summary>Prevents all user interaction with the tree.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Shows current state but prevents changes.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    // ── Parameters: Context Menu ──────────────────────────────────────

    /// <summary>Fires when a tree item is right-clicked. Provides the item and mouse event args.</summary>
    [Parameter] public EventCallback<TreeItemContextMenuEventArgs> OnItemContextMenu { get; set; }

    // ── Parameters: Checkbox Template ───────────────────────────────────

    /// <summary>Custom render fragment for checkbox display. Receives CheckboxContext with Checked, Indeterminate, OnChange, Disabled.</summary>
    [Parameter] public RenderFragment<CheckboxContext>? CheckboxTemplate { get; set; }

    // ── Parameters: Inline Editing ──────────────────────────────────────

    /// <summary>Enables inline editing of node text via double-click or F2.</summary>
    [Parameter] public bool AllowEditing { get; set; }

    /// <summary>Fires when a node's text is edited. Provides the node ID and new text value.</summary>
    [Parameter] public EventCallback<TreeItemEditEventArgs> OnItemEdit { get; set; }

    // ── Parameters: Filtering ───────────────────────────────────────────

    /// <summary>Predicate to filter visible nodes. Ancestors of matching nodes remain visible.</summary>
    [Parameter] public Func<object, bool>? FilterFunc { get; set; }

    // ── Computed ────────────────────────────────────────────────────────

    internal bool ShowCheckboxes => CheckBoxMode != CheckBoxMode.None;
    internal bool IsSingleCheckMode => CheckBoxMode == CheckBoxMode.Single;
    private string? SizeStyle => Size != null ? $"font-size:{Size};" : null;

    // ── Lifecycle ───────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (ExpandedItems != null)
            _expandedIds = new HashSet<string>(ExpandedItems);
        if (SelectedItems != null)
            _selectedIds = new HashSet<string>(SelectedItems);
        if (CheckedItems != null)
            _checkedIds = new HashSet<string>(CheckedItems);

        _cachedTree = null;

        if (AutoExpand && _selectedIds.Count > 0 && Data != null)
            ExpandAncestorsOfSelected();
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Forces re-render with current data.</summary>
    public void Rebind()
    {
        _cachedTree = null;
        StateHasChanged();
    }

    /// <summary>
    /// Expands all nodes in the tree.
    /// When <paramref name="includeUnloaded"/> is true, triggers LoadChildrenAsync for
    /// lazy-loaded nodes that haven't been fetched yet, up to <paramref name="maxDepth"/> levels deep.
    /// </summary>
    public async Task ExpandAllAsync(
        bool includeUnloaded = false,
        int maxDepth = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        if (includeUnloaded && LoadChildrenAsync != null)
        {
            var tree = GetTree();
            await LoadUnloadedNodesAsync(tree, 0, maxDepth, cancellationToken);
        }

        var fullTree = GetTree();
        CollectAllIds(fullTree, _expandedIds);
        if (ExpandedItemsChanged.HasDelegate)
            await ExpandedItemsChanged.InvokeAsync(_expandedIds.ToList());
        StateHasChanged();
    }

    private async Task LoadUnloadedNodesAsync(
        List<TreeNode> nodes, int currentDepth, int maxDepth,
        CancellationToken cancellationToken)
    {
        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node.HasChildren && !node.Children.Any()
                && !_loadedNodeIds.Contains(node.Id)
                && LoadChildrenAsync != null
                && currentDepth < maxDepth)
            {
                _loadingIds.Add(node.Id);
                StateHasChanged();
                try
                {
                    var children = await LoadChildrenAsync(node.Item);
                    if (children != null)
                    {
                        _lazyLoadedChildren[node.Id] = children.ToList();
                        _cachedTree = null;
                    }
                }
                finally
                {
                    _loadingIds.Remove(node.Id);
                    _loadedNodeIds.Add(node.Id);
                }

                // Rebuild tree to pick up new children, then recurse into them
                var updatedTree = GetTree();
                var updatedNode = FindNode(updatedTree, node.Id);
                if (updatedNode != null && updatedNode.Children.Any())
                {
                    await LoadUnloadedNodesAsync(
                        updatedNode.Children, currentDepth + 1, maxDepth, cancellationToken);
                }
            }
            else if (node.Children.Any())
            {
                await LoadUnloadedNodesAsync(
                    node.Children, currentDepth + 1, maxDepth, cancellationToken);
            }
        }
    }

    /// <summary>Collapses all nodes in the tree.</summary>
    public async Task CollapseAllAsync()
    {
        _expandedIds.Clear();
        if (ExpandedItemsChanged.HasDelegate)
            await ExpandedItemsChanged.InvokeAsync(_expandedIds.ToList());
        StateHasChanged();
    }

    /// <summary>Clears the current filter and shows all nodes.</summary>
    public void ClearFilter()
    {
        _cachedTree = null;
        StateHasChanged();
    }

    /// <summary>Programmatically navigates to a node: expands all ancestors, selects it, and sets focus.</summary>
    public async Task SelectNodeAsync(string id)
    {
        var tree = GetTree();
        var node = FindNode(tree, id);
        if (node == null) return;

        // Expand ancestors
        var ancestors = new List<string>();
        CollectAncestorIds(tree, id, ancestors);
        foreach (var ancestorId in ancestors)
            _expandedIds.Add(ancestorId);

        if (ExpandedItemsChanged.HasDelegate)
            await ExpandedItemsChanged.InvokeAsync(_expandedIds.ToList());

        // Select the node
        _selectedIds.Clear();
        _selectedIds.Add(id);
        _focusedNodeId = id;
        await SelectedItemsChanged.InvokeAsync(_selectedIds.ToList());

        _cachedTree = null;
        StateHasChanged();
    }

    // ── Internal API (used by SunfishTreeItem) ───────────────────────────

    internal bool IsItemChecked(string? id) => id != null && _checkedIds.Contains(id);
    internal bool IsItemSelected(string? id) => id != null && _selectedIds.Contains(id);
    internal bool IsItemFocused(string? id) => id != null && _focusedNodeId == id;

    /// <summary>Returns tri-state: true (all checked), false (none), null (indeterminate).</summary>
    internal bool? GetCheckState(string id)
    {
        var tree = GetTree();
        var node = FindNode(tree, id);
        if (node == null || !node.Children.Any())
            return _checkedIds.Contains(id);

        var allChildIds = new List<string>();
        CollectAllIds(node.Children, allChildIds);
        var checkedCount = allChildIds.Count(cid => _checkedIds.Contains(cid));

        if (checkedCount == 0 && !_checkedIds.Contains(id))
            return false;
        if (checkedCount == allChildIds.Count)
            return true;
        return null; // indeterminate
    }

    internal void ToggleItemChecked(string? id)
    {
        if (id == null || Disabled || ReadOnly) return;

        if (IsSingleCheckMode)
        {
            _checkedIds.Clear();
            _checkedIds.Add(id);
        }
        else
        {
            var wasChecked = _checkedIds.Contains(id);
            if (wasChecked)
                _checkedIds.Remove(id);
            else
                _checkedIds.Add(id);

            // Cascade to children
            if (AllowCheckChildren)
            {
                var tree = GetTree();
                var node = FindNode(tree, id);
                if (node != null)
                {
                    var childIds = new List<string>();
                    CollectAllIds(node.Children, childIds);
                    foreach (var childId in childIds)
                    {
                        if (wasChecked)
                            _checkedIds.Remove(childId);
                        else
                            _checkedIds.Add(childId);
                    }
                }
            }

            // Update ancestors
            if (AllowCheckParents)
            {
                var tree = GetTree();
                UpdateAncestorCheckState(tree, id);
            }
        }

        _ = NotifyCheckedChanged();
        StateHasChanged();
    }

    internal async Task SelectItem(string id, object item)
    {
        if (Disabled || ReadOnly) return;
        if (SelectionMode == TreeSelectionMode.None) return;

        if (SelectionMode == TreeSelectionMode.Single)
        {
            _selectedIds.Clear();
            _selectedIds.Add(id);
        }
        else // Multiple
        {
            if (!_selectedIds.Remove(id))
                _selectedIds.Add(id);
        }

        _focusedNodeId = id;
        await SelectedItemsChanged.InvokeAsync(_selectedIds.ToList());
        if (OnItemClick.HasDelegate)
            await OnItemClick.InvokeAsync(item);
    }

    // ── Tree building ───────────────────────────────────────────────────

    internal record TreeNode(
        string Id,
        object Item,
        string Text,
        string? Icon,
        List<TreeNode> Children,
        bool HasChildren);

    private List<TreeNode> GetTree()
    {
        _cachedTree ??= BuildTree();
        return _cachedTree;
    }

    internal List<TreeNode> BuildTree()
    {
        if (Data == null) return new List<TreeNode>();

        List<TreeNode> tree;

        if (!string.IsNullOrEmpty(ItemsField))
            tree = BuildHierarchical(Data, 0);
        else if (!string.IsNullOrEmpty(IdField) && !string.IsNullOrEmpty(ParentIdField))
            tree = BuildFlat(Data);
        else
        {
            int idx = 0;
            tree = Data.Select(item => new TreeNode(
                $"auto-{idx++}",
                item,
                GetPropertyString(item, TextField) ?? item.ToString() ?? "",
                GetPropertyString(item, IconField),
                new List<TreeNode>(),
                false
            )).ToList();
        }

        if (FilterFunc != null)
            tree = ApplyFilter(tree);

        return tree;
    }

    private List<TreeNode> BuildHierarchical(IEnumerable<object> items, int depth)
    {
        var result = new List<TreeNode>();
        int idx = 0;
        foreach (var item in items)
        {
            var id = GetPropertyString(item, IdField) ?? $"h-{depth}-{idx++}";
            var text = GetPropertyString(item, TextField) ?? item.ToString() ?? "";
            var icon = GetPropertyString(item, IconField);
            var children = new List<TreeNode>();
            var childItems = GetPropertyValue(item, ItemsField);

            if (childItems is System.Collections.IEnumerable enumerable)
            {
                var childList = enumerable.Cast<object>().ToList();
                if (childList.Any())
                    children = BuildHierarchical(childList, depth + 1);
            }

            // Include lazy-loaded children for this node
            if (!children.Any() && _lazyLoadedChildren.TryGetValue(id, out var lazyChildren) && lazyChildren.Any())
                children = BuildHierarchical(lazyChildren, depth + 1);

            var hasChildren = children.Any();
            if (!string.IsNullOrEmpty(HasChildrenField))
            {
                var hcVal = GetPropertyValue(item, HasChildrenField);
                if (hcVal is bool b) hasChildren = b;
            }

            result.Add(new TreeNode(id, item, text, icon, children, hasChildren));
        }
        return result;
    }

    private List<TreeNode> BuildFlat(IEnumerable<object> items)
    {
        // Include lazy-loaded children alongside the original data
        var allItems = items.ToList();
        foreach (var kvp in _lazyLoadedChildren)
        {
            foreach (var child in kvp.Value)
            {
                var childId = GetPropertyString(child, IdField) ?? "";
                if (!string.IsNullOrEmpty(childId) && !allItems.Any(i => GetPropertyString(i, IdField) == childId))
                    allItems.Add(child);
            }
        }
        var lookup = new Dictionary<string, TreeNode>();
        var roots = new List<TreeNode>();

        foreach (var item in allItems)
        {
            var id = GetPropertyString(item, IdField) ?? "";
            var text = GetPropertyString(item, TextField) ?? item.ToString() ?? "";
            var icon = GetPropertyString(item, IconField);
            var hasChildren = false;
            if (!string.IsNullOrEmpty(HasChildrenField))
            {
                var hcVal = GetPropertyValue(item, HasChildrenField);
                if (hcVal is bool b) hasChildren = b;
            }
            lookup[id] = new TreeNode(id, item, text, icon, new List<TreeNode>(), hasChildren);
        }

        foreach (var item in allItems)
        {
            var id = GetPropertyString(item, IdField) ?? "";
            var parentId = GetPropertyString(item, ParentIdField);
            var node = lookup[id];

            if (string.IsNullOrEmpty(parentId) || !lookup.ContainsKey(parentId))
                roots.Add(node);
            else
                lookup[parentId].Children.Add(node);
        }

        return roots;
    }

    // ── Filtering ───────────────────────────────────────────────────────

    private List<TreeNode> ApplyFilter(List<TreeNode> nodes)
    {
        var result = new List<TreeNode>();
        foreach (var node in nodes)
        {
            var filteredChildren = ApplyFilter(node.Children);
            var matches = FilterFunc!(node.Item);
            if (matches || filteredChildren.Count > 0)
            {
                result.Add(node with { Children = filteredChildren });
            }
        }
        return result;
    }

    // ── Rendering ───────────────────────────────────────────────────────

    internal RenderFragment RenderNodes(List<TreeNode> nodes) => builder =>
    {
        foreach (var node in nodes)
        {
            var isExpanded = _expandedIds.Contains(node.Id);
            var isSelected = _selectedIds.Contains(node.Id);
            var isFocused = _focusedNodeId == node.Id;
            var hasKids = node.Children.Any() || node.HasChildren;
            var checkState = ShowCheckboxes ? GetCheckState(node.Id) : (bool?)false;

            builder.OpenElement(0, "li");

            var liClass = CssProvider.TreeItemClass(isExpanded, isSelected)
                + (isFocused ? " mar-tree-item--focused" : "")
                + (FilterFunc != null && FilterFunc(node.Item) ? " mar-tree-item--filter-match" : "");
            builder.AddAttribute(1, "class", liClass);
            builder.AddAttribute(2, "role", "treeitem");
            builder.AddAttribute(3, "id", $"tree-node-{node.Id}");
            if (hasKids)
                builder.AddAttribute(4, "aria-expanded", isExpanded.ToString().ToLower());
            builder.AddAttribute(5, "aria-selected", isSelected.ToString().ToLower());

            // Header
            builder.OpenElement(10, "div");
            var headerClass = "mar-tree-item__header" +
                (EnableDragDrop && _dragOverNodeId == node.Id ? " mar-tree-item__header--dragover" : "");
            builder.AddAttribute(11, "class", headerClass);

            if (EnableDragDrop && !Disabled && !ReadOnly)
            {
                var dragNodeId = node.Id;
                builder.AddAttribute(12, "draggable", "true");
                builder.AddAttribute(13, "ondragstart", EventCallback.Factory.Create<DragEventArgs>(this, () => _draggedNodeId = dragNodeId));
                builder.AddAttribute(14, "ondragover", EventCallback.Factory.Create<DragEventArgs>(this, () => _dragOverNodeId = dragNodeId));
                builder.AddAttribute(15, "ondragleave", EventCallback.Factory.Create<DragEventArgs>(this, () => { if (_dragOverNodeId == dragNodeId) _dragOverNodeId = null; }));
                builder.AddAttribute(16, "ondrop", EventCallback.Factory.Create<DragEventArgs>(this, () => HandleDrop(dragNodeId)));
                builder.AddEventPreventDefaultAttribute(17, "ondragover", true);
            }

            // Context menu
            if (OnItemContextMenu.HasDelegate)
            {
                var ctxNode = node;
                builder.AddAttribute(18, "oncontextmenu", EventCallback.Factory.Create<MouseEventArgs>(this, (MouseEventArgs args) =>
                    OnItemContextMenu.InvokeAsync(new TreeItemContextMenuEventArgs { Item = ctxNode.Item, ItemId = ctxNode.Id, MouseEventArgs = args.ToSunfish() })));
                builder.AddEventPreventDefaultAttribute(19, "oncontextmenu", true);
            }

            // ExpandOnClick / ExpandOnDoubleClick on header
            if (hasKids && !Disabled && !ReadOnly)
            {
                var clickNodeId = node.Id;
                if (ExpandOnClick)
                    builder.AddAttribute(20, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => ToggleNodeAsync(clickNodeId)));
                if (ExpandOnDoubleClick && !AllowEditing)
                    builder.AddAttribute(21, "ondblclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => ToggleNodeAsync(clickNodeId)));
            }

            // Toggle button
            if (hasKids)
            {
                var nodeId = node.Id;
                builder.OpenElement(20, "button");
                builder.AddAttribute(21, "type", "button");
                builder.AddAttribute(22, "class", "mar-tree-item__toggle");
                builder.AddAttribute(23, "aria-label", isExpanded ? "Collapse" : "Expand");
                builder.AddAttribute(24, "tabindex", "-1");
                builder.AddAttribute(25, "disabled", Disabled || ReadOnly);
                builder.AddAttribute(26, "onclick", EventCallback.Factory.Create(this, () => ToggleNodeAsync(nodeId)));
                builder.AddContent(27, isExpanded ? "\u25BC" : "\u25B6");
                builder.CloseElement();
            }

            // Checkbox
            if (ShowCheckboxes)
            {
                var cbId = node.Id;
                if (CheckboxTemplate != null)
                {
                    var ctx = new CheckboxContext
                    {
                        Checked = checkState == true,
                        Indeterminate = checkState == null,
                        Disabled = Disabled || ReadOnly,
                        OnChange = (val) => { if (val != (checkState == true)) ToggleItemChecked(cbId); }
                    };
                    builder.AddContent(30, CheckboxTemplate(ctx));
                }
                else
                {
                    builder.OpenElement(30, "input");
                    builder.AddAttribute(31, "type", "checkbox");
                    builder.AddAttribute(32, "class",
                        "mar-tree-item__checkbox" +
                        (checkState == null ? " mar-tree-item__checkbox--indeterminate" : ""));
                    builder.AddAttribute(33, "checked", checkState == true);
                    builder.AddAttribute(34, "disabled", Disabled || ReadOnly);
                    builder.AddAttribute(35, "tabindex", "-1");
                    builder.AddAttribute(36, "aria-checked", checkState switch
                    {
                        true => "true",
                        false => "false",
                        null => "mixed"
                    });
                    builder.AddAttribute(37, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, _ => ToggleItemChecked(cbId)));
                    builder.CloseElement();
                }
            }

            // Icon
            if (!string.IsNullOrEmpty(node.Icon))
            {
                builder.OpenElement(40, "span");
                builder.AddAttribute(41, "class", "mar-tree-item__icon");
                builder.AddContent(42, IconProvider.GetIcon(node.Icon, Sunfish.Foundation.Enums.IconSize.Small));
                builder.CloseElement();
            }

            // Content (template, inline edit, or text label)
            if (_editingNodeId == node.Id && AllowEditing)
            {
                var editId = node.Id;
                builder.OpenElement(50, "input");
                builder.AddAttribute(51, "type", "text");
                builder.AddAttribute(52, "class", "mar-tree-item__edit-input");
                builder.AddAttribute(53, "value", _editingText);
                builder.AddAttribute(54, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e => _editingText = e.Value?.ToString() ?? ""));
                builder.AddAttribute(55, "onblur", EventCallback.Factory.Create<FocusEventArgs>(this, () => CommitEdit(editId)));
                builder.AddAttribute(56, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
                {
                    if (e.Key == "Enter") CommitEdit(editId);
                    else if (e.Key == "Escape") CancelEdit();
                }));
                builder.AddAttribute(57, "autofocus", true);
                builder.CloseElement();
            }
            else if (ItemTemplate != null)
            {
                builder.AddContent(50, ItemTemplate(node.Item));
            }
            else
            {
                var clickNode = node;
                builder.OpenElement(50, "span");
                builder.AddAttribute(51, "class", "mar-tree-item__title");
                if (!Disabled && !ReadOnly)
                    builder.AddAttribute(52, "onclick", EventCallback.Factory.Create(this, () => SelectItem(clickNode.Id, clickNode.Item)));
                if (AllowEditing && !Disabled && !ReadOnly)
                    builder.AddAttribute(53, "ondblclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => StartEdit(clickNode.Id, clickNode.Text)));
                builder.AddContent(54, node.Text);
                builder.CloseElement();
            }

            builder.CloseElement(); // div header

            // Loading indicator
            if (hasKids && isExpanded && _loadingIds.Contains(node.Id))
            {
                builder.OpenElement(60, "div");
                builder.AddAttribute(61, "class", "mar-tree-item__loading");
                builder.AddAttribute(62, "role", "status");
                builder.AddContent(63, "Loading\u2026");
                builder.CloseElement();
            }

            // Children
            if (hasKids && isExpanded && node.Children.Any())
            {
                builder.OpenElement(70, "ul");
                builder.AddAttribute(71, "role", "group");
                builder.AddAttribute(72, "class", "mar-tree-item__children");
                builder.AddContent(73, RenderNodes(node.Children));
                builder.CloseElement();
            }

            builder.CloseElement(); // li
        }
    };

    // ── Expansion ───────────────────────────────────────────────────────

    private async Task ToggleNodeAsync(string id)
    {
        if (Disabled || ReadOnly) return;

        var wasExpanded = _expandedIds.Contains(id);

        if (wasExpanded)
        {
            _expandedIds.Remove(id);
        }
        else
        {
            if (SingleExpand)
            {
                // Collapse siblings: find parent, remove sibling IDs
                var tree = GetTree();
                var siblingIds = FindSiblingIds(tree, id);
                foreach (var sibId in siblingIds)
                    _expandedIds.Remove(sibId);
            }

            _expandedIds.Add(id);

            // Lazy load on first expand
            if (LoadChildrenAsync != null && !_loadedNodeIds.Contains(id))
            {
                var tree = GetTree();
                var node = FindNode(tree, id);
                if (node != null && node.HasChildren && !node.Children.Any())
                {
                    _loadingIds.Add(id);
                    StateHasChanged();

                    try
                    {
                        var children = await LoadChildrenAsync(node.Item);
                        if (children != null)
                        {
                            _lazyLoadedChildren[id] = children.ToList();
                            _cachedTree = null;
                        }
                    }
                    finally
                    {
                        _loadingIds.Remove(id);
                        _loadedNodeIds.Add(id);
                    }
                }
            }
        }

        if (ExpandedItemsChanged.HasDelegate)
            await ExpandedItemsChanged.InvokeAsync(_expandedIds.ToList());

        StateHasChanged();
    }

    // ── Drag and Drop ───────────────────────────────────────────────────

    private async Task HandleDrop(string targetId)
    {
        if (ReadOnly) return;
        if (_draggedNodeId != null && _draggedNodeId != targetId && OnItemDrop.HasDelegate)
            await OnItemDrop.InvokeAsync((_draggedNodeId, targetId));
        _draggedNodeId = null;
        _dragOverNodeId = null;
        StateHasChanged();
    }

    // ── Keyboard Navigation (WAI-ARIA TreeView pattern) ─────────────────

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (Disabled) return;

        var visibleIds = GetVisibleNodeIds();
        if (visibleIds.Count == 0) return;

        _focusedNodeId ??= visibleIds[0];
        var currentIndex = visibleIds.IndexOf(_focusedNodeId);
        if (currentIndex < 0) currentIndex = 0;

        _preventKeyDefault = true;

        switch (e.Key)
        {
            case "ArrowDown":
                if (currentIndex < visibleIds.Count - 1)
                    _focusedNodeId = visibleIds[currentIndex + 1];
                break;

            case "ArrowUp":
                if (currentIndex > 0)
                    _focusedNodeId = visibleIds[currentIndex - 1];
                break;

            case "ArrowRight":
                if (_expandedIds.Contains(_focusedNodeId))
                {
                    // Move to first child
                    if (currentIndex < visibleIds.Count - 1)
                        _focusedNodeId = visibleIds[currentIndex + 1];
                }
                else
                {
                    // Expand current node
                    var tree = GetTree();
                    var node = FindNode(tree, _focusedNodeId);
                    if (node != null && (node.Children.Any() || node.HasChildren))
                        await ToggleNodeAsync(_focusedNodeId);
                }
                break;

            case "ArrowLeft":
                if (_expandedIds.Contains(_focusedNodeId))
                {
                    // Collapse current node
                    await ToggleNodeAsync(_focusedNodeId);
                }
                else
                {
                    // Move to parent
                    var tree = GetTree();
                    var parentId = FindParentId(tree, _focusedNodeId);
                    if (parentId != null)
                        _focusedNodeId = parentId;
                }
                break;

            case "Enter":
            case " ":
                var enterTree = GetTree();
                var focusedNode = FindNode(enterTree, _focusedNodeId);
                if (focusedNode != null)
                {
                    if (ShowCheckboxes)
                        ToggleItemChecked(_focusedNodeId);
                    if (SelectionMode != TreeSelectionMode.None)
                        await SelectItem(_focusedNodeId, focusedNode.Item);
                }
                break;

            case "Home":
                _focusedNodeId = visibleIds[0];
                break;

            case "End":
                _focusedNodeId = visibleIds[^1];
                break;

            case "F2":
                // Start inline editing of focused node
                if (AllowEditing && !ReadOnly && _focusedNodeId != null)
                {
                    var editTree = GetTree();
                    var editNode = FindNode(editTree, _focusedNodeId);
                    if (editNode != null)
                        StartEdit(editNode.Id, editNode.Text);
                }
                break;

            case "Escape":
                if (_editingNodeId != null)
                    CancelEdit();
                break;

            case "*":
                // Expand all siblings of focused node
                var sibTree = GetTree();
                var siblings = FindSiblingIds(sibTree, _focusedNodeId);
                foreach (var sibId in siblings)
                    _expandedIds.Add(sibId);
                _expandedIds.Add(_focusedNodeId);
                if (ExpandedItemsChanged.HasDelegate)
                    await ExpandedItemsChanged.InvokeAsync(_expandedIds.ToList());
                _cachedTree = null;
                break;

            default:
                _preventKeyDefault = false;
                break;
        }

        StateHasChanged();
    }

    // ── Helper: visible node IDs (flattened, respects expansion) ─────────

    private List<string> GetVisibleNodeIds()
    {
        var tree = GetTree();
        var result = new List<string>();
        CollectVisibleIds(tree, result);
        return result;
    }

    private void CollectVisibleIds(List<TreeNode> nodes, List<string> result)
    {
        foreach (var node in nodes)
        {
            result.Add(node.Id);
            if (_expandedIds.Contains(node.Id) && node.Children.Any())
                CollectVisibleIds(node.Children, result);
        }
    }

    // ── Helper: tree traversal ──────────────────────────────────────────

    private static TreeNode? FindNode(List<TreeNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id) return node;
            var found = FindNode(node.Children, id);
            if (found != null) return found;
        }
        return null;
    }

    private static string? FindParentId(List<TreeNode> nodes, string childId, string? currentParentId = null)
    {
        foreach (var node in nodes)
        {
            if (node.Id == childId) return currentParentId;
            var found = FindParentId(node.Children, childId, node.Id);
            if (found != null) return found;
        }
        return null;
    }

    private static List<string> FindSiblingIds(List<TreeNode> tree, string nodeId)
    {
        var siblings = FindSiblingList(tree, nodeId);
        return siblings?.Where(n => n.Id != nodeId).Select(n => n.Id).ToList() ?? new List<string>();
    }

    private static List<TreeNode>? FindSiblingList(List<TreeNode> nodes, string nodeId)
    {
        foreach (var node in nodes)
        {
            if (node.Id == nodeId) return nodes;
            var found = FindSiblingList(node.Children, nodeId);
            if (found != null) return found;
        }
        return null;
    }

    private static void CollectAllIds(List<TreeNode> nodes, ICollection<string> ids)
    {
        foreach (var node in nodes)
        {
            ids.Add(node.Id);
            CollectAllIds(node.Children, ids);
        }
    }

    private static void CollectAllIds(List<TreeNode> nodes, HashSet<string> ids)
    {
        foreach (var node in nodes)
        {
            ids.Add(node.Id);
            CollectAllIds(node.Children, ids);
        }
    }

    // ── Helper: tri-state ancestor update ───────────────────────────────

    private void UpdateAncestorCheckState(List<TreeNode> tree, string childId)
    {
        var parentId = FindParentId(tree, childId);
        while (parentId != null)
        {
            var parentNode = FindNode(tree, parentId);
            if (parentNode == null) break;

            var allChildIds = new List<string>();
            CollectAllIds(parentNode.Children, allChildIds);
            var allChecked = allChildIds.All(cid => _checkedIds.Contains(cid));
            var noneChecked = allChildIds.All(cid => !_checkedIds.Contains(cid));

            if (allChecked)
                _checkedIds.Add(parentId);
            else
                _checkedIds.Remove(parentId);
            // Indeterminate state is computed on-the-fly by GetCheckState()

            parentId = FindParentId(tree, parentId);
        }
    }

    // ── Helper: auto-expand ancestors of selected items ─────────────────

    private void ExpandAncestorsOfSelected()
    {
        var tree = GetTree();
        foreach (var selectedId in _selectedIds)
        {
            var ancestors = new List<string>();
            CollectAncestorIds(tree, selectedId, ancestors);
            foreach (var ancestorId in ancestors)
                _expandedIds.Add(ancestorId);
        }
    }

    private static bool CollectAncestorIds(List<TreeNode> nodes, string targetId, List<string> ancestors)
    {
        foreach (var node in nodes)
        {
            if (node.Id == targetId) return true;
            if (CollectAncestorIds(node.Children, targetId, ancestors))
            {
                ancestors.Add(node.Id);
                return true;
            }
        }
        return false;
    }

    // ── Helper: property reflection ─────────────────────────────────────

    private static string? GetPropertyString(object item, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return null;
        return item.GetType().GetProperty(propertyName)?.GetValue(item)?.ToString();
    }

    private static object? GetPropertyValue(object item, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return null;
        return item.GetType().GetProperty(propertyName)?.GetValue(item);
    }

    // ── Inline Editing ────────────────────────────────────────────────

    private void StartEdit(string nodeId, string currentText)
    {
        _editingNodeId = nodeId;
        _editingText = currentText;
        StateHasChanged();
    }

    private async void CommitEdit(string nodeId)
    {
        if (_editingNodeId != nodeId) return;
        var newText = _editingText.Trim();
        _editingNodeId = null;

        if (!string.IsNullOrEmpty(newText) && OnItemEdit.HasDelegate)
            await OnItemEdit.InvokeAsync(new TreeItemEditEventArgs { ItemId = nodeId, NewText = newText });

        StateHasChanged();
    }

    private void CancelEdit()
    {
        _editingNodeId = null;
        _editingText = "";
        StateHasChanged();
    }

    internal bool IsEditing(string id) => _editingNodeId == id;

    // ── Helper: notify checked changed ──────────────────────────────────

    private async Task NotifyCheckedChanged()
    {
        if (CheckedItemsChanged.HasDelegate)
            await CheckedItemsChanged.InvokeAsync(_checkedIds.ToList());
    }
}
