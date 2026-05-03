---
title: Theming and CSS Provider
page_title: DataSheet - Theming and CSS Provider
description: CSS provider methods, state-based styling, and class naming guidance for SunfishDataSheet.
slug: datasheet-theming-and-css-provider
tags: sunfish,blazor,datasheet,theming,css,styling,provider
published: True
position: 8
components: ["datasheet"]
---

# DataSheet Theming and CSS Provider

SunfishDataSheet delegates all visual styling to the CSS provider (`ISunfishCssProvider`). The provider exposes methods for each region of the DataSheet, receiving state flags that allow themes to apply context-sensitive classes. This article defines each CSS provider method, its parameters, and how themes are expected to use them.

>caption In this article:

* [CSS Provider Overview](#css-provider-overview)
* [DataSheetClass](#datasheetclass)
* [DataSheetCellClass](#datasheetcellclass)
* [DataSheetHeaderCellClass](#datasheetheadercellclass)
* [DataSheetRowClass](#datasheetrowclass)
* [DataSheetToolbarClass](#datasheettoolbarclass)
* [DataSheetBulkBarClass](#datasheetbulkbarclass)
* [DataSheetSaveFooterClass](#datasheetsavefooterclass)
* [CellState Visual Mapping](#cellstate-visual-mapping)
* [Class Naming Conventions](#class-naming-conventions)
* [Example Theme Implementation](#example-theme-implementation)

## CSS Provider Overview

The DataSheet calls CSS provider methods during rendering to obtain class strings for each element. The provider pattern is the same used by other Sunfish components — a single `ISunfishCssProvider` implementation supplies classes for all components, and the DataSheet methods receive state parameters that describe the current condition of each element.

Themes must return space-separated CSS class strings. The DataSheet applies these classes directly to the corresponding HTML elements. The component does not add its own hard-coded classes — all styling is delegated to the provider.

## DataSheetClass

```csharp
string DataSheetClass(bool isLoading)
```

Applied to the **root container** element that wraps the entire DataSheet (grid, toolbar, bulk bar, and save footer).

| Parameter | Type | Description |
| --- | --- | --- |
| `isLoading` | `bool` | `true` when the `IsLoading` parameter is set, indicating skeleton rows are shown. |

**Theme expectations:**

* Return the base DataSheet container class (e.g., `mar-datasheet`).
* When `isLoading` is `true`, add a loading modifier (e.g., `mar-datasheet--loading`) to apply reduced opacity, pointer-events restrictions, or skeleton animation styles.

**FluentUI implementation example:**

```
mar-datasheet                          (always)
mar-datasheet--loading                 (when isLoading)
```

## DataSheetCellClass

```csharp
string DataSheetCellClass(CellState state, bool isActive, bool isEditable)
```

Applied to each **data cell** (`<td>` with `role="gridcell"`). This is the most frequently called provider method — it runs once per visible cell on every render.

| Parameter | Type | Description |
| --- | --- | --- |
| `state` | `CellState` | The current state of the cell: `Pristine`, `Dirty`, `Invalid`, `Saving`, or `Saved`. |
| `isActive` | `bool` | `true` when this cell is the currently focused (active) cell. |
| `isEditable` | `bool` | `true` when the column allows editing. `false` for computed or `Editable="false"` columns. |

**Theme expectations:**

* Return a base cell class (e.g., `mar-datasheet__cell`).
* Add state modifiers based on `CellState`:
  * `Dirty` → `mar-datasheet__cell--dirty` (e.g., subtle background tint to indicate pending change).
  * `Invalid` → `mar-datasheet__cell--invalid` (e.g., red border or background).
  * `Saving` → `mar-datasheet__cell--saving` (e.g., pulsing animation or muted colors).
  * `Saved` → `mar-datasheet__cell--saved` (e.g., brief green highlight that fades).
  * `Pristine` → no additional modifier.
* When `isActive` is `true`, add a focus class (e.g., `mar-datasheet__cell--active`) for the focus ring.
* When `isEditable` is `false`, add a readonly class (e.g., `mar-datasheet__cell--readonly`) to visually distinguish non-editable cells (e.g., lighter background).

**FluentUI implementation example:**

```
mar-datasheet__cell                    (always)
mar-datasheet__cell--active            (when isActive)
mar-datasheet__cell--readonly          (when !isEditable)
mar-datasheet__cell--dirty             (when state == Dirty)
mar-datasheet__cell--invalid           (when state == Invalid)
mar-datasheet__cell--saving            (when state == Saving)
mar-datasheet__cell--saved             (when state == Saved)
```

## DataSheetHeaderCellClass

```csharp
string DataSheetHeaderCellClass(bool isSortable)
```

Applied to each **column header cell** (`<th>` with `role="columnheader"`).

| Parameter | Type | Description |
| --- | --- | --- |
| `isSortable` | `bool` | Included for interface symmetry with other grid-like components. The DataSheet does not support sorting (sorting is a SunfishDataGrid feature), so this parameter is always `false`. |

**Theme expectations:**

* Return a base header cell class (e.g., `mar-datasheet__header-cell`).
* The `isSortable` parameter exists for interface consistency across the CSS provider. Since the DataSheet does not support sorting, filtering, or column reordering (these are SunfishDataGrid features), themes can ignore this flag and return a static class.

**FluentUI implementation example:**

```
mar-datasheet__header-cell             (always)
```

## DataSheetRowClass

```csharp
string DataSheetRowClass(bool isDirty, bool isSelected, bool isDeleted)
```

Applied to each **data row** (`<tr>` with `role="row"`).

| Parameter | Type | Description |
| --- | --- | --- |
| `isDirty` | `bool` | `true` when the row has at least one dirty field. |
| `isSelected` | `bool` | `true` when the row is selected for bulk operations (e.g., bulk delete). |
| `isDeleted` | `bool` | `true` when the row has been marked for deletion (pending Save All). |

**Theme expectations:**

* Return a base row class (e.g., `mar-datasheet__row`).
* When `isDirty`, add a modifier (e.g., `mar-datasheet__row--dirty`) to visually indicate the row has unsaved changes (e.g., a left border accent).
* When `isSelected`, add a modifier (e.g., `mar-datasheet__row--selected`) for selection highlighting (e.g., light blue background).
* When `isDeleted`, add a modifier (e.g., `mar-datasheet__row--deleted`) to show the row is pending deletion (e.g., strikethrough text, reduced opacity).

**FluentUI implementation example:**

```
mar-datasheet__row                     (always)
mar-datasheet__row--dirty              (when isDirty)
mar-datasheet__row--selected           (when isSelected)
mar-datasheet__row--deleted            (when isDeleted)
```

>note A row can be both dirty and deleted (the user edited cells then deleted the row). Themes should ensure the `--deleted` style takes visual priority when both modifiers are present.

## DataSheetToolbarClass

```csharp
string DataSheetToolbarClass()
```

Applied to the **toolbar container** above the grid header.

| Parameter | Type | Description |
| --- | --- | --- |
| (none) | — | No state parameters. |

**Theme expectations:**

* Return a toolbar class (e.g., `mar-datasheet__toolbar`) that provides layout (flex, padding, alignment) for toolbar items (Add Row button, custom `ToolbarTemplate` content).

**FluentUI implementation example:**

```
mar-datasheet__toolbar                 (always)
```

## DataSheetBulkBarClass

```csharp
string DataSheetBulkBarClass(bool isVisible)
```

Applied to the **bulk action bar** that appears when rows are selected or dirty rows exist.

| Parameter | Type | Description |
| --- | --- | --- |
| `isVisible` | `bool` | `true` when the bulk bar should be shown (e.g., rows are selected for deletion, or there are dirty rows to save). |

**Theme expectations:**

* Return a base bulk bar class (e.g., `mar-datasheet__bulk-bar`).
* When `isVisible` is `true`, add a visibility modifier (e.g., `mar-datasheet__bulk-bar--visible`). The bar may use CSS transitions to slide in/out.
* When `isVisible` is `false`, the bar should be hidden (e.g., `display: none` or `max-height: 0` with transition).

**FluentUI implementation example:**

```
mar-datasheet__bulk-bar                (always)
mar-datasheet__bulk-bar--visible       (when isVisible)
```

## DataSheetSaveFooterClass

```csharp
string DataSheetSaveFooterClass(int dirtyCount)
```

Applied to the **save footer** region that displays the dirty row count and Save All button.

| Parameter | Type | Description |
| --- | --- | --- |
| `dirtyCount` | `int` | The number of rows with at least one dirty field. `0` when no changes are pending. |

**Theme expectations:**

* Return a base footer class (e.g., `mar-datasheet__save-footer`).
* When `dirtyCount > 0`, add a modifier (e.g., `mar-datasheet__save-footer--has-changes`) to visually emphasize the footer (e.g., accent color, elevated shadow).
* The footer typically displays text like "3 rows modified" and a Save All button. The dirty count text is managed by the component; the CSS provider only controls the container styling.

**FluentUI implementation example:**

```
mar-datasheet__save-footer             (always)
mar-datasheet__save-footer--has-changes (when dirtyCount > 0)
```

## CellState Visual Mapping

The following table summarizes how each `CellState` value should map to visual treatment in a theme:

| CellState | Recommended Visual Treatment | Purpose |
| --- | --- | --- |
| `Pristine` | No special styling. Default cell appearance. | Cell has no pending changes. |
| `Dirty` | Subtle background tint (e.g., light amber or blue). Optional left-border accent. | Indicates unsaved changes. Helps users see what has been modified at a glance. |
| `Invalid` | Red border or red background tint. Error icon or indicator. | Draws attention to cells that must be fixed before Save All. |
| `Saving` | Pulsing or muted animation. Reduced saturation. | Indicates the cell is part of an in-progress save operation. |
| `Saved` | Brief green highlight that fades out via CSS transition. | Provides confirmation feedback. Transitions to `Pristine` styling after the animation completes. |

Themes should also handle combinations of cell state and active/readonly flags:

* An **active invalid** cell should show both the focus ring and the error styling.
* A **readonly dirty** cell should not normally occur (readonly cells cannot be edited), but if it does (e.g., via programmatic commit), the dirty styling applies.

## Class Naming Conventions

SunfishDataSheet CSS classes follow the BEM-like pattern used by other Sunfish components:

| Pattern | Example | Usage |
| --- | --- | --- |
| `mar-{component}` | `mar-datasheet` | Block (root element) |
| `mar-{component}__{element}` | `mar-datasheet__cell` | Element (child of the block) |
| `mar-{component}__{element}--{modifier}` | `mar-datasheet__cell--dirty` | Modifier (state variation) |

This is consistent with the naming used in other Sunfish component CSS providers (e.g., `mar-grid`, `mar-button`, `mar-dialog`).

## Example Theme Implementation

The following is a simplified example of what a CSS provider implementation looks like for the DataSheet methods. This mirrors the pattern used in the FluentUI provider.

```csharp
public string DataSheetClass(bool isLoading)
{
    var builder = new CssClassBuilder("mar-datasheet");
    if (isLoading) builder.Add("mar-datasheet--loading");
    return builder.Build();
}

public string DataSheetCellClass(CellState state, bool isActive, bool isEditable)
{
    var builder = new CssClassBuilder("mar-datasheet__cell");
    if (isActive) builder.Add("mar-datasheet__cell--active");
    if (!isEditable) builder.Add("mar-datasheet__cell--readonly");

    builder.Add(state switch
    {
        CellState.Dirty => "mar-datasheet__cell--dirty",
        CellState.Invalid => "mar-datasheet__cell--invalid",
        CellState.Saving => "mar-datasheet__cell--saving",
        CellState.Saved => "mar-datasheet__cell--saved",
        _ => ""
    });

    return builder.Build();
}

public string DataSheetHeaderCellClass(bool isSortable)
    => "mar-datasheet__header-cell";

public string DataSheetRowClass(bool isDirty, bool isSelected, bool isDeleted)
{
    var builder = new CssClassBuilder("mar-datasheet__row");
    if (isDirty) builder.Add("mar-datasheet__row--dirty");
    if (isSelected) builder.Add("mar-datasheet__row--selected");
    if (isDeleted) builder.Add("mar-datasheet__row--deleted");
    return builder.Build();
}

public string DataSheetToolbarClass()
    => "mar-datasheet__toolbar";

public string DataSheetBulkBarClass(bool isVisible)
{
    var builder = new CssClassBuilder("mar-datasheet__bulk-bar");
    if (isVisible) builder.Add("mar-datasheet__bulk-bar--visible");
    return builder.Build();
}

public string DataSheetSaveFooterClass(int dirtyCount)
{
    var builder = new CssClassBuilder("mar-datasheet__save-footer");
    if (dirtyCount > 0) builder.Add("mar-datasheet__save-footer--has-changes");
    return builder.Build();
}
```

## See Also

* [DataSheet Overview](slug:datasheet-overview)
* [Editing and Validation](slug:datasheet-editing-and-validation)
