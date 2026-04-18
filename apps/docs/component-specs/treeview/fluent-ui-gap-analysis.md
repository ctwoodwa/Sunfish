---
title: Fluent UI Tree Gap Analysis & API Extension
page_title: TreeView - Fluent UI Gap Analysis
description: Analysis of Fluent UI React Tree features and proposed API extensions for the Sunfish Blazor TreeView.
slug: treeview-fluent-ui-gap-analysis
tags: sunfish,blazor,treeview,fluent-ui,gap-analysis
published: True
position: 50
components: ["treeview"]
---

# Fluent UI React Tree - Gap Analysis & API Extension

This document compares the [Fluent UI React v9 Tree component](https://storybooks.fluentui.dev/react/?path=/docs/components-tree--docs) with the Sunfish Blazor TreeView, identifies feature gaps, and proposes API extensions.

## Feature Comparison Matrix

| Feature | Fluent UI | Sunfish | Gap? |
|---|---|---|---|
| Flat data binding | FlatTree + useHeadlessFlatTree | `Data` + `ParentIdField` | Equivalent |
| Hierarchical data binding | Nested `<Tree>` / `<TreeItem>` | `Data` + `ItemsField` | Equivalent |
| Single selection | `selectionMode="single"` | `SelectionMode="Single"` | Equivalent |
| Multi-selection (checkboxes) | `selectionMode="multiselect"` | `CheckBoxMode="Multiple"` | Equivalent |
| Expand / collapse | `openItems` / `onOpenChange` | `ExpandedItems` / `ExpandedItemsChanged` | Equivalent |
| Load on demand | Lazy loading via headless hook | `LoadChildrenAsync` / `OnExpand` | Equivalent |
| Icons | `iconBefore`, `iconAfter` slots | Single `IconField` | **Gap** |
| Templates | Slot-based (`content`, `aside`, `actions`) | `ItemTemplate` per level | **Gap** |
| Aside content (badges, counts) | `aside` slot on layouts | Not supported | **Gap** |
| Actions (hover buttons) | `actions` slot on TreeItemLayout | Not supported | **Gap** |
| Persona layout (avatar/media) | `TreeItemPersonaLayout` | Not supported | **Gap** |
| Appearance variants | `"subtle"`, `"transparent"`, `"subtle-alpha"` | None (only `Size`) | **Gap** |
| Collapse animation/motion | `collapseMotion` slot | Not supported | **Gap** |
| Navigation mode | `"tree"` vs `"treegrid"` | Single mode | **Gap** |
| Virtualization | Via FlatTree + react-window | Not supported | **Gap** |
| Infinite scrolling | Built-in example pattern | Not supported | **Gap** |
| Tree manipulation (add/remove) | Headless hook + focus management | `Rebind()` only | **Gap** |
| Drag and drop | Not built-in (delegates to @dnd-kit) | Built-in (`EnableDragDrop`) | **Sunfish ahead** |
| Inline editing | Not built-in | `AllowEditing` / `OnItemEdit` | **Sunfish ahead** |
| Context menu | Not built-in | `OnItemContextMenu` | **Sunfish ahead** |
| Checkbox cascade (tri-state) | Not built-in | `AllowCheckChildren` / `AllowCheckParents` | **Sunfish ahead** |
| Filtering | Not built-in | `FilterFunc` | **Sunfish ahead** |
| CheckboxTemplate | Not built-in | `CheckboxTemplate` | **Sunfish ahead** |
| Subtree (independent sub-trees) | `<Tree>` as subtree ignores root props | Not supported | **Gap** |

---

## Proposed API Extensions

### 1. Appearance Parameter

Fluent UI offers visual variants that control the tree's chrome. This is a simple but valuable design-system feature.

**New parameter:**

```csharp
/// <summary>
/// Controls the visual appearance of the tree items.
/// </summary>
[Parameter] public TreeViewAppearance Appearance { get; set; } = TreeViewAppearance.Subtle;
```

**New enum:**

```csharp
public enum TreeViewAppearance
{
    /// <summary>Default appearance with subtle hover/active backgrounds.</summary>
    Subtle,
    /// <summary>No background — items blend into the page.</summary>
    Transparent,
    /// <summary>Subtle with alpha-blended backgrounds for layered surfaces.</summary>
    SubtleAlpha
}
```

**Usage:**

```razor
<SunfishTreeView Data="@Data" Appearance="TreeViewAppearance.Transparent" />
```

**Impact:** Low complexity. Renders as a CSS class on the root element (`sunfish-treeview--subtle`, etc.) with corresponding SCSS styles.

---

### 2. IconAfter Support

Fluent UI supports icons on both sides of the item text. Sunfish currently has a single `IconField`.

**New binding property:**

```csharp
// On TreeViewBinding:
[Parameter] public string IconAfterField { get; set; }
```

**Data model support:**

```csharp
public class TreeItem
{
    public ISvgIcon Icon { get; set; }       // existing — renders before text
    public ISvgIcon IconAfter { get; set; }  // new — renders after text
}
```

**Rendered HTML:**

```html
<span class="sunfish-treeview-item__content">
    <span class="sunfish-treeview-item__icon-before"><!-- Icon --></span>
    <span class="sunfish-treeview-item__text">Node text</span>
    <span class="sunfish-treeview-item__icon-after"><!-- IconAfter --></span>
</span>
```

**Impact:** Low complexity. Extends existing icon rendering pipeline.

---

### 3. Aside Content Template

Fluent UI's `aside` slot displays supplementary content (badges, counters, status indicators) on the right edge of each item. This is distinct from `IconAfter` — it's right-aligned secondary content.

**New parameter:**

```csharp
// On SunfishTreeView:
[Parameter] public RenderFragment<object> AsideTemplate { get; set; }
```

**Per-level support via TreeViewBinding:**

```csharp
// On TreeViewBinding:
[Parameter] public RenderFragment<object> AsideTemplate { get; set; }
```

**Usage:**

```razor
<SunfishTreeView Data="@Data">
    <TreeViewBindings>
        <TreeViewBinding>
            <AsideTemplate>
                @{
                    var item = context as MailFolder;
                    @if (item.UnreadCount > 0)
                    {
                        <SunfishBadge>@item.UnreadCount</SunfishBadge>
                    }
                }
            </AsideTemplate>
        </TreeViewBinding>
    </TreeViewBindings>
</SunfishTreeView>
```

**Rendered HTML:**

```html
<li class="sunfish-treeview-item" role="treeitem">
    <div class="sunfish-treeview-item__row">
        <span class="sunfish-treeview-item__content">...</span>
        <span class="sunfish-treeview-item__aside"><!-- AsideTemplate renders here --></span>
    </div>
</li>
```

**Impact:** Medium complexity. Requires layout changes to tree item row to support right-aligned content with flex.

---

### 4. Actions Template (Hover/Focus Actions)

Fluent UI's `actions` slot renders action buttons that appear on hover or focus. These are distinct from `Aside` — they are interactive controls that are hidden by default.

**New parameter:**

```csharp
// On SunfishTreeView:
[Parameter] public RenderFragment<object> ActionsTemplate { get; set; }

// On TreeViewBinding:
[Parameter] public RenderFragment<object> ActionsTemplate { get; set; }
```

**Usage:**

```razor
<SunfishTreeView Data="@Data">
    <TreeViewBindings>
        <TreeViewBinding>
            <ActionsTemplate>
                @{
                    var item = context as FileItem;
                    <SunfishButton Icon="@SvgIcon.Pencil"
                                   OnClick="@(() => Edit(item))"
                                   FillMode="flat" Size="sm" />
                    <SunfishButton Icon="@SvgIcon.Trash"
                                   OnClick="@(() => Delete(item))"
                                   FillMode="flat" Size="sm" />
                }
            </ActionsTemplate>
        </TreeViewBinding>
    </TreeViewBindings>
</SunfishTreeView>
```

**Behavior:**
- Actions are hidden by default (CSS `opacity: 0` / `visibility: hidden`).
- Shown on item hover or when any action inside receives focus.
- When `ActionsTemplate` is present and `Aside` is also present, actions replace the aside on hover.
- Actions are keyboard-accessible via Tab when the item has focus (treegrid navigation mode).

**Impact:** Medium complexity. Requires hover/focus CSS, ARIA adjustments, and interaction with NavigationMode.

---

### 5. NavigationMode Parameter

Fluent UI distinguishes between `"tree"` and `"treegrid"` keyboard navigation. In `tree` mode, Right arrow expands/drills into children. In `treegrid` mode, Right arrow moves focus to inline actions within the row — critical for items with `ActionsTemplate`.

**New parameter:**

```csharp
[Parameter] public TreeViewNavigationMode NavigationMode { get; set; } = TreeViewNavigationMode.Tree;
```

**New enum:**

```csharp
public enum TreeViewNavigationMode
{
    /// <summary>
    /// Default tree navigation. Right arrow expands or drills into children.
    /// </summary>
    Tree,
    /// <summary>
    /// Grid-like navigation. Right arrow moves focus to actions within the tree item row.
    /// The root element renders with role="treegrid".
    /// </summary>
    TreeGrid
}
```

**Keyboard behavior by mode:**

| Key | Tree mode | TreeGrid mode |
|---|---|---|
| Right arrow (on branch) | Expand, then drill into first child | Move focus to first action in row |
| Left arrow (on action) | N/A | Return focus to tree item |
| Tab (on item) | Exits tree | Moves to first action (if actions present) |

**ARIA:** When `NavigationMode="TreeGrid"`, the root renders `role="treegrid"` and each row includes `role="row"` with `role="gridcell"` for content and action cells.

**Impact:** Medium-high complexity. Requires alternate keyboard handler and ARIA role changes. Should be implemented alongside ActionsTemplate.

---

### 6. Collapse Animation (Motion)

Fluent UI supports a `collapseMotion` slot for animating expand/collapse transitions.

**New parameter:**

```csharp
/// <summary>
/// Enables expand/collapse animation on tree item children.
/// </summary>
[Parameter] public bool AnimateExpand { get; set; } = false;

/// <summary>
/// Duration of expand/collapse animation in milliseconds.
/// </summary>
[Parameter] public int AnimationDuration { get; set; } = 200;
```

**Implementation approach:**
- Use CSS `max-height` or `grid-template-rows` transition on the child `<ul role="group">`.
- When collapsing, animate height to 0 then set `display: none` after transition ends.
- When expanding, set `display: block`, measure height, and animate from 0 to measured height.
- Requires JS interop for height measurement.

**Usage:**

```razor
<SunfishTreeView Data="@Data" AnimateExpand="true" AnimationDuration="300" />
```

**Impact:** Medium complexity. Requires JS interop for measuring child group height, and CSS transitions.

---

### 7. Virtualization

Fluent UI's FlatTree supports virtualization via integration with windowing libraries. This is critical for large datasets (1000+ items).

**New parameters:**

```csharp
/// <summary>
/// Enables row virtualization. Only visible items are rendered in the DOM.
/// Requires flat data binding (ParentIdField) or hierarchical data that can be flattened internally.
/// </summary>
[Parameter] public bool EnableVirtualization { get; set; } = false;

/// <summary>
/// The fixed height (in pixels) of each tree item row when virtualization is enabled.
/// </summary>
[Parameter] public float ItemHeight { get; set; } = 36f;

/// <summary>
/// Number of additional items to render above and below the visible area.
/// </summary>
[Parameter] public int OverscanCount { get; set; } = 3;

/// <summary>
/// The height of the TreeView scroll container. Required when virtualization is enabled.
/// Accepts CSS values like "400px", "50vh", "100%".
/// </summary>
[Parameter] public string Height { get; set; }
```

**Implementation approach:**
- Internally flatten the tree to a visible-items list (respecting expanded state).
- Use Blazor's `<Virtualize>` component or a custom virtual scroller.
- Only render items in the viewport + overscan buffer.
- Recalculate the visible list when `ExpandedItems` changes.

**Usage:**

```razor
<SunfishTreeView Data="@LargeDataSet"
                 EnableVirtualization="true"
                 ItemHeight="36"
                 Height="500px" />
```

**Constraints:**
- All items must have the same height (fixed-size virtualization).
- `AnimateExpand` is not compatible with virtualization (animation disabled automatically when virtualized).

**Impact:** High complexity. Requires internal flat-list representation, scroll position tracking, and careful ARIA management for off-screen items.

---

### 8. Infinite Scrolling

Extends load-on-demand to support incrementally loading sibling items as the user scrolls to the end of a branch.

**New parameters:**

```csharp
/// <summary>
/// Callback invoked when the user scrolls near the end of a node's children.
/// Return additional items to append to the existing children.
/// </summary>
[Parameter] public EventCallback<TreeViewScrollEventArgs> OnScrollNearEnd { get; set; }

/// <summary>
/// Number of pixels from the bottom of the scroll container before OnScrollNearEnd fires.
/// </summary>
[Parameter] public int ScrollThreshold { get; set; } = 100;
```

**New event args:**

```csharp
public class TreeViewScrollEventArgs
{
    /// <summary>The parent item whose children are being scrolled.</summary>
    public object ParentItem { get; set; }

    /// <summary>The parent item's ID.</summary>
    public object ParentId { get; set; }

    /// <summary>Current count of loaded children.</summary>
    public int LoadedCount { get; set; }
}
```

**Usage:**

```razor
<SunfishTreeView Data="@Data"
                 EnableVirtualization="true"
                 Height="400px"
                 OnScrollNearEnd="@LoadMore">
</SunfishTreeView>

@code {
    async Task LoadMore(TreeViewScrollEventArgs args)
    {
        var moreItems = await DataService.GetNextPage(args.ParentId, args.LoadedCount);
        Data.AddRange(moreItems);
        TreeViewRef.Rebind();
    }
}
```

**Impact:** Medium complexity. Leverages virtualization infrastructure. Requires scroll position detection via JS interop.

---

### 9. Tree Manipulation API

Fluent UI's headless hook approach makes add/remove straightforward. Sunfish should offer explicit methods with focus management.

**New methods on SunfishTreeView:**

```csharp
/// <summary>
/// Programmatically adds an item to the tree and optionally sets focus to it.
/// </summary>
/// <param name="item">The item to add.</param>
/// <param name="focusNewItem">Whether to move focus to the newly added item.</param>
public async Task AddItemAsync(TItem item, bool focusNewItem = true);

/// <summary>
/// Programmatically removes an item from the tree and manages focus.
/// Focus moves to the previous sibling, or the parent if no siblings remain.
/// </summary>
/// <param name="itemId">The ID of the item to remove.</param>
public async Task RemoveItemAsync(object itemId);

/// <summary>
/// Programmatically moves an item to a new parent.
/// </summary>
/// <param name="itemId">The ID of the item to move.</param>
/// <param name="newParentId">The ID of the new parent (null for root).</param>
/// <param name="index">Position among siblings (null to append at end).</param>
public async Task MoveItemAsync(object itemId, object newParentId, int? index = null);
```

**Focus management rules (per Fluent UI guidance):**
- After `AddItemAsync`: focus moves to the new item.
- After `RemoveItemAsync`: focus moves to previous sibling; if none, to parent; if none, to next sibling.
- After `MoveItemAsync`: focus follows the moved item.

**Usage:**

```razor
<SunfishButton OnClick="@AddChild">Add Item</SunfishButton>
<SunfishTreeView @ref="TreeViewRef" Data="@Data" />

@code {
    async Task AddChild()
    {
        var newItem = new TreeItem { Id = nextId++, Text = "New Item", ParentId = selectedId };
        Data.Add(newItem);
        await TreeViewRef.AddItemAsync(newItem);
    }
}
```

**Impact:** Medium complexity. The add/remove logic is straightforward; focus management requires JS interop.

---

### 10. Subtree Support

Fluent UI allows nesting independent `<Tree>` components as subtrees that ignore root-level props like `openItems` and `selectionMode`. This enables mixed selection behavior within one visual tree.

**New component:**

```csharp
/// <summary>
/// Renders an independent subtree within a parent SunfishTreeView.
/// The subtree manages its own expand, selection, and checkbox state.
/// </summary>
public partial class SunfishTreeViewSubtree<TItem> : ComponentBase
{
    [Parameter] public IEnumerable<TItem> Data { get; set; }
    [Parameter] public SelectionMode? SelectionMode { get; set; }
    [Parameter] public CheckBoxMode? CheckBoxMode { get; set; }
    [Parameter] public IEnumerable<object> ExpandedItems { get; set; }
    [Parameter] public RenderFragment<object> ItemTemplate { get; set; }
}
```

**Usage:**

```razor
<SunfishTreeView Data="@RootData" SelectionMode="Single">
    <TreeViewBindings>
        <TreeViewBinding>
            <ItemTemplate>
                @{
                    var item = context as FolderItem;
                    <span>@item.Text</span>
                    @if (item.HasSubtree)
                    {
                        <SunfishTreeViewSubtree Data="@item.SubtreeData"
                                                SelectionMode="Multiple"
                                                CheckBoxMode="CheckBoxMode.Multiple" />
                    }
                }
            </ItemTemplate>
        </TreeViewBinding>
    </TreeViewBindings>
</SunfishTreeView>
```

**Impact:** High complexity. Requires isolating state management per subtree while sharing visual rendering.

---

## Implementation Priority

Recommended implementation order based on user value vs. complexity:

| Priority | Feature | Complexity | User Value |
|---|---|---|---|
| P0 | Appearance parameter | Low | High — design system alignment |
| P0 | IconAfter support | Low | Medium — common UI pattern |
| P1 | Aside content template | Medium | High — badges/counts are ubiquitous |
| P1 | Actions template | Medium | High — interactive tree items |
| P1 | NavigationMode (treegrid) | Medium-High | High — accessibility for actions |
| P2 | Collapse animation | Medium | Medium — polish |
| P2 | Tree manipulation API | Medium | Medium — programmatic CRUD |
| P3 | Virtualization | High | High — large datasets |
| P3 | Infinite scrolling | Medium | Medium — depends on virtualization |
| P4 | Subtree support | High | Low — niche use cases |

---

## Features Where Sunfish Is Already Ahead

These features exist in Sunfish but **not** in Fluent UI's Tree. No action needed:

| Feature | Sunfish API |
|---|---|
| Built-in drag and drop | `EnableDragDrop`, `OnItemDrop`, `OnDragStart`, `OnDrag`, `OnDrop`, `OnDragEnd` |
| Inline editing | `AllowEditing`, `OnItemEdit` |
| Context menu | `OnItemContextMenu` |
| Checkbox cascade (tri-state) | `AllowCheckChildren`, `AllowCheckParents` |
| Filtering with ancestor visibility | `FilterFunc`, `ClearFilter()` |
| Custom checkbox template | `CheckboxTemplate` |
| Multi-level data binding (different models per level) | Multiple `TreeViewBinding` with `Level` |
| Data refresh methods | `Rebind()`, observable collections |
| Expand helpers | `ExpandAllAsync()`, `CollapseAllAsync()`, `SingleExpand`, `AutoExpand` |
| Item render callback | `OnItemRender` |

---

## Summary of New API Surface

### New Parameters on SunfishTreeView

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Appearance` | `TreeViewAppearance` | `Subtle` | Visual appearance variant |
| `NavigationMode` | `TreeViewNavigationMode` | `Tree` | Keyboard navigation mode |
| `AnimateExpand` | `bool` | `false` | Enable expand/collapse animation |
| `AnimationDuration` | `int` | `200` | Animation duration in ms |
| `EnableVirtualization` | `bool` | `false` | Enable row virtualization |
| `ItemHeight` | `float` | `36f` | Row height for virtualization |
| `OverscanCount` | `int` | `3` | Extra items rendered outside viewport |
| `Height` | `string` | `null` | Scroll container height |
| `OnScrollNearEnd` | `EventCallback<TreeViewScrollEventArgs>` | — | Infinite scroll callback |
| `ScrollThreshold` | `int` | `100` | Pixels from bottom to trigger scroll event |
| `ActionsTemplate` | `RenderFragment<object>` | `null` | Hover/focus action buttons |
| `AsideTemplate` | `RenderFragment<object>` | `null` | Right-aligned supplementary content |

### New Parameters on TreeViewBinding

| Parameter | Type | Description |
|---|---|---|
| `IconAfterField` | `string` | Property name for the trailing icon |
| `AsideTemplate` | `RenderFragment<object>` | Per-level aside content |
| `ActionsTemplate` | `RenderFragment<object>` | Per-level action buttons |

### New Methods on SunfishTreeView

| Method | Description |
|---|---|
| `AddItemAsync(TItem, bool)` | Add item with optional focus |
| `RemoveItemAsync(object)` | Remove item with focus management |
| `MoveItemAsync(object, object, int?)` | Move item to new parent |

### New Types

| Type | Kind | Description |
|---|---|---|
| `TreeViewAppearance` | Enum | Subtle, Transparent, SubtleAlpha |
| `TreeViewNavigationMode` | Enum | Tree, TreeGrid |
| `TreeViewScrollEventArgs` | Class | Event args for infinite scrolling |
| `SunfishTreeViewSubtree<TItem>` | Component | Independent subtree |
