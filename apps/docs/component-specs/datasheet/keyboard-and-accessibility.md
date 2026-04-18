---
title: Keyboard and Accessibility
page_title: DataSheet - Keyboard and Accessibility
description: Keyboard shortcuts, focus model, ARIA roles, and screen reader support for SunfishDataSheet.
slug: datasheet-keyboard-and-accessibility
tags: sunfish,blazor,datasheet,keyboard,accessibility,aria,a11y
published: True
position: 7
components: ["datasheet"]
---

# DataSheet Keyboard and Accessibility

SunfishDataSheet provides full keyboard navigation for all editing and data operations, and exposes an accessible grid structure using WAI-ARIA roles and attributes. This article defines the normative keyboard shortcut contracts, the focus model, and accessibility requirements.

>caption In this article:

* [Keyboard Shortcuts](#keyboard-shortcuts)
* [Focus Model](#focus-model)
* [Navigation Behavior](#navigation-behavior)
* [Edit Mode and Focus Interaction](#edit-mode-and-focus-interaction)
* [ARIA Roles and Attributes](#aria-roles-and-attributes)
* [Screen Reader Announcements](#screen-reader-announcements)
* [Accessibility Checklist](#accessibility-checklist)

## Keyboard Shortcuts

The following keyboard shortcuts are handled by the DataSheet. The JavaScript interop layer captures keydown events on the grid element and prevents default browser behavior for these keys.

### Navigation Keys

| Key | Context | Behavior |
| --- | --- | --- |
| **Arrow Up** | Not in edit mode | Moves the active cell up one row in the same column. No effect on the first row. |
| **Arrow Down** | Not in edit mode | Moves the active cell down one row in the same column. No effect on the last row. |
| **Arrow Left** | Not in edit mode | Moves the active cell left one column in the same row. No effect on the first column. |
| **Arrow Right** | Not in edit mode | Moves the active cell right one column in the same row. No effect on the last column. |
| **Tab** | Any | If in edit mode, commits the current cell. Moves the active cell to the next editable cell (left to right, top to bottom). Wraps from the last column of a row to the first editable column of the next row. At the last cell of the last row, focus leaves the grid. |
| **Shift+Tab** | Any | If in edit mode, commits the current cell. Moves the active cell to the previous editable cell (right to left, bottom to top). Wraps from the first column of a row to the last editable column of the previous row. At the first cell of the first row, focus leaves the grid. |
| **Enter** | Edit mode | Commits the current cell and moves the active cell down one row in the same column. If on the last row, commits without moving. |
| **Enter** | Not in edit mode | Enters edit mode on the active cell (same as F2). |

### Editing Keys

| Key | Context | Behavior |
| --- | --- | --- |
| **F2** | Not in edit mode | Enters edit mode on the active cell. The editor opens with the current value. For text columns, the full value is selected. No effect on non-editable or computed cells. |
| **Escape** | Edit mode | Cancels the current edit. The cell reverts to the value it had before entering edit mode. The cell exits edit mode but remains the active cell. |
| **Printable character** | Not in edit mode | Enters edit mode and replaces the cell value with the typed character (text and number columns only). |
| **Delete** | Not in edit mode | Clears all editable cells in the current selection. Sets each cell to its type's default value: `""` for text, `0`/`null` for numbers, `null`/`default` for dates, `false` for checkboxes. Computed and non-editable cells are skipped. |
| **Space** | Active cell is checkbox | Toggles the checkbox value. No separate edit mode needed. |

### Command Keys

| Key | Context | Behavior |
| --- | --- | --- |
| **Ctrl+S** | Any | Triggers Save All. Runs validation, then fires `OnSaveAll` if all cells are valid. If a cell is in edit mode, it is committed first. |
| **Ctrl+Z** | Any | Undoes the last cell edit on the active cell. Restores the value the cell had before its most recent commit. Single-level undo only. |
| **Ctrl+C** | Not in edit mode | Copies the selected cell range (or the active cell if no range is selected) to the clipboard as TSV. |
| **Ctrl+V** | Not in edit mode | Pastes TSV data from the clipboard, starting at the active cell. See [Bulk Paste and Clipboard](slug:datasheet-bulk-paste-and-clipboard). Requires `AllowBulkPaste=true`. |
| **Ctrl+D** | Not in edit mode | Fill Down. Copies the active cell's value to all cells below it in the same column within the current selection range. Only fills editable, non-computed cells. |
| **Ctrl+A** | Not in edit mode | Selects all cells in the DataSheet. |

### Key Handling Implementation

The grid's root element has `tabindex="0"` to receive keyboard focus. The JavaScript interop module (`sunfish-datasheet.js`) registers a `keydown` listener on the grid element and calls the .NET `HandleKeyDown` method for most keys. Clipboard operations (Ctrl+C, Ctrl+V) are handled in JavaScript to interact with the browser's Clipboard API, with paste data forwarded to .NET via `PasteFromClipboard`.

Default browser behavior is suppressed (`event.preventDefault()`) for all keys listed above when the grid has focus. This prevents, for example, Ctrl+S from opening the browser's save dialog.

## Focus Model

The DataSheet uses a **roving tabindex** approach for cell navigation:

* The grid root element (`role="grid"`) has `tabindex="0"` and receives initial focus when the user tabs into the component.
* Once focused, arrow keys and Tab/Shift+Tab move the active cell. The active cell is tracked in component state (`_activeCellRow` and `_activeCellField`).
* Only one cell is the active cell at any time. The active cell receives visual focus styling via `DataSheetCellClass(state, isActive: true, isEditable)`.
* When a cell enters edit mode, focus transfers to the editor input element within the cell. The cell remains the active cell.
* When the user commits or cancels, focus returns to the cell (or moves to the next cell if triggered by Tab/Enter).
* If the user clicks outside the grid, the grid loses focus and no cell is active. Clicking back into the grid sets the clicked cell as the active cell.

## Navigation Behavior

### Non-Edit Mode Navigation

In non-edit mode, arrow keys move the active cell one step in the indicated direction. The active cell can land on non-editable cells (including computed columns) — focus moves freely across all columns.

**Boundary behavior:**

| Boundary | Arrow key | Result |
| --- | --- | --- |
| First row | Arrow Up | No movement. Active cell stays. |
| Last row | Arrow Down | No movement. Active cell stays. |
| First column | Arrow Left | No movement. Active cell stays. |
| Last column | Arrow Right | No movement. Active cell stays. |

### Edit Mode Navigation

In edit mode, arrow keys are captured by the editor input (e.g., moving the cursor within a text input). They do **not** move the active cell. Only Tab, Shift+Tab, and Enter cause cell movement from edit mode.

### Tab Navigation Across Rows

Tab moves the active cell sequentially through editable cells. The traversal order is:

1. Left to right through columns in the current row (skipping non-editable columns).
2. Wrap to the first editable column of the next row.
3. After the last cell of the last row, Tab moves focus out of the grid to the next focusable element on the page.

Shift+Tab follows the reverse order.

## Edit Mode and Focus Interaction

| State | Focus location | Keys captured by | Cell movement via |
| --- | --- | --- | --- |
| Not in edit mode | Grid root or active cell | Grid keydown handler | Arrows, Tab, Shift+Tab, Enter |
| Edit mode (text/number) | Editor `<input>` | Input element (arrows move cursor) | Tab, Shift+Tab, Enter, Escape |
| Edit mode (date) | Date picker input | Date picker | Tab, Shift+Tab, Enter, Escape |
| Edit mode (select) | Dropdown `<select>` | Dropdown (arrows change selection) | Tab, Shift+Tab, Enter, Escape |
| Checkbox (no edit mode) | Cell element | Grid keydown handler | Arrows, Tab, Shift+Tab; Space toggles |

## ARIA Roles and Attributes

SunfishDataSheet uses the WAI-ARIA `grid` pattern to expose its structure to assistive technologies.

### Element Roles

| Element | ARIA Role | Attribute | Description |
| --- | --- | --- | --- |
| Root container | `role="grid"` | `aria-label`, `aria-rowcount`, `aria-colcount`, `aria-busy` | The grid landmark. `aria-label` comes from the `AriaLabel` parameter. `aria-busy="true"` when `IsLoading` or `IsSaving`. |
| Table row | `role="row"` | — | Each data row and the header row. |
| Header cell | `role="columnheader"` | `aria-label` | The column title. |
| Data cell | `role="gridcell"` | `aria-readonly`, `aria-invalid`, `title` | See below. |
| Toolbar | `role="toolbar"` | — | The toolbar region containing Add Row, custom actions, etc. |
| Bulk action bar | `role="toolbar"` | — | The bar containing Save All, bulk delete, and selection count. |

### Cell-Level Attributes

| Attribute | When Applied | Value |
| --- | --- | --- |
| `aria-readonly="true"` | Cell is non-editable (`Editable=false`) or computed | Always `"true"` on these cells. |
| `aria-invalid="true"` | Cell is in `CellState.Invalid` | Applied when validation fails. Removed when the cell returns to a valid state. |
| `title` | Cell is in `CellState.Invalid` | Contains the validation error message, providing a tooltip on hover and accessible name for the error. |
| `aria-hidden="true"` | Row is marked for deletion | Applied to deleted rows to hide them from the accessibility tree while they remain visually present (struck through). |

### Live Regions

The DataSheet includes an `aria-live="polite"` region that announces state changes to screen readers:

| Announcement Trigger | Example Text |
| --- | --- |
| Dirty count changes | "3 rows modified" |
| Save All initiated | "Saving changes" |
| Save All completed | "Changes saved successfully" |
| Save All failed (via `IsSaving` returning to `false` with errors) | "Save failed. {N} validation errors." |
| Validation errors appear | "{N} cells have errors" |

## Screen Reader Announcements

The DataSheet is designed to provide meaningful context during editing operations:

* **Cell focus** — when the active cell changes, the screen reader announces the column header and cell value (via the `gridcell` role and column header association).
* **Edit mode entry** — when F2 is pressed, focus moves to the editor input. The input has an associated label (the column title) for screen reader context.
* **Validation errors** — when a cell becomes invalid, `aria-invalid="true"` is set and the `title` attribute contains the error message. Screen readers announce the invalid state on focus.
* **Dirty state** — the `aria-live` region announces the current dirty row count whenever it changes, keeping the user informed without requiring manual navigation.

## Accessibility Checklist

The following checklist summarizes the accessibility guarantees provided by SunfishDataSheet:

* [ ] All interactive cells are reachable via keyboard (Tab, arrows).
* [ ] The grid uses `role="grid"` with `role="row"` and `role="gridcell"` children.
* [ ] Column headers use `role="columnheader"` with descriptive `aria-label`.
* [ ] Non-editable cells have `aria-readonly="true"`.
* [ ] Invalid cells have `aria-invalid="true"` with a descriptive `title`.
* [ ] A visible focus ring distinguishes the active cell.
* [ ] The `aria-live` region announces Save All status and dirty count.
* [ ] `aria-busy="true"` is set during loading and saving states.
* [ ] Deleted rows have `aria-hidden="true"` to prevent screen reader traversal.
* [ ] Ctrl+S, Ctrl+Z, Ctrl+C, Ctrl+V, and other shortcuts are documented and do not conflict with screen reader modes.

>tip Screen readers typically use their own keyboard shortcuts (e.g., NVDA uses Insert+key, JAWS uses specific modes). The DataSheet's grid role signals to screen readers that arrow keys navigate cells, which most screen readers support via their "forms mode" or "application mode."

## See Also

* [DataSheet Overview](slug:datasheet-overview)
* [Selection and Ranges](slug:datasheet-selection-and-ranges)
* [SunfishDataGrid Keyboard Navigation](slug:grid-keyboard-navigation)
