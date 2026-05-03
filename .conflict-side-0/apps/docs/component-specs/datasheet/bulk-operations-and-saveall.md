---
title: Bulk Operations and Save All
page_title: DataSheet - Bulk Operations and Save All
description: Save All contract, dirty and deleted row payloads, undo, reset, retry guidance, and bulk operation lifecycle in SunfishDataSheet.
slug: datasheet-bulk-operations-and-saveall
tags: sunfish,blazor,datasheet,saveall,bulk,undo,reset
published: True
position: 5
components: ["datasheet"]
---

# DataSheet Bulk Operations and Save All

SunfishDataSheet is built around a **batch commit model**: users make multiple edits across rows and columns, then persist all changes at once via Save All. This article defines the Save All contract, the undo and reset mechanisms, and guidance for handling failures.

>caption In this article:

* [Save All Contract](#save-all-contract)
* [Save All Flow](#save-all-flow)
* [DataSheetSaveArgs Payload](#datasheetsaveargs-payload)
* [IsSaving and Cell State Transitions](#issaving-and-cell-state-transitions)
* [Add Row](#add-row)
* [Delete Row](#delete-row)
* [Undo (Ctrl+Z)](#undo-ctrlz)
* [Reset](#reset)
* [Retry and Error Handling Guidance](#retry-and-error-handling-guidance)

## Save All Contract

The Save All operation is the primary mechanism for persisting changes made in the DataSheet. The component guarantees the following:

* **Only dirty and deleted rows are included.** Rows that have not been modified are never sent to `OnSaveAll`.
* **Row order is preserved.** `DirtyRows` and `DeletedRows` maintain the same order as the rows appear in the DataSheet (top to bottom).
* **Validation gates the save.** Save All is blocked if any cell is in `CellState.Invalid`. The component runs `ValidateAllAsync()` and `OnValidate` before firing `OnSaveAll`. See [Editing and Validation](slug:datasheet-editing-and-validation#pre-save-validation-onvalidate).
* **The component does not persist data.** `OnSaveAll` provides the dirty and deleted rows; the caller is responsible for writing them to a database, API, or other storage. The component has no knowledge of the persistence layer.
* **Idempotency is the caller's responsibility.** The component does not deduplicate or track whether a particular `OnSaveAll` invocation has already been processed. If the user triggers Save All twice (e.g., due to a slow network), both invocations fire with the current dirty state. The caller should implement idempotency guards if needed.

## Save All Flow

The following steps execute in order when Save All is triggered (via Ctrl+S, the Save All button, or `SaveAllAsync()`):

1. **Validation pass** — the component calls `ValidateAllAsync()`, which iterates all dirty rows and runs required + column-level `Validate` delegates on each dirty field.
2. **Abort on cell errors** — if any cell is `CellState.Invalid` after step 1, Save All is aborted. The invalid cells are highlighted and the user must fix them.
3. **OnValidate event** — fires with `DataSheetValidateArgs<TItem>` containing the dirty rows and an empty `Errors` list. The handler may append errors.
4. **Abort on OnValidate errors** — if the `Errors` list is non-empty after the handler returns, Save All is aborted. Each error is mapped to its cell and the cell transitions to `CellState.Invalid`.
5. **Cell state transition to Saving** — all dirty cells transition to `CellState.Saving`. The CSS provider applies saving-specific styles.
6. **OnSaveAll fires** — the component fires `OnSaveAll` with `DataSheetSaveArgs<TItem>` containing `DirtyRows` and `DeletedRows`.
7. **Caller persists** — the caller sets `IsSaving=true` before awaiting the persistence operation, then sets `IsSaving=false` after completion.
8. **Post-save state** — on success, dirty cells transition to `CellState.Saved` (brief visual indicator), then to `CellState.Pristine`. The saved values become the new original snapshots. Deleted rows are removed from the dataset.

>caption Handling Save All

````RAZOR
<SunfishDataSheet TItem="Invoice" Data="@_invoices" KeyField="Id"
                 OnSaveAll="@HandleSave" IsSaving="@_saving">
    @* columns *@
</SunfishDataSheet>

@code {
    bool _saving;

    async Task HandleSave(DataSheetSaveArgs<Invoice> args)
    {
        _saving = true;
        StateHasChanged();

        try
        {
            await InvoiceService.BulkUpdateAsync(args.DirtyRows);
            await InvoiceService.BulkDeleteAsync(args.DeletedRows);
        }
        finally
        {
            _saving = false;
        }
    }
}
````

## DataSheetSaveArgs Payload

| Property | Type | Description |
| --- | --- | --- |
| `DirtyRows` | `IReadOnlyList<TItem>` | Rows with at least one modified field. Each item is the live `TItem` instance with the current (edited) values. |
| `DeletedRows` | `IReadOnlyList<TItem>` | Rows the user has marked for deletion. These rows are still in the dataset until Save All completes; they are removed after the handler returns. |

**Payload details:**

* `DirtyRows` contains the actual `TItem` instances, not clones. The caller can read any property on the row, including fields that were not modified.
* A row that is both dirty and deleted appears **only** in `DeletedRows`, not in `DirtyRows`. The component assumes that deleting a row supersedes any pending edits.
* After a successful save, rows in `DirtyRows` have their original snapshots updated to the current values. Rows in `DeletedRows` are removed from the internal data list.

## IsSaving and Cell State Transitions

The `IsSaving` parameter is a one-way bound boolean that the caller controls. It affects the component as follows:

| `IsSaving` Value | Component Behavior |
| --- | --- |
| `true` | Save All button is disabled. Dirty cells display `CellState.Saving` styling. Editing is still possible but Save All cannot be re-triggered. |
| `false` | Normal operation. If cells were in `CellState.Saving`, they transition to `CellState.Saved` (briefly), then `CellState.Pristine`. |

**Cell state transitions during save:**

```
CellState.Dirty → CellState.Saving    (Save All initiated)
CellState.Saving → CellState.Saved    (IsSaving set to false)
CellState.Saved → CellState.Pristine  (after brief visual indicator)
```

The `Saved` state provides a visual confirmation (e.g., green highlight) that lasts for a short period before the cell reverts to `Pristine`. The exact duration is controlled by CSS transitions in the theme.

## Add Row

When `AllowAddRow` is `true`, the DataSheet toolbar includes a "+ Add Row" button. Clicking it:

1. Creates a new `TItem` instance using the parameterless constructor.
2. Appends the row to the end of the internal data list.
3. The new row is immediately considered **dirty** (all fields differ from a "no original" baseline).
4. The active cell moves to the first editable column of the new row.
5. The new row appears in `DataSheetSaveArgs.DirtyRows` when Save All is triggered.

>important The `TItem` type must have a public parameterless constructor for Add Row to work. If it does not, the add operation will throw an exception.

## Delete Row

When `AllowDeleteRow` is `true`:

* Each row displays a delete button (or checkbox).
* Clicking the delete button marks the row as **deleted** — it is visually struck through (styled via `DataSheetRowClass` with `isDeleted=true`) and excluded from editing.
* Deleted rows remain visible in the DataSheet until Save All completes. This allows the user to review deletions before committing.
* The bulk action bar shows a "Delete Selected" button when rows are selected, allowing multi-row deletion.
* Deleted rows appear in `DataSheetSaveArgs.DeletedRows`.
* A deleted row can be **undeleted** before Save All by clicking the delete button again (toggle behavior).
* After Save All, deleted rows are removed from the dataset and are no longer visible.

## Undo (Ctrl+Z)

The DataSheet supports **per-cell undo** via Ctrl+Z. Undo restores the most recent pre-edit value for the active cell.

**Undo behavior:**

* Ctrl+Z reverts the active cell to the value it had before the last commit. This is a single-level undo — pressing Ctrl+Z again has no additional effect (there is no multi-level undo stack).
* The undo buffer stores one value per cell: the value immediately before the most recent `CommitCellEdit`.
* If the active cell has not been edited, Ctrl+Z has no effect.
* Undo triggers the same validation and dirty tracking flow as a normal commit — the restored value is written, validation runs, and dirty state is recalculated.
* Undo does not affect other cells. There is no "undo all" shortcut; use `ResetAsync()` for that.

**Undo after paste:**

* After a bulk paste operation, Ctrl+Z undoes only the **last individual cell** that was committed during the paste, not the entire paste operation.

## Reset

The `ResetAsync()` method discards all dirty state and restores every row to its original snapshot values.

**Reset guarantees:**

* Every modified cell is reverted to the value captured when `Data` was bound or `SetDataAsync` was last called.
* All `CellState.Dirty` and `CellState.Invalid` cells transition to `CellState.Pristine`.
* Deleted rows are undeleted (restored to the dataset).
* Added rows (created via Add Row but not yet saved) are removed.
* The undo buffer is cleared.
* `OnRowChanged` does **not** fire during reset — the reset is a bulk state operation, not a sequence of individual commits.

>caption Reset via component reference

````RAZOR
<SunfishDataSheet @ref="_sheet" TItem="Product" Data="@_products" KeyField="Id"
                 OnSaveAll="@HandleSave">
    @* columns *@
</SunfishDataSheet>

<button @onclick="() => _sheet.ResetAsync()">Discard Changes</button>

@code {
    SunfishDataSheet<Product> _sheet = default!;
}
````

## Retry and Error Handling Guidance

When `OnSaveAll` fails (e.g., a server error), the component does not automatically retry or revert. The caller controls the recovery flow:

**Recommended patterns:**

1. **Leave dirty state intact.** If the save fails, do not call `ResetAsync()`. The dirty rows remain dirty, and the user can attempt Save All again after resolving the issue.

2. **Show an error banner.** Use `ToolbarTemplate` or an external component to display the error. The DataSheet does not have a built-in error notification mechanism for save failures.

3. **Set `IsSaving=false` in the `finally` block.** This ensures the component returns to an editable state even if the save throws an exception.

4. **Handle partial failures.** If the backend persists some rows but fails on others, the caller can:
   * Call `SetDataAsync` with the successfully saved rows to update the original snapshots for those rows.
   * Leave the failed rows dirty so the user can retry.

5. **Allow a second Save All.** Since dirty state is preserved after a failed save, the user can fix data, make additional edits, or simply click Save All again. The next `OnSaveAll` will include all currently dirty rows (including those from the failed attempt).

>caption Error handling pattern

````RAZOR
@code {
    bool _saving;
    string? _saveError;

    async Task HandleSave(DataSheetSaveArgs<Product> args)
    {
        _saving = true;
        _saveError = null;
        StateHasChanged();

        try
        {
            await ProductService.BulkSaveAsync(args.DirtyRows, args.DeletedRows);
        }
        catch (Exception ex)
        {
            _saveError = $"Save failed: {ex.Message}. Please try again.";
        }
        finally
        {
            _saving = false;
        }
    }
}
````

## See Also

* [DataSheet Overview](slug:datasheet-overview)
* [Editing and Validation](slug:datasheet-editing-and-validation)
* [Keyboard and Accessibility](slug:datasheet-keyboard-and-accessibility)
