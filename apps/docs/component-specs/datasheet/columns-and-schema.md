---
title: Columns and Schema
page_title: DataSheet - Columns and Schema
description: Define typed columns, editors, validation, and computed fields for the SunfishDataSheet component.
slug: datasheet-columns-and-schema
tags: sunfish,blazor,datasheet,columns,schema,editors
published: True
position: 1
components: ["datasheet"]
---

# DataSheet Columns and Schema

Each `SunfishDataSheetColumn` defines how a single property on `TItem` is displayed, edited, and validated within the DataSheet. Columns are the schema — they determine the editor widget, parsing rules, formatting, and whether a field participates in dirty tracking and Save All.

>caption In this article:

* [Column Definition Basics](#column-definition-basics)
* [Column Type Reference](#column-type-reference)
* [Text Columns](#text-columns)
* [Number Columns](#number-columns)
* [Date Columns](#date-columns)
* [Select Columns](#select-columns)
* [Checkbox Columns](#checkbox-columns)
* [Computed Columns](#computed-columns)
* [Required Fields](#required-fields)
* [Custom Cell Templates](#custom-cell-templates)
* [Column Width and Sizing](#column-width-and-sizing)

## Column Definition Basics

Columns are declared as child content of `SunfishDataSheet`. The `Field` parameter binds the column to a property on `TItem` by name (case-sensitive). The component uses reflection to read and write property values at runtime.

>caption Define a mix of column types

````RAZOR
<SunfishDataSheet TItem="Product" Data="@_products" KeyField="Id"
                 OnSaveAll="@HandleSave">
    <SunfishDataSheetColumn TItem="Product" Field="Name" Title="Product Name"
        ColumnType="DataSheetColumnType.Text" Required="true" />
    <SunfishDataSheetColumn TItem="Product" Field="Price" Title="Unit Price"
        ColumnType="DataSheetColumnType.Number"
        Format="@(r => r.Price.ToString("C2"))" />
    <SunfishDataSheetColumn TItem="Product" Field="ReleaseDate" Title="Release"
        ColumnType="DataSheetColumnType.Date" />
    <SunfishDataSheetColumn TItem="Product" Field="Category" Title="Category"
        ColumnType="DataSheetColumnType.Select" Options="@_categories" />
    <SunfishDataSheetColumn TItem="Product" Field="InStock" Title="In Stock"
        ColumnType="DataSheetColumnType.Checkbox" />
    <SunfishDataSheetColumn TItem="Product" Field="Margin" Title="Margin"
        ColumnType="DataSheetColumnType.Computed" Editable="false"
        Format="@(r => ((r.Price - r.Cost) / r.Price * 100).ToString("F1") + "%")" />
</SunfishDataSheet>
````

### Column Parameter Reference

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `Field` | `string` | required | Property name on `TItem`. Case-sensitive. Must match a public property with a getter (and a setter for editable columns). |
| `Title` | `string?` | Field value | Header text. If omitted, the raw `Field` name is displayed. |
| `ColumnType` | `DataSheetColumnType` | `Text` | Determines the editor widget, parsing logic, and default validation behavior. |
| `Editable` | `bool` | `true` | Set to `false` to prevent user editing. Cells render in read-only mode. Computed columns ignore this parameter and are always read-only. |
| `Required` | `bool` | `false` | When `true`, the column's built-in validation rejects null, empty strings, and unchecked checkboxes. Blocks Save All if violated. |
| `MinWidth` | `int?` | `null` | Minimum column width in pixels. The browser may render the column wider if content requires it. |
| `Width` | `string?` | `null` | Explicit column width as a CSS value (e.g., `"150px"`, `"10%"`). |
| `Format` | `Func<TItem, string?>?` | `null` | Display formatter invoked when the cell is in read mode. Receives the full row and returns a display string. |
| `Validate` | `Func<TItem, string?>?` | `null` | Per-cell validator. Runs after each cell commit and before Save All. Return `null` for valid, or an error message string for invalid. |
| `Options` | `IEnumerable<DataSheetSelectOption>?` | `null` | Value/label pairs for `Select` columns. Ignored for other column types. |
| `CellTemplate` | `RenderFragment<DataSheetCellContext<TItem>>?` | `null` | Custom cell rendering. Overrides the default editor/display for this column. |

## Column Type Reference

The `DataSheetColumnType` enum determines the editor rendered in each cell. The following sections describe each type in detail.

| ColumnType | Editor Widget | Value Type | Participates in Save All | Editable by Default |
| --- | --- | --- | --- | --- |
| `Text` | Text input | `string` | Yes | Yes |
| `Number` | Numeric input | `int`, `decimal`, `double`, etc. | Yes | Yes |
| `Date` | Date picker | `DateTime`, `DateOnly`, `DateTime?` | Yes | Yes |
| `Select` | Dropdown select | `string` (matched to `Options.Value`) | Yes | Yes |
| `Checkbox` | Checkbox toggle | `bool` | Yes | Yes |
| `Computed` | Formatted text (read-only) | Any (via `Format`) | No (read-only) | No |

## Text Columns

Text columns render a standard text input in edit mode. The value is read and written as a `string`.

**Editor behavior:**

* Clicking or pressing F2 on a focused text cell enters edit mode with the current value selected.
* The text input accepts any string value.
* Pressing Enter or Tab commits the value. Pressing Escape reverts to the pre-edit value.
* If `Required` is `true`, committing an empty string marks the cell as `CellState.Invalid`.

**Validation:**

* Built-in required check: rejects `null` or whitespace-only strings when `Required` is set.
* Custom `Validate` delegate runs after the required check. It receives the full row so it can perform cross-field validation.

**Dirty tracking:**

* A text cell becomes dirty when the committed value differs from the original snapshot (string comparison, ordinal).

## Number Columns

Number columns render a numeric input in edit mode. The component parses the input string to the target property's numeric type using `decimal.TryParse` (with fallback type conversion for `int`, `double`, `float`, etc.).

**Editor behavior:**

* The input restricts entry to numeric characters, decimal separators, and minus signs.
* Step buttons or arrow keys may increment/decrement the value (browser-native behavior).
* Committing a non-numeric string leaves the cell in `CellState.Invalid` with a parse error.

**Validation:**

* Built-in parse validation: if the input cannot be parsed to the target numeric type, the cell is marked invalid.
* Built-in required check: rejects `null` or zero when `Required` is set (zero rejection applies only to non-nullable types where `default` is `0`).
* Custom `Validate` delegate runs after type parsing succeeds.

**Display format:**

* When a `Format` delegate is provided, read-mode cells display the formatted string (e.g., currency, percentage).
* Edit mode always shows the raw numeric value for precise editing.

## Date Columns

Date columns render a date picker input in edit mode. The value is parsed to `DateTime` (or `DateOnly` / `DateTime?` depending on the property type) using `DateTime.TryParse`.

**Editor behavior:**

* Clicking or pressing F2 opens the date input. The browser's native date picker may appear depending on the platform.
* The accepted format is determined by the browser locale and input type.
* Committing an unparseable date string marks the cell as `CellState.Invalid`.

**Validation:**

* Built-in parse validation: rejects values that cannot be parsed to the target date type.
* Built-in required check: rejects `null` or `default(DateTime)` when `Required` is set.
* Custom `Validate` delegate runs after parsing succeeds.

**Display format:**

* The `Format` delegate controls the display string in read mode (e.g., `r => r.Date.ToString("yyyy-MM-dd")`).
* Without a `Format`, the default `ToString()` of the property value is displayed.

## Select Columns

Select columns render a dropdown populated from the `Options` parameter. The stored value is the `DataSheetSelectOption.Value` string; the displayed text is `DataSheetSelectOption.Label`.

**Editor behavior:**

* Clicking or pressing F2 opens the dropdown.
* The user selects one option from the list. Typing filters options if the dropdown supports it.
* Pressing Escape closes the dropdown without changing the value.
* Committing a selection writes the `Value` string to the `TItem` property.

**Validation:**

* Built-in required check: rejects `null` or empty `Value` when `Required` is set.
* If the current cell value does not match any `Options.Value`, the cell is not automatically marked invalid — the existing value is preserved. This allows for values that were valid at the time of data entry but have since been removed from the options list.
* Custom `Validate` delegate can enforce stricter option matching if needed.

**Options contract:**

* The `Options` collection is read when the column is rendered. Changing `Options` at runtime updates the dropdown on the next render cycle.
* `Value` strings must be unique within the `Options` collection.

>caption Define select options

````RAZOR
<SunfishDataSheetColumn TItem="Product" Field="Category" Title="Category"
    ColumnType="DataSheetColumnType.Select"
    Options="@(new List<DataSheetSelectOption>
    {
        new() { Value = "Electronics", Label = "Electronics" },
        new() { Value = "Clothing", Label = "Clothing" },
        new() { Value = "Food", Label = "Food & Beverage" }
    })" />
````

## Checkbox Columns

Checkbox columns render a checkbox toggle. The bound property must be `bool` (or `bool?` for tri-state, though tri-state is not natively supported by the editor — `null` renders as unchecked).

**Editor behavior:**

* Clicking the checkbox or pressing Space toggles the value immediately (no separate edit mode). The change is committed on toggle.
* Tab and arrow key navigation moves focus without toggling.

**Validation:**

* Built-in required check: when `Required` is set, `false` is treated as invalid (the checkbox must be checked).
* Custom `Validate` delegate runs after the toggle commit.

**Dirty tracking:**

* The cell becomes dirty when the boolean value differs from the original snapshot.

## Computed Columns

Computed columns are **read-only display columns**. They never enter edit mode, regardless of the `Editable` parameter value. Their purpose is to show derived or computed values based on other fields in the same row.

**Behavior contracts:**

* The `Format` delegate is required for computed columns. Without it, the cell displays the raw `ToString()` of the property value.
* Computed columns **do not participate in dirty tracking**. Changing other fields in the row does not mark the computed column as dirty.
* Computed columns **are not included in `DataSheetSaveArgs.DirtyRows`** field-level tracking. The row itself may be dirty due to other fields, but the computed column's value is not sent as a changed field.
* The displayed value is recalculated on every render. If the `Format` delegate depends on other row properties that have been edited, the computed value updates immediately in the UI.
* Computed columns are skipped during clipboard paste — pasted data in a computed column's position is discarded.
* Keyboard navigation passes through computed cells but does not allow edit mode entry.

>caption Computed column showing a derived total

````RAZOR
<SunfishDataSheetColumn TItem="OrderLine" Field="LineTotal" Title="Total"
    ColumnType="DataSheetColumnType.Computed" Editable="false"
    Format="@(r => (r.Quantity * r.UnitPrice).ToString("C2"))" />
````

>important Computed columns should not have a backing setter that performs side effects. The component reads the property via reflection for display purposes only; it never writes to computed column properties.

## Required Fields

Setting `Required="true"` on a column activates built-in validation that blocks Save All when the field is empty:

| ColumnType | "Empty" definition |
| --- | --- |
| `Text` | `null` or whitespace-only string |
| `Number` | `null` (for nullable types) |
| `Date` | `null` or `default(DateTime)` |
| `Select` | `null` or empty string |
| `Checkbox` | `false` (unchecked) |
| `Computed` | Not applicable (never validated) |

When a required field fails validation, the cell transitions to `CellState.Invalid` and displays a default error message: `"{Title} is required."` This message can be overridden by providing a `Validate` delegate that returns a custom message for the same condition.

## Custom Cell Templates

The `CellTemplate` parameter allows full control over cell rendering. The template receives a `DataSheetCellContext<TItem>` with the row item, field name, current value, editing state, dirty flag, and validation error.

>caption Custom cell template with conditional styling

````RAZOR
<SunfishDataSheetColumn TItem="Product" Field="Price" Title="Price"
    ColumnType="DataSheetColumnType.Number">
    <CellTemplate>
        @if (context.IsEditing)
        {
            <input type="number" value="@context.Value"
                   @onchange="@(e => CommitPrice(context.Item, e))" />
        }
        else
        {
            <span class="@(context.IsDirty ? "price-changed" : "")">
                @((context.Value as decimal?)?.ToString("C2") ?? "—")
            </span>
        }
    </CellTemplate>
</SunfishDataSheetColumn>
````

When using `CellTemplate`, the caller is responsible for:

* Rendering appropriate content for both display and edit modes (check `context.IsEditing`).
* Committing edits via `CommitCellEdit` on the DataSheet `@ref` (the template does not automatically commit).
* Displaying validation errors from `context.ValidationError` if desired.

## Column Width and Sizing

Columns accept `Width` (explicit CSS value) and `MinWidth` (minimum pixels). When neither is set, columns share available space equally, similar to an HTML table with no explicit widths.

**Sizing behavior:**

* `Width` sets the column to an exact width. The value can be any CSS unit (`px`, `%`, `rem`, etc.).
* `MinWidth` ensures the column never shrinks below the specified pixel value, even when the container is narrow.
* When both are set, `Width` takes precedence and `MinWidth` acts as a floor.
* If the total of all explicit column widths exceeds the container width, a horizontal scrollbar appears.

## See Also

* [DataSheet Overview](slug:datasheet-overview)
* [Editing and Validation](slug:datasheet-editing-and-validation)
* [Bulk Paste and Clipboard](slug:datasheet-bulk-paste-and-clipboard)
