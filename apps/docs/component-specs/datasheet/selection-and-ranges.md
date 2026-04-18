---
title: Selection and Ranges
page_title: DataSheet - Selection and Ranges
description: Cell and row selection model, rectangular range creation, and how selection drives clipboard and bulk operations in SunfishDataSheet.
slug: datasheet-selection-and-ranges
tags: sunfish,blazor,datasheet,selection,ranges,cells
published: True
position: 3
components: ["datasheet"]
---

# DataSheet Selection and Ranges

SunfishDataSheet supports single-cell focus and multi-cell rectangular range selection. Selection drives several other features: copy (Ctrl+C), paste (Ctrl+V), Fill Down (Ctrl+D), Delete, and bulk delete. This article describes the selection model, how ranges are created, and how selection state is used by the rest of the component.

>caption In this article:

* [Active Cell](#active-cell)
* [Rectangular Range Selection](#rectangular-range-selection)
* [Row Selection](#row-selection)
* [How Selection Is Created](#how-selection-is-created)
* [How Selection Is Used by Other Features](#how-selection-is-used-by-other-features)
* [Selection API](#selection-api)
* [Limitations](#limitations)

## Active Cell

The DataSheet always tracks a single **active cell** — the cell that currently has focus. The active cell is visually distinguished by a focus ring (styled via `DataSheetCellClass` with `isActive=true`).

**Active cell behavior:**

* Exactly one cell can be active at a time. Clicking a cell or navigating with keyboard arrows moves the active cell.
* The active cell determines the anchor point for range selection and clipboard operations.
* When a cell enters edit mode, it remains the active cell. Committing or cancelling an edit does not change which cell is active (only navigation keys move it).
* If no cell has been focused yet (e.g., the DataSheet just loaded), there is no active cell until the user clicks or tabs into the grid.

## Rectangular Range Selection

In addition to the active cell, users can select a **rectangular range** of cells spanning multiple rows and columns. The range is defined by two corners: the anchor cell (where selection started) and the extent cell (where it ended).

**Range characteristics:**

* A range always forms a rectangle — it includes all cells in the rows between the anchor and extent, across the columns between the anchor and extent.
* The active cell is always part of the selected range (it is the anchor).
* A single-cell selection is a degenerate range where anchor and extent are the same cell.
* Computed columns are included in the visual selection highlight, but are skipped by operations that modify cell values (paste, Fill Down, Delete).

## Row Selection

Row-level selection is used for **bulk delete** operations when `AllowDeleteRow` is `true`. Row selection is separate from cell range selection:

* Clicking a row's delete checkbox (or using a row selection mechanism) toggles that row's selected state.
* Selected rows are tracked in an internal `HashSet<TItem>`.
* The bulk action bar shows the count of selected rows and offers a "Delete Selected" action.
* Row selection does not affect cell range selection — both can coexist.

## How Selection Is Created

### Mouse

| Action | Result |
| --- | --- |
| **Click** a cell | Sets the active cell and clears any existing range. Creates a single-cell selection. |
| **Shift+Click** a cell | Extends the selection from the current active cell (anchor) to the clicked cell (extent), forming a rectangular range. The active cell does not move. |
| **Click and drag** | Creates a rectangular range from the mousedown cell (anchor) to the mouseup cell (extent). The anchor becomes the active cell. |

### Keyboard

| Action | Result |
| --- | --- |
| **Arrow keys** (without Shift) | Moves the active cell one step in the arrow direction. Clears any existing range. |
| **Shift+Arrow keys** | Extends the selection range from the active cell in the arrow direction. The active cell (anchor) does not move; the extent expands. |
| **Tab / Shift+Tab** | Moves the active cell to the next/previous editable cell. Clears any range. |
| **Ctrl+A** | Selects all cells in the DataSheet (full rectangular range). |

### Programmatic

Selection is currently managed internally by the component. There is no public API to get or set the selected range programmatically. If a future release exposes selection state, it will follow the shape described in the [Selection API](#selection-api) section.

## How Selection Is Used by Other Features

The selected range (or the active cell when no multi-cell range exists) determines the scope for several operations:

| Feature | How selection is used |
| --- | --- |
| **Copy (Ctrl+C)** | Copies the values of all cells in the selected range as a TSV string to the clipboard. See [Bulk Paste and Clipboard](slug:datasheet-bulk-paste-and-clipboard). |
| **Paste (Ctrl+V)** | Pastes TSV data from the clipboard starting at the active cell (top-left anchor). The pasted data fills a rectangular region anchored at the active cell. See [Bulk Paste and Clipboard](slug:datasheet-bulk-paste-and-clipboard). |
| **Fill Down (Ctrl+D)** | Copies the value of the active cell (top row of the selection) down to all cells in the same column within the selected range. Only editable, non-computed cells are filled. |
| **Delete** | Clears all editable cells in the selected range, setting each to its type's default value (`""` for text, `0` for numbers, `null` for dates, `false` for checkboxes). |
| **Bulk Delete** | Row selection (not cell range) determines which rows are marked for deletion. |

## Selection API

> Selection state is currently internal to the component. The following describes the intended shape for a future public API, if exposed.

**Planned selection state shape:**

```csharp
public class DataSheetSelection
{
    public (TItem Row, string Field) ActiveCell { get; }
    public (TItem AnchorRow, string AnchorField,
            TItem ExtentRow, string ExtentField)? Range { get; }
}
```

Currently, the only way to observe which cell is active is through the `DataSheetCellContext.IsEditing` property in a `CellTemplate`, which indicates editing state but not mere focus. There is no public event for selection changes.

## Limitations

* **No non-contiguous selection** — the DataSheet does not support selecting multiple disjoint ranges (e.g., Ctrl+Click to add individual cells to a selection). Only a single rectangular range is supported at a time.
* **No column or row header click-to-select** — clicking a column header does not select the entire column. Clicking a row number (if displayed) does not select the entire row for cell operations. Row selection for deletion is handled by separate row checkboxes.
* **Selection and editing** — entering edit mode on a cell within a multi-cell selection does not clear the selection, but operations like Delete or Fill Down will not execute while a cell is in edit mode (the user must commit or cancel first).
* **Selection and virtualization** — when virtualization is enabled, only visible rows are rendered. Selecting a range that extends beyond visible rows works correctly at the data level, but the visual highlight is only rendered for visible rows. Scrolling reveals the highlight for newly visible rows within the range.

## See Also

* [DataSheet Overview](slug:datasheet-overview)
* [Bulk Paste and Clipboard](slug:datasheet-bulk-paste-and-clipboard)
* [Keyboard and Accessibility](slug:datasheet-keyboard-and-accessibility)
