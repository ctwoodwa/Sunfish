---
title: Splitter and Dual-Pane Layout
page_title: AllocationScheduler Splitter Layout
description: Specification for the draggable vertical splitter between the resource grid and timeline panes in the AllocationScheduler component, including collapse/restore behavior, keyboard accessibility, theming, and programmatic control.
slug: allocation-scheduler-splitter-layout
tags: sunfish,blazor,allocation-scheduler,splitter,layout
published: True
position: 2
components: ["allocation-scheduler"]
---

# AllocationScheduler — Splitter and Dual-Pane Layout

## Overview

The `SunfishAllocationScheduler` uses a dual-pane layout separated by a draggable vertical splitter. The **left pane** contains the resource grid — the `<AllocationResourceColumns>` that display resource identity, role, department, and other metadata fields. The **right pane** contains the navigable timeline surface — time-bucket columns, allocation values, and visual overlays.

The splitter is the boundary between these two regions. Users drag the splitter horizontally to redistribute space between the resource grid and the timeline. This interaction model is inspired by the **Microsoft Project Gantt Chart** view, where a vertical divider separates the left task-sheet table from the right Gantt chart. Users can drag the divider to give more room to columns or to the chart, and can drag it fully to one side to effectively hide either pane.

The splitter is a first-class interactive element: it is keyboard-operable, ARIA-annotated, and themeable. It participates in the component's focus order and fires events when the user changes its position or collapses a pane.


## Default Layout

The left pane width is always equal to the sum of the widths of all rendered `AllocationResourceColumns`. There is no independent "pane width" that can exceed or fall short of the column total. Dead space in the left pane — pixels between the last column and the splitter — is a layout error and must never occur.

**Column-boundary rule:** The splitter can only rest at a position that is a valid total of the defined columns. Dragging the splitter left or right adjusts the width of the last resizable `AllocationResourceColumn`. If all columns have `AllowResize="false"`, the splitter is locked.

**Defaults:**

| Dimension | Default | Rationale |
| --- | --- | --- |
| DefaultSplitterPosition | `null` (auto) | The left pane defaults to the sum of the widths declared on all `AllocationResourceColumns`. If no explicit widths are set, columns use their natural fit-content width and the splitter starts at their combined rendered width. |
| MinLeftPaneWidth | derived | Equal to the sum of `MinWidth` values of all columns that have `AllowResize="false"` or have reached their own `MinWidth` floor. Not a free-standing pixel value. |
| MinRightPaneWidth | `300px` | The timeline pane must always show at least this many pixels so that at least a few time buckets are visible. |
| Splitter handle visible width | `8px` | Wide enough to see and target with a mouse, narrow enough to not waste horizontal space. |
| Splitter handle pointer hit area | `24px` | Extends the clickable zone beyond the visible handle for easier targeting, especially on touch devices. |


## Column Resize Relationship

The splitter and `AllocationResourceColumn` widths are two views of the same state. They must always agree:

- `SplitterPosition` = sum of all `AllocationResourceColumn` rendered widths.
- Dragging the splitter by Δpx applies Δpx to the last resizable column's `Width`, subject to that column's own `MinWidth` and `MaxWidth` constraints.
- If the last column is non-resizable, the Δpx is applied to the rightmost resizable column instead.
- If no column is resizable (all have `AllowResize="false"`), the splitter is rendered as a fixed visual divider with no drag affordance.
- Columns resized via their own column-header drag handle update `SplitterPosition` accordingly and fire `SplitterPositionChanged`.
- Any operation that changes column widths (show/hide, reorder, programmatic resize) must recalculate `SplitterPosition` and fire `SplitterPositionChanged` if the value changes.


## Parameters

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `SplitterPosition` | `double?` | `null` | The left-pane width in pixels. Supports two-way binding. When `null`, `DefaultSplitterPosition` is used on first render. |
| `DefaultSplitterPosition` | `double?` | `null` | Reserved for persisted state restore. When non-null, the scheduler attempts to restore the last saved splitter position on first render by distributing the stored width across the resizable columns proportionally. If the stored value is less than the derived `MinLeftPaneWidth` or greater than the component width minus `MinRightPaneWidth`, it is clamped silently. When `null` (default), the left pane renders at the natural sum of column widths. |
| `SplitterPositionChanged` | `EventCallback<double>` | — | Fires when the user finishes dragging the splitter. Carries the new left-pane width in pixels. |
| `MinLeftPaneWidth` | `double` | derived | Read-only derived value. Always equals the sum of `MinWidth` for columns that are non-resizable or have reached their minimum. Cannot be set directly by the consumer — control column `MinWidth` values instead. |
| `MinRightPaneWidth` | `double` | `300` | Minimum width of the right timeline pane in pixels. |
| `AllowSplitterCollapse` | `bool` | `false` | When `true`, the user can drag the splitter past `MinRightPaneWidth` or `MinLeftPaneWidth` to fully collapse either pane. A restore affordance appears when a pane is collapsed. |
| `SplitterCssClass` | `string` | `null` | Optional CSS class added to the splitter handle element for custom styling. |


## Collapse and Restore Behavior

When `AllowSplitterCollapse` is `true`, the splitter supports full pane collapse in both directions.

### Collapsing the Timeline (Right Pane)

When the user drags the splitter past the point where the right pane would be narrower than `MinRightPaneWidth`, the timeline pane **snaps fully closed**. The left resource grid expands to fill the full component width minus a narrow restore zone.

A **restore zone** remains visible at the right edge of the component:

- The restore zone is at least **24px wide** and spans the full component height.
- It renders a visual collapse indicator (a chevron or grip icon pointing left, indicating the collapsed pane direction).
- Clicking the restore zone, or pressing **Enter** or **Space** while it is focused, re-opens the timeline pane.
- The timeline restores to the last non-collapsed `SplitterPosition`. If no prior position exists, it restores to `DefaultSplitterPosition`.
- `SplitterPositionChanged` fires with the restored width.
- `OnSplitterRestored` fires with the restored width.

### Collapsing the Resource Grid (Left Pane)

Symmetric behavior applies when the user drags the splitter past `MinLeftPaneWidth` to the left:

- The left resource grid snaps fully closed.
- A restore zone (at least 24px wide, full height) appears at the left edge of the component.
- Clicking or pressing Enter/Space on the restore zone re-opens the resource grid to its last non-collapsed position, or to `DefaultSplitterPosition`.
- `SplitterPositionChanged` and `OnSplitterRestored` fire with the restored width.

### Collapse State Tracking

When a pane collapses, `OnSplitterCollapsed` fires with a `SplitterSide` value indicating which pane was collapsed. The component internally stores the last non-collapsed position so that restore operations return to a usable state.

When `AllowSplitterCollapse` is `false` (the default), dragging is clamped to the range `[MinLeftPaneWidth, componentWidth - MinRightPaneWidth]` and collapse is not possible.


## Programmatic Control

Obtain a reference with `@ref` to call splitter methods programmatically.

| Method | Return Type | Description |
| --- | --- | --- |
| `SetSplitterPosition(double widthPx)` | `Task` | Attempts to set the total left-pane width to `widthPx` by proportionally resizing all resizable columns. If `widthPx` is less than the derived `MinLeftPaneWidth`, it is clamped to `MinLeftPaneWidth`. If it exceeds the component width minus `MinRightPaneWidth`, it is clamped to that upper bound. Non-resizable columns are never altered. Fires `SplitterPositionChanged`. |
| `CollapseSplitter(SplitterSide side)` | `Task` | Fully collapses the specified pane. Requires `AllowSplitterCollapse` to be `true`; throws `InvalidOperationException` otherwise. Fires `OnSplitterCollapsed`. |
| `RestoreSplitter()` | `Task` | Restores the last collapsed pane to its prior non-collapsed position, or to `DefaultSplitterPosition` if no prior position exists. Fires `OnSplitterRestored` and `SplitterPositionChanged`. |


## Events

| Event | Args Type | Description |
| --- | --- | --- |
| `SplitterPositionChanged` | `EventCallback<double>` | Fires when the user finishes dragging the splitter. Carries the new left-pane width in pixels. Also fires after programmatic position changes via `SetSplitterPosition` or `RestoreSplitter`. |
| `OnSplitterCollapsed` | `EventCallback<SplitterSide>` | Fires when either pane is fully collapsed via drag or programmatic `CollapseSplitter`. Carries which side was collapsed. |
| `OnSplitterRestored` | `EventCallback<double>` | Fires when a collapsed pane is restored via the restore zone or programmatic `RestoreSplitter`. Carries the restored left-pane width in pixels. |


## Keyboard and Accessibility

The splitter handle is a keyboard-operable control that meets WCAG 2.1 Level AA requirements.

### ARIA Attributes

| Attribute | Value |
| --- | --- |
| `role` | `separator` |
| `aria-orientation` | `vertical` |
| `aria-valuenow` | Current left-pane width in pixels |
| `aria-valuemin` | `MinLeftPaneWidth` value |
| `aria-valuemax` | Component width minus `MinRightPaneWidth` |
| `aria-label` | `Resize resource columns` |

### Keyboard Bindings

| Key | Action |
| --- | --- |
| **Arrow Left** | Decreases left-pane width by 16px |
| **Arrow Right** | Increases left-pane width by 16px |
| **Shift + Arrow Left** | Decreases left-pane width by 64px |
| **Shift + Arrow Right** | Increases left-pane width by 64px |
| **Home** | Snaps left pane to `MinLeftPaneWidth` |
| **End** | Snaps left pane to component width minus `MinRightPaneWidth` |
| **Enter** | Collapses or restores a pane (when `AllowSplitterCollapse` is `true`). If not collapsed, collapses the right pane. If collapsed, restores to prior position. |

All keyboard adjustments are clamped to the valid range `[MinLeftPaneWidth, componentWidth - MinRightPaneWidth]` unless `AllowSplitterCollapse` is `true`, in which case Home and End can trigger full collapse.

### Focus

- The splitter handle is included in the natural Tab order of the component.
- A visible focus ring appears when the splitter receives keyboard focus, using the Sunfish `--focus-ring` CSS custom property or a 2px outline in the theme primary color.
- The focus indicator has sufficient contrast against both the splitter background and adjacent pane backgrounds.


## Theming

The splitter appearance is controlled via CSS custom properties, consistent with the AllocationScheduler theming model.

| CSS Custom Property | Default | Description |
| --- | --- | --- |
| `--allocation-scheduler-splitter-width` | `8px` | Visible width of the splitter handle. |
| `--allocation-scheduler-splitter-background` | `var(--sunfish-color-border)` | Background color of the splitter handle in its rest state. |
| `--allocation-scheduler-splitter-hover-background` | `var(--sunfish-color-primary)` | Background color when the pointer hovers over the splitter. |
| `--allocation-scheduler-splitter-active-background` | `var(--sunfish-color-primary)` | Background color while the splitter is being dragged. |
| `--allocation-scheduler-splitter-cursor` | `col-resize` | Cursor displayed when hovering over the splitter handle. |
| `--allocation-scheduler-splitter-collapse-indicator-background` | `var(--sunfish-color-subtle-background)` | Background of the collapse/restore indicator zone. |
| `--allocation-scheduler-splitter-collapse-indicator-icon-color` | `var(--sunfish-color-primary)` | Color of the chevron or grip icon in the collapse/restore indicator. |

### BEM Class Names

```
mar-allocation-scheduler__splitter                           (element)
mar-allocation-scheduler__splitter--dragging                 (modifier)
mar-allocation-scheduler__splitter--focused                  (modifier)
mar-allocation-scheduler__splitter-restore                   (element)
mar-allocation-scheduler__splitter-restore--left             (modifier — restore zone at the left edge when the resource grid is collapsed)
mar-allocation-scheduler__splitter-restore--right            (modifier — restore zone at the right edge when the timeline is collapsed)
mar-allocation-scheduler__pane--left                         (element)
mar-allocation-scheduler__pane--right                        (element)
mar-allocation-scheduler__pane--collapsed                    (modifier — applied when the pane is fully collapsed; hides pane content and triggers restore zone display)
```

**Collapse lifecycle:** When a pane collapses, the `--collapsed` modifier is added to the pane element and the corresponding `__splitter-restore--left` or `__splitter-restore--right` element becomes visible. Clicking or pressing Enter/Space on the restore zone removes `--collapsed` and restores the pane to its prior width.


## SplitterSide Enumeration

The `SplitterSide` enum identifies which pane is the target of a collapse or restore operation.

```csharp
public enum SplitterSide
{
    Left,
    Right
}
```

This enum is used by `CollapseSplitter(SplitterSide side)` and `OnSplitterCollapsed`.


## Demo Scenarios

The following scenarios represent the primary coverage targets for splitter-related examples and tests.

1. **Drag splitter resizes last column** — Define two columns (Name 180px, Role 120px); confirm left pane renders at exactly 300px with no blank space; drag splitter 60px to the right; confirm Role column grows to 180px and `SplitterPositionChanged` fires with value 360.

2. **Collapse and restore** — Set `AllowSplitterCollapse="true"`. Drag the splitter all the way to the right to collapse the timeline pane. Confirm the restore zone is visible at the right edge. Click the restore zone and confirm the timeline re-opens to the prior width. Verify that `OnSplitterCollapsed` fires on collapse and `OnSplitterRestored` fires on restore.

3. **Programmatic SetSplitterPosition** — Call `SetSplitterPosition(480)` with Name (180px, MinWidth 80px) and Role (120px, MinWidth 60px) columns; confirm the last resizable column (Role) grows by 180px to 300px while Name remains 180px; confirm no blank space exists in the left pane.
