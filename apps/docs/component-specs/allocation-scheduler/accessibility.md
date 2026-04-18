---
title: Accessibility
page_title: AllocationScheduler Accessibility
description: ARIA roles, states, and keyboard interaction for the AllocationScheduler component.
slug: allocation-scheduler-accessibility
tags: sunfish,blazor,allocation-scheduler,accessibility,aria,keyboard
published: True
position: 8
components: ["allocation-scheduler"]
---

# AllocationScheduler Accessibility

The `SunfishAllocationScheduler` implements the WAI-ARIA grid pattern to provide a fully accessible spreadsheet-style editing experience.

## ARIA Roles

| Element | Role | Purpose |
| --- | --- | --- |
| Root container | `grid` | Identifies the component as an interactive data grid. |
| Scenario strip | `toolbar` | Groups scenario selection chips. |
| Scenario chip | `button` | Each chip uses `aria-pressed` to indicate the active scenario. |
| Navigation toolbar | `toolbar` | Groups navigation and zoom controls. |
| Header cell (resource column) | `columnheader` | Labels resource metadata columns. |
| Header cell (time bucket) | `columnheader` | Labels each time bucket; uses `aria-label` with formatted date. |
| Resource row | `row` | Groups all cells for one resource; uses `aria-label` with the resource display name. |
| Resource metadata cell | `gridcell` | Displays resource field values. |
| Allocation cell | `gridcell` | Editable or read-only allocation value. |
| Loading indicator | `status` | Announces loading state to screen readers. |

## ARIA States and Properties

| Attribute | Applied To | Meaning |
| --- | --- | --- |
| `aria-label="Allocation Scheduler"` | Root grid | Announces the component purpose. |
| `aria-label` | Navigation buttons | Announces "Navigate back", "Navigate to today", "Navigate forward". |
| `aria-label` | Time-bucket headers | Formatted bucket date (e.g., "Week of Apr 6, 2026"). |
| `aria-label` | Resource rows | Resource display name. |
| `aria-label` | Allocation cells | Contextual label combining resource, bucket, and value. |
| `aria-selected` | Allocation cells | `"true"` when the cell is part of the current selection. |
| `aria-disabled` | Allocation cells | `"true"` when the cell cannot be interacted with (e.g., conflict state). |
| `aria-readonly` | Allocation cells | `"true"` when the cell displays a read-only rollup (view grain coarser than authoritative level). |
| `aria-pressed` | Scenario chips | `"true"` for the active scenario. |
| `aria-hidden="true"` | Lock icon span | Decorative lock icon hidden from screen readers. |

## Keyboard Interaction

The keyboard model follows the WAI-ARIA grid navigation pattern and is enabled when `AllowKeyboardEdit="true"` (default).

| Key | Action |
| --- | --- |
| `Tab` | Move focus to the next cell in the row. |
| `Shift+Tab` | Move focus to the previous cell in the row. |
| `Enter` | Move focus down to the same column in the next resource row. |
| `ArrowRight` | Move focus one cell to the right. |
| `ArrowLeft` | Move focus one cell to the left. |
| `ArrowDown` | Move focus down one row. |
| `ArrowUp` | Move focus up one row. |
| Typing a number | Begin editing the focused cell (replaces current value). |
| `Escape` | Cancel in-progress edit and restore previous value. |

## Drag-Fill Accessibility

Drag-fill (`AllowDragFill="true"`) is a mouse-only interaction. Equivalent keyboard access is provided through range selection (`AllowBulkEdit`) combined with the context menu (`EnableContextMenu`), which allows setting a value across the selected range.

## Screen Reader Expectations

- On focus, a screen reader announces the grid role and label.
- Navigating into a cell announces the column header, resource name, and current value.
- Editing a cell announces the old and new values on commit.
- Read-only rollup cells are announced as "read-only" via `aria-readonly`.
- Disabled cells are announced as "unavailable" via `aria-disabled`.
