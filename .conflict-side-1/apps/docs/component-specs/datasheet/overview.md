---
title: Overview
page_title: DataSheet Overview
description: A typed, schema-driven data sheet for bulk editing rows of strongly typed records with validation and Save All commit.
slug: datasheet-overview
tags: sunfish,blazor,datasheet,overview,bulk-edit
published: True
position: 0
components: ["datasheet"]
---

# SunfishDataSheet Overview

The SunfishDataSheet component is a **typed, schema-driven data sheet** for bulk editing application data. It renders rows of strongly typed `TItem` records in an editable grid layout, with per-cell editors defined by column schema, local dirty tracking, validation, and a bulk Save All commit flow.

SunfishDataSheet is designed for **line-of-business workflows** where each row is a domain entity (order line, invoice row, configuration item) and users need to edit many rows before persisting changes in a single batch.

>caption In this article:

* [DataSheet vs DataGrid vs Spreadsheet](#datasheet-vs-datagrid-vs-spreadsheet)
* [Basic Usage](#basic-usage)
* [Parameters](#parameters)
* [Column Parameters](#column-parameters)
* [Events](#events)
* [Enums](#enums)
* [CSS Provider Methods](#css-provider-methods)
* [Public Methods](#public-methods)
* [Feature Areas](#feature-areas)

## DataSheet vs DataGrid vs Spreadsheet

SunfishDataSheet sits between SunfishDataGrid and a full Spreadsheet component. The following table clarifies the boundaries:

| Capability | SunfishDataSheet | SunfishDataGrid | Spreadsheet |
| --- | --- | --- | --- |
| Data model | Typed `TItem` rows | Typed `TItem` rows | Arbitrary cell arrays / worksheets |
| Primary purpose | Bulk edit and commit | Display, CRUD, sorting, filtering | Workbook with formulas |
| Editing pattern | All cells editable in-place; Save All commits batch | Row or cell editing with per-row save | Direct cell editing with formula engine |
| Dirty tracking | Built-in per-cell and per-row | Not built-in | Sheet-level undo history |
| Validation | Per-cell, per-row, and pre-save | DataAnnotations or custom per-row | Cell-level formula errors |
| Column schema | `SunfishDataSheetColumn` with typed editors | `GridColumn` with display and edit templates | Column/row headers (A, B, C / 1, 2, 3) |
| Clipboard | TSV copy/paste mapped to typed columns | Not built-in | Full Excel-style clipboard |
| Formulas | Not supported | Not applicable | `=SUM(A1:A10)` and similar |
| Grouping / sorting / filtering | Not supported | Full support | Limited |

SunfishDataSheet is **not** a general-purpose spreadsheet engine. It does not manage workbooks, worksheets, or arbitrary cell formulas like `=SUM(A1:A10)`. For Excel-style scenarios, use the Spreadsheet component instead.

## Basic Usage

1. Use the `SunfishDataSheet` tag with a `TItem` type parameter.
2. Bind the `Data` parameter to an `IEnumerable<TItem>` property.
3. Set `KeyField` to the property that uniquely identifies each row.
4. Add `SunfishDataSheetColumn` instances as child content, each mapping a `Field` to a typed editor via `ColumnType`.
5. Handle `OnSaveAll` to persist dirty and deleted rows.

>caption Get started with SunfishDataSheet

````RAZOR
<SunfishDataSheet TItem="OrderLine" Data="@_lines" KeyField="Id"
                 OnSaveAll="@HandleSaveAll"
                 AllowAddRow="true"
                 AllowDeleteRow="true"
                 Height="500px">
    <SunfishDataSheetColumn TItem="OrderLine" Field="ProductName" Title="Product"
        ColumnType="DataSheetColumnType.Text" Editable="true" Required="true" />
    <SunfishDataSheetColumn TItem="OrderLine" Field="Quantity" Title="Qty"
        ColumnType="DataSheetColumnType.Number" Editable="true" />
    <SunfishDataSheetColumn TItem="OrderLine" Field="UnitPrice" Title="Price"
        ColumnType="DataSheetColumnType.Number" Editable="true"
        Format="@(r => r.UnitPrice.ToString("C2"))" />
    <SunfishDataSheetColumn TItem="OrderLine" Field="ShipDate" Title="Ship Date"
        ColumnType="DataSheetColumnType.Date" Editable="true" />
    <SunfishDataSheetColumn TItem="OrderLine" Field="Status" Title="Status"
        ColumnType="DataSheetColumnType.Select" Options="@_statusOptions" />
    <SunfishDataSheetColumn TItem="OrderLine" Field="IsActive" Title="Active"
        ColumnType="DataSheetColumnType.Checkbox" />
    <SunfishDataSheetColumn TItem="OrderLine" Field="LineTotal" Title="Total"
        ColumnType="DataSheetColumnType.Computed" Editable="false"
        Format="@(r => (r.Quantity * r.UnitPrice).ToString("C2"))" />
</SunfishDataSheet>

@code {
    List<OrderLine> _lines = new();

    List<DataSheetSelectOption> _statusOptions = new()
    {
        new() { Value = "Pending", Label = "Pending" },
        new() { Value = "Shipped", Label = "Shipped" },
        new() { Value = "Delivered", Label = "Delivered" }
    };

    async Task HandleSaveAll(DataSheetSaveArgs<OrderLine> args)
    {
        await OrderService.BulkUpdateAsync(args.DirtyRows);
        await OrderService.BulkDeleteAsync(args.DeletedRows);
    }
}
````

## Parameters

The following table lists all SunfishDataSheet component-level parameters.

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `Data` | `IEnumerable<TItem>?` | `null` | Strongly typed row dataset. Each row is a domain entity, not an arbitrary cell array. |
| `KeyField` | `string` | `"Id"` | Property name used as the unique row key for dirty tracking, scroll, and focus APIs. |
| `OnSaveAll` | `EventCallback<DataSheetSaveArgs<TItem>>` | — | Fires with dirty and deleted rows when Save All is invoked. Use this to persist changes in bulk. |
| `OnRowChanged` | `EventCallback<DataSheetRowChangedArgs<TItem>>` | — | Fires after each individual cell commit, providing the row, field, old value, and new value. |
| `OnValidate` | `EventCallback<DataSheetValidateArgs<TItem>>` | — | Fires before Save All. The handler can append cross-row or cross-field validation errors. |
| `IsSaving` | `bool` | `false` | When `true`, displays a saving indicator and disables the Save All button. Set this while persistence is in progress. |
| `AllowAddRow` | `bool` | `false` | When `true`, shows a "+ Add Row" button in the toolbar. |
| `AllowDeleteRow` | `bool` | `false` | When `true`, enables per-row delete buttons and bulk delete in the bulk action bar. |
| `AllowBulkPaste` | `bool` | `true` | When `true`, enables Ctrl+V TSV paste into a typed cell range. |
| `EmptyStateMessage` | `string` | `"No data."` | Message displayed when `Data` is null or contains no items. |
| `Height` | `string?` | `null` | Fixed container height (CSS value). Enables vertical scrolling with a sticky header and bulk bar. |
| `IsLoading` | `bool` | `false` | When `true`, shows skeleton placeholder rows while data is loading. |
| `EnableVirtualization` | `bool` | `true` | When `true`, uses Blazor `Virtualize` for row rendering to improve performance with large datasets. |
| `AriaLabel` | `string` | `"Editable data grid"` | Accessible label applied to the `role="grid"` root element. |
| `ChildContent` | `RenderFragment?` | — | Column definitions via `SunfishDataSheetColumn` child components. |
| `ToolbarTemplate` | `RenderFragment?` | — | Additional toolbar content rendered alongside built-in toolbar actions (filters, custom buttons, etc.). |
| `Class` | `string?` | `null` | Inherited from `SunfishComponentBase`. Pass via HTML attribute syntax (`class="..."`). Not an explicit `[Parameter]` on `SunfishDataSheet<TItem>`; applied through `AdditionalAttributes`. |
| `Style` | `string?` | `null` | Inherited from `SunfishComponentBase`. Pass via HTML attribute syntax (`style="..."`). Not an explicit `[Parameter]` on `SunfishDataSheet<TItem>`; applied through `AdditionalAttributes`. |

## Column Parameters

Each `SunfishDataSheetColumn` maps a property on `TItem` to a typed in-cell editor with validation and formatting. See [Columns and Schema](slug:datasheet-columns-and-schema) for detailed behavior per column type.

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `Field` | `string` | required | Bound property name on `TItem`. Case-sensitive. |
| `Title` | `string?` | Field value | Header label displayed in the column header. Falls back to `Field` if not set. |
| `ColumnType` | `DataSheetColumnType` | `Text` | Editor type for the column. Determines the in-cell editing widget and value parsing. |
| `Editable` | `bool` | `true` | Whether users can edit cells in this column. Set to `false` for display-only columns. |
| `Required` | `bool` | `false` | When `true`, Save All is blocked if any cell in this column is empty or null. |
| `MinWidth` | `int?` | `null` | Minimum column width in pixels. |
| `Width` | `string?` | `null` | Column width as a CSS value (e.g., `"120px"`, `"10rem"`). |
| `Format` | `Func<TItem, string?>?` | `null` | Display formatter for read mode and computed columns. Receives the row and returns a display string. |
| `Validate` | `Func<TItem, string?>?` | `null` | Per-cell validator. Return `null` for valid; return an error message string for invalid. |
| `Options` | `IEnumerable<DataSheetSelectOption>?` | `null` | Value/label pairs for `Select` type columns. |
| `CellTemplate` | `RenderFragment<DataSheetCellContext<TItem>>?` | `null` | Custom cell rendering for advanced scenarios. Receives a `DataSheetCellContext<TItem>` with the item, field, value, editing state, and validation error. |

## Events

### DataSheetSaveArgs\<TItem\>

Provided to the `OnSaveAll` handler when the user invokes Save All.

| Property | Type | Description |
| --- | --- | --- |
| `DirtyRows` | `IReadOnlyList<TItem>` | Rows with at least one modified field, ready to be persisted. |
| `DeletedRows` | `IReadOnlyList<TItem>` | Rows the user has marked for deletion. |

### DataSheetRowChangedArgs\<TItem\>

Provided to the `OnRowChanged` handler after each individual cell commit.

| Property | Type | Description |
| --- | --- | --- |
| `Row` | `TItem` | The row containing the edited cell. |
| `Field` | `string` | Property name of the field that changed. |
| `OldValue` | `object?` | The value before the edit was committed. |
| `NewValue` | `object?` | The new value after the edit was committed. |

### DataSheetValidateArgs\<TItem\>

Provided to the `OnValidate` handler before Save All proceeds. The handler appends errors to block the save.

| Property | Type | Description |
| --- | --- | --- |
| `DirtyRows` | `IReadOnlyList<TItem>` | The rows with pending changes that are about to be saved. |
| `Errors` | `List<DataSheetValidationError<TItem>>` | The handler adds row/field errors here. If any errors exist after the handler returns, Save All is blocked. |

### DataSheetValidationError\<TItem\>

Represents a single validation error for a specific cell.

| Property | Type | Description |
| --- | --- | --- |
| `Row` | `TItem` | The row that failed validation. |
| `Field` | `string` | The property name of the field that failed. |
| `Message` | `string` | A human-readable error message displayed to the user. |

### DataSheetCellContext\<TItem\>

Provided to `CellTemplate` render fragments for custom cell rendering.

| Property | Type | Description |
| --- | --- | --- |
| `Item` | `TItem` | The row item. |
| `Field` | `string` | The field name of this cell's column. |
| `Value` | `object?` | The current cell value. |
| `IsEditing` | `bool` | Whether the cell is currently in edit mode. |
| `IsDirty` | `bool` | Whether the cell value has been modified since the last save or reset. |
| `ValidationError` | `string?` | The validation error message, if any. `null` when valid. |

### DataSheetSelectOption

Value/label pair used for `Select` type columns.

| Property | Type | Description |
| --- | --- | --- |
| `Value` | `string` | The stored value written to the `TItem` property. |
| `Label` | `string` | The display label shown in the dropdown editor. |

## Enums

### DataSheetColumnType

Determines the in-cell editor rendered for a column. Each value maps to a specific input widget and parsing behavior.

| Value | Description |
| --- | --- |
| `Text` | Free-text input. |
| `Number` | Numeric input with step support. Parses to the target property's numeric type. |
| `Date` | Date picker input. Parses to `DateTime` or `DateOnly`. |
| `Select` | Dropdown select from the column's `Options` list. |
| `Checkbox` | Boolean checkbox toggle. |
| `Computed` | Display-only computed value. Never enters edit mode. Not included in Save All payloads unless explicitly dirty via another mechanism. |

### CellState

Tracks the per-cell lifecycle for UX feedback and CSS styling.

| Value | Description |
| --- | --- |
| `Pristine` | Cell value has not been modified since the last save or reset. |
| `Dirty` | Cell value has been changed but not yet saved. |
| `Invalid` | Cell has a validation error (column-level or from `OnValidate`). |
| `Saving` | Cell is part of a Save All operation currently in progress. |
| `Saved` | Cell was recently saved successfully. Transitions back to `Pristine` after a brief visual indicator. |

## CSS Provider Methods

The CSS provider interface exposes methods for styling each region of the DataSheet. See [Theming and CSS Provider](slug:datasheet-theming-and-css-provider) for full details.

| Method | Description |
| --- | --- |
| `DataSheetClass(bool isLoading)` | Root container (grid + toolbar + bulk bar). |
| `DataSheetCellClass(CellState state, bool isActive, bool isEditable)` | Individual data cell based on state, focus, and editability. |
| `DataSheetHeaderCellClass(bool isSortable)` | Column header cell. |
| `DataSheetRowClass(bool isDirty, bool isSelected, bool isDeleted)` | Row styling for dirty, selection, and deletion states. |
| `DataSheetToolbarClass()` | Toolbar region. |
| `DataSheetBulkBarClass(bool isVisible)` | Bulk action bar (Save All, bulk delete). |
| `DataSheetSaveFooterClass(int dirtyCount)` | Save footer with dirty row count indicator. |

## Public Methods

Obtain a reference to the SunfishDataSheet instance via `@ref` to call these methods programmatically.

| Method | Returns | Description |
| --- | --- | --- |
| `ResetAsync()` | `Task` | Discards all dirty state and reverts every row to its last committed value. |
| `ValidateAllAsync()` | `Task<bool>` | Runs full validation across all dirty rows. Returns `true` if no errors exist. |
| `GetDirtyRows()` | `IReadOnlyList<TItem>` | Returns a snapshot of all rows with at least one dirty field. |
| `SetDataAsync(IEnumerable<TItem>)` | `Task` | Replaces the entire dataset and resets all dirty and validation state. |
| `ScrollToRowAsync(object key)` | `Task` | Scrolls the row identified by `KeyField` value into view. |
| `CommitCellEdit(TItem, string, object?)` | `Task` | Programmatically commits a cell value change and updates dirty state. |
| `EnterEditMode(TItem, string)` | `void` | Programmatically enters edit mode on a specific cell. |
| `IsCellEditing(TItem, string)` | `bool` | Returns whether a specific cell is currently in edit mode. |
| `SaveAllAsync()` | `Task` | Programmatically triggers the Save All flow (validate then fire `OnSaveAll`). |

>note The component also exposes `HandleKeyDown` and `PasteFromClipboard` as `[JSInvokable]` methods for JavaScript interop. These are internal to the component's keyboard and clipboard system and are not intended for direct consumer use. See [Keyboard and Accessibility](slug:datasheet-keyboard-and-accessibility) and [Bulk Paste and Clipboard](slug:datasheet-bulk-paste-and-clipboard) for details.

## Feature Areas

The following articles describe each feature area of SunfishDataSheet in detail:

* [Columns and Schema](slug:datasheet-columns-and-schema) — column types, editors, computed columns, and schema definition.
* [Editing and Validation](slug:datasheet-editing-and-validation) — editing lifecycle, per-cell and per-row validation, dirty tracking.
* [Selection and Ranges](slug:datasheet-selection-and-ranges) — cell and row selection model, range creation and usage.
* [Bulk Paste and Clipboard](slug:datasheet-bulk-paste-and-clipboard) — TSV copy/paste, type coercion, and error handling.
* [Bulk Operations and Save All](slug:datasheet-bulk-operations-and-saveall) — Save All contract, undo, reset, and retry guidance.
* [Virtualization and Performance](slug:datasheet-virtualization-and-performance) — row virtualization, thresholds, and limitations.
* [Keyboard and Accessibility](slug:datasheet-keyboard-and-accessibility) — keyboard shortcuts, focus model, ARIA roles.
* [Theming and CSS Provider](slug:datasheet-theming-and-css-provider) — CSS provider methods, state-based styling, class naming.

## See Also

* [SunfishDataGrid Overview](slug:grid-overview)
