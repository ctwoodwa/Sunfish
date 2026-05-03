# Implementation Notes: Navigation

## SunfishTreeView — Source Review Design Decisions

### 1. Tri-State Checkbox Propagation (Gap 7)

**Decision: Follow Radzen's `AllowCheckChildren` / `AllowCheckParents` pattern.**

Radzen's approach is the cleanest Blazor-native model. Two independent booleans let consumers control cascade direction independently—critical because MudBlazor issue #9411 showed that bidirectional cascading without opt-out causes real-world pain (parent selection forced when checking a single child in single-child trees).

**Implementation approach:**
- `IsChecked(nodeId)` returns `bool?`: `true` (all descendants checked), `false` (none checked), `null` (partial/indeterminate)
- Checking a parent with `AllowCheckChildren=true` calls `GetAllChildValues()` recursively and adds them to `CheckedItems`
- Child check with `AllowCheckParents=true` walks up ancestors recalculating their `bool?` state
- HTML `<input type="checkbox">` requires JS interop for `indeterminate` property (no Blazor-native way to set it), OR use a CSS-only approach with `:indeterminate` pseudo-class via a `SunfishCheckbox` sub-component
- **Lesson from MudBlazor #2550**: Selection and checkboxes should be treated as separate concerns. Selection = highlighting/active state. Checkboxes = multi-pick state. Both can coexist independently.

### 2. Multi-Selection Mode (Gap 9)

**Decision: Use MudBlazor's `SelectionMode` enum pattern, but avoid their pitfall of conflating selection with checkboxes.**

MudBlazor discussions #6227 and issues #9411/#2550 reveal that:
- Custom `ItemTemplate` broke multi-selection UI in MudBlazor because checkboxes were embedded in the default template
- The maintainers concluded "there are gonna be too many different ways people will want multi-selection to work" — so keep it configurable

**Implementation approach:**
- `SelectionMode` enum: `Single` (default, current behavior), `Multiple` (Ctrl+click adds, Shift+click ranges), `None` (no selection)
- Selection state is separate from checkbox state — checking a checkbox doesn't select the item (unless explicitly wired by consumer)
- In `Multiple` mode, `SelectItem()` does NOT clear `_selectedIds`; it toggles the clicked item

### 3. Lazy Loading (Gap 10)

**Decision: Combine MudBlazor's `ServerData` callback pattern with BlazorVirtualTreeView's load-once semantics.**

MudBlazor discussion #4906 showed that even with `ServerData`, the component rendered all 4,500 items at init — the callback pattern alone isn't enough without proper guarding. BlazorVirtualTreeView correctly loads children only on first expand.

**Implementation approach:**
- Add `Func<object, Task<IEnumerable<object>>>? LoadChildrenAsync` parameter
- In `ToggleNodeAsync`, when expanding a node with `HasChildren=true` and no loaded children:
  1. Add node ID to `_loadingIds` and re-render (shows loading indicator — already implemented)
  2. Invoke `LoadChildrenAsync(parentItem)` 
  3. Merge returned items into the tree structure
  4. Track loaded node IDs in `_loadedNodeIds` HashSet to prevent re-fetching
  5. Remove from `_loadingIds` and re-render
- Guard against double-loading with the `_loadedNodeIds` set

### 4. Keyboard Navigation (Gap 11)

**Decision: Follow Fancytree's keyboard model, which aligns with WAI-ARIA TreeView pattern.**

Fancytree treats the tree as a single tab-stop with internal arrow key navigation. This is the standard ARIA pattern and what Telerik/Syncfusion implement.

**Implementation approach:**
- Root `<ul role="tree" tabindex="0">` receives focus
- Track `_focusedNodeId` (distinct from selected — focus is visual ring, selection is state)
- Maintain a flat ordered list of visible node IDs for arrow key traversal
- Key mappings:
  - `ArrowDown` → next visible node
  - `ArrowUp` → previous visible node
  - `ArrowRight` → if collapsed, expand; if expanded, move to first child
  - `ArrowLeft` → if expanded, collapse; if collapsed, move to parent
  - `Enter` / `Space` → select/activate (and toggle checkbox if present)
  - `Home` → first node, `End` → last visible node
  - `*` → expand all siblings (Fancytree pattern)
- Add `@onkeydown` on root element, prevent default for handled keys

### 5. Virtualization Strategy (Gap 18)

**Decision: Use BlazorVirtualTreeView's flatten-and-virtualize approach with Blazor's built-in `<Virtualize>` component.**

This is the most architecturally significant change. BlazorVirtualTreeView proves the pattern works: flatten the visible tree into a list, use `<Virtualize>` to render only visible rows.

**Implementation approach:**
- Add `bool Virtualize` parameter (default false for backward compat)
- When `Virtualize=true`, replace recursive `RenderNodes()` with:
  1. `FlattenVisibleNodes()` — walks tree, includes only nodes whose ancestors are all expanded, returns `List<FlatNode>` with depth info
  2. Render via `<Virtualize Items="@_flatNodes" Context="node" ItemSize="32">` (ItemSize configurable via `ItemHeight` parameter)
  3. Each flat node renders with `padding-left: {depth * indentSize}px` for hierarchy visual
- Recompute `_flatNodes` on expand/collapse and data changes
- This approach handles keyboard navigation naturally since the flat list IS the navigation order

### 6. Filtering (Gap 16)

**Decision: Follow Fancytree's "keep ancestors visible" pattern.**

When filtering, matched nodes AND all their ancestors must remain visible (otherwise matches inside collapsed branches become invisible). Fancytree handles this cleanly.

**Implementation approach:**
- `Func<object, bool>? FilterFunc` parameter
- When set, `BuildTree()` / `RenderNodes()` marks nodes as hidden/visible
- Ancestor chain of any matching node is forced visible
- Matching nodes get a `mar-tree-item--filter-match` CSS class for highlighting
- `ClearFilter()` public method resets

## Architecture Notes

### State Separation Pattern (from MudBlazor lessons)

The tree manages four independent state sets:
1. **Expanded** (`_expandedIds`) — which nodes are open → `@bind-ExpandedItems`
2. **Selected** (`_selectedIds`) — which nodes are highlighted/active → `@bind-SelectedItems`
3. **Checked** (`_checkedIds`) — which nodes have checkboxes ticked → `@bind-CheckedItems` (new)
4. **Focused** (`_focusedNodeId`) — which node has keyboard focus → internal only, visual ring

These are intentionally independent. Radzen and MudBlazor both learned this the hard way — conflating selection with checking causes edge cases that are hard to resolve generically.

### Drag-and-Drop Enhancement Notes

Current DnD uses HTML5 drag events. For future enhancement (Fancytree-level DnD):
- Add drop-position indicators (before/after/inside target) — not just "onto"
- Add `DragThrottleInterval` for performance (Telerik pattern)
- Add `OnDragStart` / `OnDragEnd` events for validation/cancellation
- Consider tree-to-tree drag support via a shared drag context service
