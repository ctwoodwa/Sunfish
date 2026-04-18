---
title: Editing and Validation
page_title: DataSheet - Editing and Validation
description: Editing lifecycle, per-cell and per-row validation, dirty tracking, and the Save All validation flow in SunfishDataSheet.
slug: datasheet-editing-and-validation
tags: sunfish,blazor,datasheet,editing,validation,dirty-tracking
published: True
position: 2
components: ["datasheet"]
---

# DataSheet Editing and Validation

SunfishDataSheet provides an in-place editing model where every editable cell can be modified directly. The component tracks changes at the cell level, runs validation after each commit, and gates Save All behind a full validation pass. This article describes the complete editing lifecycle, validation rules, dirty state management, and error handling.

>caption In this article:

* [Editing Lifecycle](#editing-lifecycle)
* [Entering Edit Mode](#entering-edit-mode)
* [Committing and Cancelling](#committing-and-cancelling)
* [Dirty Tracking](#dirty-tracking)
* [Per-Cell Validation](#per-cell-validation)
* [Pre-Save Validation (OnValidate)](#pre-save-validation-onvalidate)
* [Validation Error Display](#validation-error-display)
* [Editing Lifecycle Sequence](#editing-lifecycle-sequence)
* [Programmatic Editing](#programmatic-editing)

## Editing Lifecycle

The DataSheet editing model follows this sequence for every cell edit:

1. **Focus** — the user navigates to a cell via click, Tab, Enter, or arrow keys. The cell becomes the active cell (highlighted with a focus ring).
2. **Enter edit mode** — the user presses F2, starts typing, or double-clicks. The cell transitions from display mode to an inline editor (text input, date picker, dropdown, etc.).
3. **Edit** — the user modifies the value in the editor.
4. **Commit or cancel:**
   * **Commit** (Enter, Tab) — the new value is written to the row, per-cell validation runs, dirty state updates, and `OnRowChanged` fires.
   * **Cancel** (Escape) — the editor closes, the cell reverts to the value it had before entering edit mode, and no events fire.
5. **Post-commit state** — the cell transitions to `CellState.Dirty` if the committed value differs from the original, or remains `CellState.Pristine` if the user committed the same value. If validation fails, the cell transitions to `CellState.Invalid`.

Checkbox columns are an exception: toggling a checkbox commits the value immediately without a separate edit mode step.

## Entering Edit Mode

A cell enters edit mode through any of the following actions:

| Action | Behavior |
| --- | --- |
| Press **F2** | Opens the editor with the current value. For text columns, the full value is selected. |
| Press any **printable character** | Opens the editor and replaces the current value with the typed character (text and number columns only). |
| **Click** on a focused cell | Opens the editor at the click position. |
| **Double-click** on any cell | Focuses the cell and opens the editor. |
| Call `EnterEditMode(row, field)` | Programmatically opens the editor on the specified cell. |

Cells that are not editable (`Editable="false"` or `ColumnType="Computed"`) ignore all edit mode triggers. Focus still moves to these cells during navigation, but no editor appears.

## Committing and Cancelling

### Commit Triggers

| Trigger | Effect after commit |
| --- | --- |
| **Enter** | Commits and moves focus down to the same column in the next row. |
| **Tab** | Commits and moves focus right to the next editable cell (wraps to the next row at the end). |
| **Shift+Tab** | Commits and moves focus left to the previous editable cell (wraps to the previous row at the start). |
| **Click** outside the editing cell | Commits the current cell. Focus moves to the clicked cell. |
| Call `CommitCellEdit(row, field, value)` | Programmatic commit. Does not move focus. |

### Cancel Trigger

| Trigger | Effect |
| --- | --- |
| **Escape** | Restores the cell to the value it had before entering edit mode. The cell exits edit mode. No events fire. |

### What Happens on Commit

When a cell value is committed, the following steps execute in order:

1. The new value is type-converted (if needed) and written to the `TItem` property via reflection.
2. **Required validation** runs if the column has `Required="true"`.
3. **Column-level validation** runs if the column has a `Validate` delegate.
4. If validation passes, the cell's dirty state is updated by comparing the committed value against the original snapshot.
5. `OnRowChanged` fires with the row, field, old value, and new value.
6. The component re-renders the affected cell (and any computed columns in the same row that may display updated derived values).

If validation fails at step 2 or 3, the cell transitions to `CellState.Invalid`, the error message is stored, and `OnRowChanged` still fires (the value was written to the model). The Save All button remains disabled as long as any cell is invalid.

## Dirty Tracking

The DataSheet maintains an internal dirty tracking system that snapshots the original value of each row when the data is first loaded (or after a reset/save).

**How dirty state works:**

* When `Data` is bound or `SetDataAsync` is called, the component deep-clones each row to create an original snapshot.
* After each cell commit, the new value is compared against the original snapshot for that field.
* If the values differ, the field is added to the row's dirty field set and the cell renders as `CellState.Dirty`.
* If the user edits a cell back to its original value, the field is removed from the dirty set. If no fields remain dirty, the row is no longer considered dirty.
* `GetDirtyRows()` returns only rows that have at least one dirty field.
* `ResetAsync()` discards all dirty state and restores every row to its original snapshot values.

**Row-level dirty tracking:**

* A row is dirty if it has at least one dirty field **or** if it has been marked for deletion.
* Deleted rows are tracked separately and appear in `DataSheetSaveArgs.DeletedRows`.
* The CSS provider receives `isDirty` and `isDeleted` flags for row-level styling.

**Cell state transitions:**

| From | To | Trigger |
| --- | --- | --- |
| `Pristine` | `Dirty` | Cell committed with a value different from the original. |
| `Pristine` | `Invalid` | Cell committed but validation failed. |
| `Dirty` | `Pristine` | Cell edited back to the original value. |
| `Dirty` | `Invalid` | Cell re-committed but validation failed. |
| `Invalid` | `Dirty` | Cell re-committed with a valid value that differs from the original. |
| `Invalid` | `Pristine` | Cell re-committed with the original value (and validation passes). |
| `Dirty` | `Saving` | Save All initiated; row is part of the save batch. |
| `Saving` | `Saved` | `OnSaveAll` handler completes without error. |
| `Saved` | `Pristine` | Brief visual indicator period ends; the saved snapshot becomes the new original. |

## Per-Cell Validation

Per-cell validation runs automatically after every cell commit. Two validation layers execute in order:

### 1. Required Validation

If the column has `Required="true"`, the component checks whether the committed value is considered "empty" for the column type (see the [Required Fields table](slug:datasheet-columns-and-schema#required-fields)). If empty, the cell is marked `CellState.Invalid` with the message `"{Title} is required."`.

### 2. Column Validate Delegate

If the column has a `Validate` delegate, it is invoked with the full row instance. The delegate returns `null` if valid or an error message string if invalid.

```csharp
// Example: cross-field validation on a column
Validate="@(row => row.EndDate < row.StartDate
    ? "End date must be after start date"
    : null)"
```

If the delegate returns an error, the cell is marked `CellState.Invalid` with the returned message. The required check is evaluated first; if it fails, the custom validate delegate is not invoked. Only one error message is produced.

## Pre-Save Validation (OnValidate)

The `OnValidate` event fires as part of the Save All flow, **after** all per-cell validation has passed. It provides an opportunity for the caller to perform cross-row, server-side, or business-rule validation that individual column validators cannot express.

**Contract:**

1. Save All is invoked (via Ctrl+S, the Save All button, or `SaveAllAsync()`).
2. The component runs `ValidateAllAsync()` — iterating all dirty rows and executing required + column-level validation. If any cell is invalid, Save All is aborted and the invalid cells are highlighted.
3. If all cells pass, `OnValidate` fires with a `DataSheetValidateArgs<TItem>` containing the dirty rows and an empty `Errors` list.
4. The handler inspects the dirty rows and appends `DataSheetValidationError<TItem>` entries to the `Errors` list for any failures.
5. After the handler returns, if `Errors` is non-empty, Save All is aborted. Each error is mapped to its cell (`Row` + `Field`), and those cells transition to `CellState.Invalid` with the error `Message`.
6. If `Errors` is empty, `OnSaveAll` fires with the `DataSheetSaveArgs<TItem>`.

>caption OnValidate handler example

````RAZOR
<SunfishDataSheet TItem="BudgetLine" Data="@_lines" KeyField="Id"
                 OnValidate="@ValidateBudget"
                 OnSaveAll="@SaveBudget">
    @* columns omitted for brevity *@
</SunfishDataSheet>

@code {
    void ValidateBudget(DataSheetValidateArgs<BudgetLine> args)
    {
        decimal totalBudget = args.DirtyRows.Sum(r => r.Amount);
        if (totalBudget > 1_000_000m)
        {
            // Mark the Amount cell on the last row as the error location
            var lastRow = args.DirtyRows.Last();
            args.Errors.Add(new DataSheetValidationError<BudgetLine>
            {
                Row = lastRow,
                Field = "Amount",
                Message = $"Total budget ({totalBudget:C}) exceeds the $1M limit."
            });
        }
    }
}
````

>important `OnValidate` is synchronous by design. If you need to call an async service (e.g., server-side uniqueness check), perform the check before Save All by calling `ValidateAllAsync()` manually and adding errors to a shared state, or use `OnSaveAll` to reject the save by re-setting `IsSaving` to `false` and displaying an error banner.

## Validation Error Display

When a cell is in `CellState.Invalid`:

* The cell receives the CSS class from `DataSheetCellClass(CellState.Invalid, ...)`, typically rendering a red border or background.
* The cell's `title` attribute contains the error message, providing a tooltip on hover.
* The cell has `aria-invalid="true"` for screen readers.
* The error message is also available in `DataSheetCellContext.ValidationError` for use in custom `CellTemplate` rendering.

The Save All button (or Ctrl+S) is disabled as long as any cell in the sheet is in `CellState.Invalid`. The dirty count indicator in the save footer shows the number of dirty rows. The dirty count includes all rows with uncommitted changes, including those that are both dirty and invalid.

## Editing Lifecycle Sequence

The following prose sequence describes the full editing and save flow from a user's perspective:

1. The user clicks on a cell displaying "100.00" in a Number column. The cell receives focus (active state).
2. The user presses F2. The cell enters edit mode, showing a numeric input with "100" selected.
3. The user types "250" and presses Tab.
4. The component writes `250` to the row's property. Required validation passes (non-null). The column `Validate` delegate runs and returns `null` (valid). The original snapshot value was `100`, so the cell transitions to `CellState.Dirty`. `OnRowChanged` fires with `OldValue=100, NewValue=250`. Focus moves to the next editable cell.
5. The user edits two more cells in other rows, then presses Ctrl+S.
6. The component runs `ValidateAllAsync()`: it iterates all three dirty rows, re-running required and column validation on each dirty field. All pass.
7. `OnValidate` fires. The handler checks business rules and adds no errors.
8. All dirty cells transition to `CellState.Saving`. `OnSaveAll` fires with `DirtyRows` containing the three modified rows.
9. The caller sets `IsSaving=true`, persists changes to the backend, then sets `IsSaving=false`.
10. The component transitions saved cells to `CellState.Saved` briefly, then to `CellState.Pristine`. The saved values become the new original snapshots.

## Programmatic Editing

The following public methods allow programmatic control over editing:

| Method | Returns | Description |
| --- | --- | --- |
| `EnterEditMode(TItem row, string field)` | `void` | Opens the editor on a specific cell. The cell must be editable. |
| `CommitCellEdit(TItem row, string field, object? value)` | `Task` | Writes a value to a cell, runs validation, and updates dirty state. Does not require the cell to be in edit mode. |
| `IsCellEditing(TItem row, string field)` | `bool` | Returns `true` if the specified cell is currently in edit mode. |
| `ValidateAllAsync()` | `Task<bool>` | Runs full validation across all dirty rows without triggering Save All. Returns `true` if all cells are valid. |

These methods are available via the component `@ref` and are useful for scenarios where editing must be orchestrated from outside the DataSheet (e.g., a toolbar button that applies a value to all selected cells).

## See Also

* [DataSheet Overview](slug:datasheet-overview)
* [Columns and Schema](slug:datasheet-columns-and-schema)
* [Bulk Operations and Save All](slug:datasheet-bulk-operations-and-saveall)
