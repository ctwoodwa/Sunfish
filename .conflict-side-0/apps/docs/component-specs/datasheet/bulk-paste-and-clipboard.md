---
title: Bulk Paste and Clipboard
page_title: DataSheet - Bulk Paste and Clipboard
description: TSV copy and paste behavior, type coercion, error handling, and clipboard integration in SunfishDataSheet.
slug: datasheet-bulk-paste-and-clipboard
tags: sunfish,blazor,datasheet,clipboard,paste,copy,tsv
published: True
position: 4
components: ["datasheet"]
---

# DataSheet Bulk Paste and Clipboard

SunfishDataSheet supports copying cell data to the clipboard and pasting tab-separated values (TSV) from external sources such as Excel, Google Sheets, or other DataSheet instances. This enables fast bulk data entry without manually editing each cell. The feature is controlled by the `AllowBulkPaste` parameter (default: `true`).

>caption In this article:

* [Copy Behavior](#copy-behavior)
* [Paste Behavior](#paste-behavior)
* [Paste Mapping Rules](#paste-mapping-rules)
* [Type Coercion on Paste](#type-coercion-on-paste)
* [Paste Error Handling](#paste-error-handling)
* [Worked Example](#worked-example)
* [Limitations](#limitations)

## Copy Behavior

When the user presses **Ctrl+C** with one or more cells selected, the DataSheet copies the selected cell values to the system clipboard as a TSV (tab-separated values) string.

**Copy rules:**

* Each row in the selection becomes one line in the TSV output.
* Cell values within a row are separated by tab characters (`\t`).
* Line breaks between rows use `\n`.
* **Column headers are not included** in the copied data. Only cell values are copied.
* Computed column cells are included in the copy — their displayed (formatted) value is copied.
* If a `Format` delegate is defined on a column, the **raw value** (not the formatted display string) is copied. This ensures that pasting the data back into a DataSheet or Excel produces the correct typed value.
* Empty cells are copied as empty strings.

>caption Copy output for a 2-row, 3-column selection

```
Widget A\t42\t2025-06-15
Widget B\t18\t2025-07-01
```

The JavaScript interop layer reads the active cell's or selected range's values and writes them to the clipboard using the browser's Clipboard API (`navigator.clipboard.writeText`).

## Paste Behavior

When the user presses **Ctrl+V** while a cell is focused, the DataSheet reads TSV data from the clipboard and maps it into the grid starting at the **active cell** (top-left anchor).

**Paste flow:**

1. The JavaScript interop reads the clipboard text via `navigator.clipboard.readText()`.
2. The TSV string is passed to the .NET `PasteFromClipboard(string tsvData)` method.
3. The component parses the TSV into a two-dimensional array of strings (split by `\n` for rows, `\t` for columns).
4. Starting at the active cell, the component maps each parsed value to the corresponding cell in the DataSheet.
5. For each cell, the string value is coerced to the column's expected type.
6. If coercion succeeds, the value is committed via `CommitCellEdit`, triggering validation and dirty tracking.
7. If coercion fails, the cell is marked `CellState.Invalid` with a parse error message.

**Paste is disabled when:**

* `AllowBulkPaste` is `false`.
* No active cell exists (the grid has not been focused).
* The DataSheet is in a saving state (`IsSaving=true`).

## Paste Mapping Rules

Pasted data is mapped to a rectangular region of the DataSheet starting at the active cell. The mapping follows these rules:

| Rule | Behavior |
| --- | --- |
| **Anchor** | The top-left cell of the paste region is the active cell at the time of paste. |
| **Column mapping** | Pasted columns map left-to-right from the anchor column through subsequent columns in display order. |
| **Row mapping** | Pasted rows map top-to-bottom from the anchor row through subsequent rows in the dataset. |
| **Range expansion** | If the pasted data has more columns or rows than the selection, the paste region expands to accommodate the data (up to the grid's column and row boundaries). |
| **Truncation at boundaries** | If pasted data extends beyond the last column or last row of the DataSheet, the excess is silently discarded. No new rows or columns are created. |
| **Read-only columns** | Computed columns and columns with `Editable="false"` are **skipped** during paste. The paste cursor advances past them, and the next pasted value maps to the next editable column. |
| **Deleted rows** | Rows marked for deletion are skipped during paste. |

## Type Coercion on Paste

Each pasted string value is coerced to the target column's expected type. The following table describes the coercion rules per `DataSheetColumnType`:

| ColumnType | Coercion Rule | Failure Behavior |
| --- | --- | --- |
| `Text` | Value used as-is. | Never fails. |
| `Number` | `decimal.TryParse` with invariant culture. Result is type-converted to the property's actual numeric type (`int`, `double`, etc.). | Cell marked `CellState.Invalid` with message "Invalid number". |
| `Date` | `DateTime.TryParse` with InvariantCulture (matches the invariant round-trip used by `data-raw-value`). | Cell marked `CellState.Invalid` with message "Invalid date". |
| `Select` | Value matched against `Options.Value` (case-sensitive). | Cell marked `CellState.Invalid` with message "Value not in options". |
| `Checkbox` | `"true"` or `"1"` (case-insensitive) → `true`. All other values → `false`. | Never fails (defaults to `false`). |
| `Computed` | Skipped entirely. | Not applicable. |

After coercion, the column's `Validate` delegate runs (if defined), and `Required` validation applies. These may produce additional `CellState.Invalid` entries beyond type coercion failures.

## Paste Error Handling

When paste encounters errors, the DataSheet uses a **best-effort** approach:

* Each cell in the paste region is processed independently. A failure in one cell does not abort the paste for other cells.
* Cells that fail type coercion are marked `CellState.Invalid` with an appropriate error message. The raw pasted string is **not** written to the model — the cell retains its previous value.
* Cells that pass coercion but fail validation (required or custom `Validate`) are written to the model but marked `CellState.Invalid`.
* After the paste completes, the user can see which cells failed (highlighted via invalid cell styling) and correct them manually.
* The `OnRowChanged` event fires once for each successfully committed cell during paste, not once for the entire paste operation.

>important Large paste operations may generate many `OnRowChanged` events. If your handler performs expensive work (e.g., server calls), consider debouncing or deferring until Save All.

## Worked Example

Consider a DataSheet with four columns in display order:

| Column | Field | ColumnType | Editable |
| --- | --- | --- | --- |
| 1 | `ProductName` | Text | Yes |
| 2 | `Category` | Select | Yes |
| 3 | `Margin` | Computed | No |
| 4 | `Price` | Number | Yes |

The user copies a 3×2 range from Excel:

```
Gadget X\tElectronics\t25.99
Gadget Y\tClothing\t18.50
Gadget Z\tFood\t12.00
```

The user clicks on the `ProductName` cell of row 5 (making it the active cell) and presses Ctrl+V.

**What happens:**

1. **Row 5, Column 1 (ProductName, Text):** "Gadget X" is written as-is. Cell becomes dirty.
2. **Row 5, Column 2 (Category, Select):** "Electronics" is matched against `Options`. If "Electronics" is a valid option value, it is committed. Otherwise, the cell is marked invalid.
3. **Row 5, Column 3 (Margin, Computed):** Skipped (read-only). The paste cursor advances.
4. **Row 5, Column 4 (Price, Number):** "25.99" is parsed via `decimal.TryParse` → `25.99m`. Committed and marked dirty.
5. **Row 6, Column 1:** "Gadget Y" → committed.
6. **Row 6, Column 2:** "Clothing" → matched against options.
7. **Row 6, Column 3:** Skipped.
8. **Row 6, Column 4:** "18.50" → parsed and committed.
9. **Row 7:** Same pattern for "Gadget Z", "Food", "12.00".

After paste, the user sees 9 cells modified (3 rows × 3 editable columns), with any option-matching failures highlighted as invalid.

## Limitations

* **No new row creation on paste** — if the pasted data has more rows than available in the DataSheet below the anchor, the excess rows are discarded. Use `AllowAddRow` and add rows before pasting if you need to accommodate additional data.
* **No column header mapping** — the paste operation maps columns by position, not by header name. The user must ensure the source data's column order matches the DataSheet's column order from the anchor point.
* **Clipboard API requirement** — paste relies on `navigator.clipboard.readText()`, which requires the page to have clipboard permissions. In some browsers, this requires a user gesture (the Ctrl+V keypress satisfies this). If clipboard access is denied, the paste silently fails.
* **TSV format only** — the component expects tab-separated values. CSV (comma-separated) data is not automatically parsed. If users paste CSV data, the entire row may be interpreted as a single cell value.
* **Performance with large pastes** — pasting thousands of cells triggers individual `CommitCellEdit` calls and re-renders. For very large paste operations (1,000+ cells), consider disabling `OnRowChanged` during the paste or using `SetDataAsync` to replace the entire dataset.
* **No undo for bulk paste** — Ctrl+Z after a paste undoes only the last individual cell change, not the entire paste operation. There is no batch undo.

## See Also

* [DataSheet Overview](slug:datasheet-overview)
* [Selection and Ranges](slug:datasheet-selection-and-ranges)
* [Editing and Validation](slug:datasheet-editing-and-validation)
