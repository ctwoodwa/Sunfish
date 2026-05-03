---
title: Virtualization and Performance
page_title: DataSheet - Virtualization and Performance
description: Row virtualization, performance thresholds, and limitations for large datasets in SunfishDataSheet.
slug: datasheet-virtualization-and-performance
tags: sunfish,blazor,datasheet,virtualization,performance,virtual-scrolling
published: True
position: 6
components: ["datasheet"]
---

# DataSheet Virtualization and Performance

SunfishDataSheet uses Blazor's built-in `Virtualize` component for row-level virtualization. When enabled (the default), only visible rows are rendered to the DOM, with a small overscan buffer for smooth scrolling. This article describes how virtualization works, when to use it, and its limitations.

>caption In this article:

* [Enabling Virtualization](#enabling-virtualization)
* [How It Works](#how-it-works)
* [Recommended Thresholds](#recommended-thresholds)
* [Performance Considerations](#performance-considerations)
* [Limitations](#limitations)

## Enabling Virtualization

Virtualization is controlled by the `EnableVirtualization` parameter, which defaults to `true`.

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `EnableVirtualization` | `bool` | `true` | When `true`, the DataSheet uses Blazor `Virtualize` for row rendering. When `false`, all rows are rendered to the DOM. |

To use virtualization effectively, set a `Height` on the DataSheet so the component has a scrollable viewport:

````RAZOR
<SunfishDataSheet TItem="Product" Data="@_products" KeyField="Id"
                 EnableVirtualization="true"
                 Height="600px"
                 OnSaveAll="@HandleSave">
    @* columns *@
</SunfishDataSheet>
````

When `Height` is not set (the DataSheet auto-sizes to fit its content), virtualization has limited benefit because the browser attempts to render all rows within the expanding container.

## How It Works

The DataSheet wraps its table body (`<tbody>`) in Blazor's `<Virtualize>` component with the following configuration:

* **Items** — the internal display row list (`_displayRows`), which reflects the current data, including added rows and excluding deleted rows that have been committed.
* **OverscanCount** — set to `5`. This means 5 additional rows are rendered above and below the visible viewport, providing a buffer during scrolling.
* **Row height** — determined by the browser based on CSS. The `Virtualize` component measures the rendered row height and uses it to calculate scroll position and visible row count.

**Rendering behavior:**

* Only the rows within the viewport (plus overscan) are present in the DOM at any time.
* Scrolling causes new rows to render and off-screen rows to be removed.
* The table header (`<thead>`) is **not** virtualized — it remains sticky at the top of the scroll container.
* The toolbar and bulk action bar are also outside the virtualized region.

## Recommended Thresholds

The following guidance helps determine when to enable or disable virtualization:

| Row Count | Recommendation |
| --- | --- |
| Under 50 rows | Virtualization provides negligible benefit. Either setting works. |
| 50–500 rows | Virtualization is recommended. Rendering all rows may cause noticeable initial load time. |
| 500–10,000 rows | Virtualization is strongly recommended. Without it, initial render and re-render times grow linearly with row count. |
| Over 10,000 rows | Virtualization is essential. Consider whether the use case is appropriate for client-side DataSheet, or whether server-side paging should be used instead. |

>tip For datasets over 10,000 rows, the primary performance bottleneck shifts from DOM rendering to data transfer and in-memory state management. Consider loading data in pages or using server-side filtering to reduce the client-side dataset size.

>note **WASM Demo Threshold:** 10,000 rows is supported with `EnableVirtualization=true` in hosted Blazor Server scenarios. The interactive demo caps at 5,000 rows due to WASM host constraints (browser memory and JS interop overhead). Production Blazor Server deployments can safely exceed this threshold.

## Performance Considerations

### Initial Load

* When `IsLoading` is `true`, the DataSheet renders skeleton placeholder rows. The number of skeleton rows is a fixed count (currently 5), independent of the viewport height.
* When data arrives and `IsLoading` transitions to `false`, the component snapshots original values (via deep cloning) for all rows. For very large datasets (10,000+ rows), this cloning step may cause a brief UI pause. If this is a concern, load data incrementally.

### Editing in Virtualized Sheets

* Editing a cell in a virtualized sheet works identically to non-virtualized editing. The dirty state, validation, and undo buffer operate on the full data model, not just visible rows.
* Committing an edit triggers a re-render of the affected row only (and any computed columns in that row). Virtualization ensures this does not cascade to off-screen rows.

### Dirty Tracking Memory

* The component stores original snapshots (deep clones) and dirty field sets for modified rows. For datasets with many dirty rows, memory usage grows proportionally. Each dirty row stores both the original clone and the live instance.
* `ResetAsync()` restores originals and clears the dirty state, freeing the delta memory.

### Bulk Paste Performance

* Pasting a large block of data (e.g., 500 rows × 10 columns) triggers individual `CommitCellEdit` calls for each cell. With virtualization, only the visible cells re-render during paste, but the validation and dirty tracking overhead applies to all pasted cells.
* For very large paste operations, the component processes cells synchronously in a single frame. This may cause a brief UI freeze. No background threading is used.

### ScrollToRowAsync

* `ScrollToRowAsync(object key)` uses JavaScript interop to scroll the row with the matching `data-row-key` attribute into view. If the target row is far from the current scroll position, `Virtualize` renders intermediate rows as the browser scrolls, which is handled automatically.

## Limitations

* **Row virtualization only** — SunfishDataSheet virtualizes rows but not columns. If the DataSheet has many columns (50+), all columns are rendered for each visible row. For extremely wide sheets, consider reducing the visible column count or using horizontal scrolling.

* **No configurable overscan** — the overscan count is fixed at 5 rows. This is sufficient for most scroll speeds, but users scrolling very quickly through large datasets may briefly see empty row placeholders.

* **Selection highlight across virtualized rows** — when a selection range extends beyond the visible rows, the visual highlight is only rendered for the rows currently in the DOM. Scrolling reveals the highlight for newly visible rows within the range. The selection state itself is always correct at the data level.

* **Bulk paste into virtualized sheets** — pasting a large number of rows applies changes to the data model regardless of which rows are visible. However, the visual feedback (cell state transitions, validation highlights) is only visible for rows currently rendered. Scrolling after paste reveals the correct state for all rows.

* **Height is required** — without a `Height` parameter, the DataSheet container expands to fit all content, which defeats the purpose of virtualization. Always set `Height` when using virtualization.

* **Dynamic row heights** — the `Virtualize` component assumes consistent row heights. If cell content varies significantly in height (e.g., multiline text), the scroll position calculation may become inaccurate. Ensure consistent row heights via CSS for the best experience.

## See Also

* [DataSheet Overview](slug:datasheet-overview)
* [SunfishDataGrid Virtual Scrolling](slug:components/grid/virtual-scrolling)
