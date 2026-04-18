---
title: Keyboard Navigation
page_title: AllocationScheduler Keyboard Navigation
description: Complete keyboard shortcut reference for the Sunfish Blazor AllocationScheduler — cell navigation, edit mode, selection, clipboard, context menu, and date navigation.
slug: allocation-scheduler-keyboard-navigation
tags: sunfish,blazor,allocation-scheduler,keyboard,navigation,a11y,shortcuts
published: True
position: 21
components: ["allocation-scheduler"]
---

# AllocationScheduler Keyboard Navigation

The AllocationScheduler supports full keyboard operation — cell navigation, range selection, typed-in editing, drag-fill via keyboard, clipboard, and context menu commands. This page is the authoritative shortcut reference and the contract that the JS interop module (`AllocationSchedulerInterop`) must honor.

## Basics

Keyboard navigation is enabled by default via the `AllowKeyboardEdit` parameter. When enabled, the timeline grid installs a single roving tabindex on the active cell, and all keyboard handlers are bound through the JS interop module `./_content/Sunfish.Components/js/allocation-scheduler.js`.

```razor
<SunfishAllocationScheduler Resources="@resources"
                           Allocations="@allocations"
                           AuthoritativeLevel="TimeGranularity.Week"
                           AllowKeyboardEdit="true" />
```

To disable all keyboard editing (making the component effectively keyboard read-only), set `AllowKeyboardEdit="false"`. Arrow-key navigation still works in this mode, but F2, Enter, type-to-edit, Delete, and paste are disabled.

## Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `AllowKeyboardEdit` | `bool` | `true` | Enables keyboard-driven editing (F2, Enter, type-to-edit, Delete, paste). When `false`, arrow navigation still works but edit shortcuts are suppressed. |
| `AllowDragFill` | `bool` | `true` | Enables drag-fill. The fill handle renders on the active cell and is operable via keyboard when the cell is focused. |
| `EnableContextMenu` | `bool` | `true` | Enables the right-click context menu and its keyboard openers (`Menu` key, `Shift+F10`). |
| `SelectionMode` | `AllocationSelectionMode` | `Range` | Controls what selection keyboard shortcuts do. In `None`, all selection shortcuts are no-ops. In `Cell`, `Shift+Arrow` is a no-op. |

## Navigation Model

The AllocationScheduler uses a **single-stop roving tabindex** pattern: the entire grid is one Tab stop. Pressing Tab from outside focuses the active cell (or the first cell if none is active). Pressing Tab while the active cell is focused moves focus out of the grid to the next page element.

Inside the grid, arrow keys move the active cell. This is standard ARIA grid behavior — see the [WAI-ARIA Authoring Practices Grid pattern](https://www.w3.org/WAI/ARIA/apg/patterns/grid/).

## Default Keys

### All Grid Cells (navigation)

These keys work regardless of edit state, as long as a cell is focused.

| Shortcut | Action |
|---|---|
| `Arrow Right` | Move active cell to next bucket (same resource). |
| `Arrow Left` | Move active cell to previous bucket (same resource). |
| `Arrow Down` | Move active cell to next resource (same bucket). |
| `Arrow Up` | Move active cell to previous resource (same bucket). |
| `Home` | Move active cell to first bucket in current resource row. |
| `End` | Move active cell to last bucket in current resource row. |
| `Ctrl + Home` | Move active cell to top-left cell of the visible grid. |
| `Ctrl + End` | Move active cell to bottom-right cell of the visible grid. |
| `Page Down` | Scroll down one resource-row page; active cell moves with it. |
| `Page Up` | Scroll up one resource-row page; active cell moves with it. |
| `Alt + Page Down` | Scroll right one bucket page (horizontal paging). |
| `Alt + Page Up` | Scroll left one bucket page. |
| `Tab` | Exit the grid to the next focusable element on the page. |
| `Shift + Tab` | Exit the grid to the previous focusable element on the page. |
| `Esc` | Cancel edit if editing, then clear selection, then close context menu. |

When navigation reaches the edge of the visible range, the component calls the appropriate paging method internally to scroll one bucket further, so continuous arrow presses walk indefinitely through the data.

### Data Cells (edit activation)

These keys activate edit mode at the active cell. All require `AllowKeyboardEdit="true"` and a cell where `AuthoritativeLevel` matches the current `ViewGrain` (or `AllowZoomEdit` is `true`).

| Shortcut | Action |
|---|---|
| `F2` | Enter edit mode. The cell's current value becomes the editor's starting text. Fires `OnEnterEditMode` on the JS side. |
| `Enter` | Enter edit mode (same as F2). |
| Any printable character (`0-9`, `-`, `.`, letters) | Enter edit mode and replace the cell value with the typed character. Fires `OnStartTyping` with the initial character. |
| `Delete` or `Backspace` | Clears the active cell to zero. Fires `OnDeletePressed`, which commits a `CellEditedArgs` with `NewValue = 0m`. Does NOT enter edit mode. |

### Data Cells (commit / cancel)

Once in edit mode, these keys commit or cancel.

| Shortcut | Action |
|---|---|
| `Enter` | Commit the edit. Fires `OnCellEdited`. Active cell moves **down** one resource. If `AllowBulkEdit` and a range is selected, fires `OnRangeEdited` for the whole range. |
| `Tab` | Commit the edit. Active cell moves **right** one bucket. Stays in edit mode so the next cell is ready for typing. |
| `Shift + Tab` | Commit the edit. Active cell moves **left** one bucket. Stays in edit mode. |
| `Esc` | Cancel the edit. Reverts to the old value. Exits edit mode. |
| `Arrow Up` / `Arrow Down` | Commit the edit and move vertically one resource. Stays in edit mode. |

### Selection Keys

| Shortcut | Action |
|---|---|
| `Shift + Arrow Right` | Extend selection one bucket right. Active cell moves right. |
| `Shift + Arrow Left` | Extend selection one bucket left. |
| `Shift + Arrow Up` | Extend selection one resource up. |
| `Shift + Arrow Down` | Extend selection one resource down. |
| `Shift + Home` | Extend selection from active cell to first bucket in current resource. |
| `Shift + End` | Extend selection from active cell to last bucket in current resource. |
| `Shift + Ctrl + Home` | Extend selection from active cell to top-left of visible grid. |
| `Shift + Ctrl + End` | Extend selection from active cell to bottom-right of visible grid. |
| `Ctrl + A` | Select all visible cells at the current `ViewGrain`. Only active when `SelectionMode="Range"`. |
| `Esc` | If a range is selected, collapses the selection to just the active cell. A second `Esc` clears the selection entirely. |

All selection shortcuts are no-ops when `SelectionMode="None"`. When `SelectionMode="Cell"`, only single-cell movement is supported — `Shift+Arrow` still moves the active cell but does not extend the selection.

### Clipboard Keys

| Shortcut | Action |
|---|---|
| `Ctrl + C` | Copy the current selection to the clipboard as TSV. Header row and resource labels are included so the text round-trips through Excel and Google Sheets. |
| `Ctrl + X` | Copy then clear. Writes TSV to clipboard, then applies `0` to all selected cells (fires `OnRangeEdited`). Requires `AllowKeyboardEdit`. |
| `Ctrl + V` | Paste TSV from clipboard into the rectangle anchored at the active cell. Fires `OnPasteData(tsv)` on the .NET side, which parses and fires `OnRangeEdited` with the resolved records. If the pasted data does not fit the visible grid, it is clipped (not wrapped). |

Clipboard operations go through the JS interop module — the `initClipboard` call binds document-level handlers when the grid has focus and releases them on blur.

### Drag-Fill via Keyboard

When `AllowDragFill="true"` and the active cell has the fill handle, these keys operate the handle without a mouse:

| Shortcut | Action |
|---|---|
| `Ctrl + D` | Fill down — copy the active cell value into every cell below it in the current selection. |
| `Ctrl + R` | Fill right — copy the active cell value into every cell to the right in the current selection. |
| `Ctrl + Shift + D` | Fill down with series extrapolation (if two or more cells are selected in the source column). |
| `Ctrl + Shift + R` | Fill right with series extrapolation. |

These shortcuts go through the same `OnDragFillCompleted` JS callback as mouse drag-fill, so the event payload shape is identical.

### Context Menu Keys

| Shortcut | Action |
|---|---|
| `Menu` key (Windows application key) | Open the context menu anchored on the active cell. |
| `Shift + F10` | Open the context menu anchored on the active cell. |
| `Arrow Down` / `Arrow Up` (inside menu) | Move focus between menu items. |
| `Enter` / `Space` (inside menu) | Invoke the focused menu item. Fires `OnContextMenuAction`. |
| `Esc` (inside menu) | Close the menu without invoking anything. Returns focus to the active cell. |
| `Tab` (inside menu) | Close the menu and move focus to the next page element. |

Submenus open on `Arrow Right` and close on `Arrow Left`. See [context-menu.md](context-menu.md) for the full menu surface.

### Date / View Navigation Keys

Date and view navigation are exposed as public methods rather than keyboard shortcuts, because the common Alt+Arrow / Ctrl+Home bindings would conflict with cell navigation. Host pages can bind these to their own toolbar buttons.

| Method | Purpose |
|---|---|
| `NavigateTo(DateTime date)` | Jump to a specific date; adjusts `VisibleStart` / `VisibleEnd` to the configured window. |
| `NavigateForward()` | Move the visible window one window-width forward. |
| `NavigateBack()` | Move the visible window one window-width backward. |
| `NavigateToToday()` | Center the visible window on today at the current grain. |

Host code can still wire shortcuts from the surrounding page if needed:

```razor
<div @onkeydown="HandlePageKey" tabindex="0">
    <SunfishAllocationScheduler @ref="_scheduler" ... />
</div>

@code {
    private SunfishAllocationScheduler _scheduler = default!;

    private async Task HandlePageKey(KeyboardEventArgs e)
    {
        if (e.CtrlKey && e.Key == "ArrowRight") await _scheduler.NavigateForward();
        else if (e.CtrlKey && e.Key == "ArrowLeft") await _scheduler.NavigateBack();
        else if (e.CtrlKey && e.Key == "Home") await _scheduler.NavigateToToday();
    }
}
```

## JS Interop Entry Points

The keyboard surface is implemented by JS interop callbacks invoked from `allocation-scheduler.js`. These are all `[JSInvokable]` methods on `SunfishAllocationScheduler` — host code should not call them directly, but they are documented here because they form the event contract between JS and .NET:

| Method | Triggered by | Notes |
|---|---|---|
| `OnCellFocused(string cellKeyJson)` | Arrow keys, Home, End, Tab into grid | Sets `_activeCell`, updates roving tabindex, fires ARIA live-region announcement. |
| `OnEnterEditMode(string cellKeyJson)` | `F2`, `Enter` (not in edit mode) | Sets `_editMode = true`, seeds `_editValue` from current cell value. |
| `OnStartTyping(string cellKeyJson, string initialChar)` | Printable character while not in edit mode | Sets `_editMode = true`, seeds `_editValue` with `initialChar`. |
| `OnEscapePressed()` | `Esc` | Cascades: cancel edit → clear selection → close context menu. |
| `OnDeletePressed(string cellKeyJson)` | `Delete`, `Backspace` (not in edit mode) | Commits `CellEditedArgs` with `NewValue = 0m`. |
| `OnDragFillCompleted(string payloadJson)` | Mouse drag-fill release, or `Ctrl+D`/`Ctrl+R` | Payload shape: `{source: {resourceKey, bucketStart}, targets: [...]}`. |
| `OnPasteData(string tsv)` | `Ctrl+V` | TSV is parsed by the component and applied to the rectangle anchored at `_activeCell`. |

The JS module is initialized in `OnAfterRenderAsync` via three calls:

```csharp
await _interopModule.InvokeVoidAsync("AllocationSchedulerInterop.initKeyboardNav", _gridRef, _dotNetRef);
await _interopModule.InvokeVoidAsync("AllocationSchedulerInterop.initDragFill", _gridRef, _dotNetRef);
await _interopModule.InvokeVoidAsync("AllocationSchedulerInterop.initClipboard", _gridRef, _dotNetRef);
```

Reading the source of `allocation-scheduler.js` is the authoritative answer for any keyboard behavior not documented here.

## Accessibility

The keyboard model conforms to the [WAI-ARIA Grid pattern](https://www.w3.org/WAI/ARIA/apg/patterns/grid/):

- `role="grid"` on the timeline container, `role="row"` on each resource row, `role="gridcell"` on each cell.
- Single-stop roving tabindex: exactly one cell has `tabindex="0"` at any time. All others have `tabindex="-1"`.
- `aria-rowcount` / `aria-colcount` set to the full logical row/column counts, not just visible.
- `aria-rowindex` / `aria-colindex` set per cell so virtualization does not confuse screen readers.
- `aria-selected="true"` on cells within the active selection.
- `aria-live="polite"` region announces selection changes and cell-value commits.
- `aria-describedby` on the active cell points to a hidden span carrying "{Resource} — {BucketStart:d} — {Value}".

See [accessibility.md](accessibility.md) for the complete a11y contract.

## Quick Reference

```
NAVIGATE
  Arrow keys ........... move active cell
  Home / End ........... first / last bucket in row
  Ctrl+Home / Ctrl+End . top-left / bottom-right
  Page Up / Page Down .. vertical paging
  Alt+PgUp / Alt+PgDn .. horizontal paging
  Tab / Shift+Tab ...... exit grid

SELECT
  Shift + Arrow ........ extend selection
  Shift + Home/End ..... extend to row edge
  Ctrl + A ............. select all visible
  Esc .................. collapse / clear

EDIT
  F2 / Enter / typing .. enter edit mode
  Delete / Backspace ... clear cell (NewValue=0)
  Enter ................ commit, move down
  Tab / Shift+Tab ...... commit, move right / left
  Esc .................. cancel

CLIPBOARD
  Ctrl + C ............. copy selection (TSV)
  Ctrl + X ............. cut selection
  Ctrl + V ............. paste TSV

FILL
  Ctrl + D ............. fill down
  Ctrl + R ............. fill right
  Ctrl + Shift + D/R ... fill with series extrapolation

MENU
  Menu / Shift+F10 ..... open context menu
  Arrow keys / Enter ... navigate / invoke
  Esc .................. close
```

## See Also

- [Selection](selection.md)
- [Editing](editing.md)
- [Context Menu](context-menu.md)
- [Events](events.md)
- [Accessibility](accessibility.md)
