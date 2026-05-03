# Test Plan: Navigation

## Test Categories

### Rendering Tests
- [ ] Components render with default parameters
- [ ] Components apply Class and Style parameters
- [ ] Components render AdditionalAttributes

### Parameter Tests — TreeView Source-Review Gaps

#### Tri-State Checkboxes (Gap 7)
- [ ] Checking parent with `AllowCheckChildren=true` checks all descendants
- [ ] Unchecking parent unchecks all descendants
- [ ] Checking all children with `AllowCheckParents=true` auto-checks parent
- [ ] Checking some children renders parent as indeterminate
- [ ] `AllowCheckChildren=false` prevents cascade to children
- [ ] `AllowCheckParents=false` prevents cascade to parent
- [ ] Tri-state works correctly with deeply nested hierarchies (3+ levels)

#### CheckedItems Binding (Gap 8)
- [ ] `@bind-CheckedItems` reflects internal checked state
- [ ] Setting `CheckedItems` externally updates internal `_checkedIds`
- [ ] `CheckedItemsChanged` fires when user toggles checkbox

#### Multi-Selection (Gap 9)
- [ ] `SelectionMode.Single` clears previous selection on click (current behavior)
- [ ] `SelectionMode.Multiple` adds to selection without clearing
- [ ] `SelectionMode.None` prevents selection entirely
- [ ] `SelectedItems` binding works in all selection modes

#### Lazy Loading (Gap 10)
- [ ] `LoadChildrenAsync` callback fires on first expand of `HasChildren=true` node
- [ ] Loading indicator shown during async load
- [ ] Children render after callback completes
- [ ] Subsequent expand/collapse does NOT re-invoke callback
- [ ] Error in callback does not leave tree in broken state

#### Keyboard Navigation (Gap 11)
- [ ] ArrowDown moves focus to next visible node
- [ ] ArrowUp moves focus to previous visible node
- [ ] ArrowRight on collapsed node expands it
- [ ] ArrowRight on expanded node moves focus to first child
- [ ] ArrowLeft on expanded node collapses it
- [ ] ArrowLeft on collapsed node moves focus to parent
- [ ] Enter/Space selects the focused node
- [ ] Enter/Space toggles checkbox when checkboxes enabled
- [ ] Home moves focus to first node, End to last visible node
- [ ] Tab/Shift+Tab moves focus in/out of tree (single tab stop)

#### ExpandOnClick / SingleExpand (Gaps 12-13)
- [ ] `ExpandOnClick=true` expands node when clicking anywhere on header
- [ ] `ExpandOnDoubleClick=true` expands on double-click only
- [ ] `SingleExpand=true` collapses siblings when a node expands

#### AutoExpand (Gap 14)
- [ ] Setting `SelectedItems` programmatically with `AutoExpand=true` expands ancestors

#### ExpandAll / CollapseAll (Gap 15)
- [ ] `ExpandAllAsync()` expands every node in tree
- [ ] `CollapseAllAsync()` collapses every node in tree
- [ ] Both fire `ExpandedItemsChanged`

#### Filtering (Gap 16)
- [ ] `FilterFunc` hides non-matching leaf nodes
- [ ] Ancestors of matching nodes remain visible
- [ ] Matching nodes receive filter highlight CSS class
- [ ] Removing filter restores all nodes

#### Disabled / ReadOnly (Gap 17)
- [ ] `Disabled=true` prevents all interaction (click, drag, keyboard)
- [ ] `Disabled=true` sets `aria-disabled` on tree
- [ ] `ReadOnly=true` shows current state but prevents changes

#### Virtualization (Gap 18)
- [ ] `Virtualize=true` renders only visible nodes in viewport
- [ ] Expand/collapse correctly updates virtualized flat list
- [ ] Scroll position maintained during expand/collapse
- [ ] Large dataset (5000+ nodes) renders without noticeable lag

### Event Tests
- [ ] `OnItemClick` fires with correct item on click
- [ ] `OnItemDrop` fires with correct source/target on drag-drop
- [ ] `OnItemContextMenu` fires on right-click (Gap 20)
- [ ] `OnItemEdit` fires with new text after inline edit (Gap 22)

### Accessibility Tests
- [ ] Root element has `role="tree"` and `tabindex="0"`
- [ ] Items have `role="treeitem"` with correct `aria-expanded` and `aria-selected`
- [ ] Groups have `role="group"`
- [ ] Keyboard navigation follows WAI-ARIA TreeView pattern
- [ ] Indeterminate checkbox state communicated to assistive tech
- [ ] `aria-disabled` applied when tree is disabled

## bUnit Test Patterns

### Tri-State Checkbox Test Example
```csharp
[Fact]
public void CheckParent_WithAllowCheckChildren_CascadesToAllDescendants()
{
    var data = BuildThreeLevelTree(); // helper
    var cut = RenderComponent<SunfishTreeView>(p => p
        .Add(t => t.Data, data)
        .Add(t => t.CheckBoxMode, CheckBoxMode.Multiple)
        .Add(t => t.AllowCheckChildren, true)
        .Add(t => t.IdField, "Id")
        .Add(t => t.TextField, "Name")
        .Add(t => t.ItemsField, "Children"));
    
    // Click parent checkbox
    var parentCheckbox = cut.FindAll(".mar-tree-item__checkbox").First();
    parentCheckbox.Change(true);
    
    // All descendant checkboxes should be checked
    var allCheckboxes = cut.FindAll(".mar-tree-item__checkbox");
    allCheckboxes.Should().AllSatisfy(cb => cb.IsChecked().Should().BeTrue());
}
```

### Lazy Loading Test Example
```csharp
[Fact]
public async Task ExpandNode_WithLoadChildrenAsync_InvokesCallbackOnce()
{
    int callCount = 0;
    var cut = RenderComponent<SunfishTreeView>(p => p
        .Add(t => t.Data, new[] { new { Id = "1", Name = "Parent", HasChildren = true } })
        .Add(t => t.HasChildrenField, "HasChildren")
        .Add(t => t.LoadChildrenAsync, async (parent) => {
            callCount++;
            return new[] { new { Id = "1-1", Name = "Child" } };
        }));
    
    // First expand — should load
    cut.Find(".mar-tree-item__toggle").Click();
    await Task.Delay(50);
    callCount.Should().Be(1);
    
    // Collapse and re-expand — should NOT reload
    cut.Find(".mar-tree-item__toggle").Click();
    cut.Find(".mar-tree-item__toggle").Click();
    await Task.Delay(50);
    callCount.Should().Be(1);
}
```
