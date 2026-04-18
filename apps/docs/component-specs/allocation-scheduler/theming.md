---
title: Theming
page_title: AllocationScheduler Theming
description: CSS provider methods, CSS variable list, and theming customization for the AllocationScheduler component.
slug: allocation-scheduler-theming
tags: sunfish,blazor,allocation-scheduler,theming,css,fluent-ui,bootstrap
published: True
position: 7
components: ["allocation-scheduler"]
---

# AllocationScheduler Theming

## CSS Provider Methods

The AllocationScheduler uses 14 CSS provider methods. Both FluentUI and Bootstrap providers implement all methods.

| Method | Parameters | Returns |
|---|---|---|
| AllocationSchedulerClass | none | Root container class |
| AllocationSchedulerToolbarClass | none | Toolbar container |
| AllocationSchedulerResourceColumnClass | isPinned | Resource column cell |
| AllocationSchedulerTimeHeaderClass | grain | Time bucket header |
| AllocationSchedulerRowClass | isSelected, isOverAllocated | Resource row |
| AllocationSchedulerCellClass | isEditable, isSelected, isConflict, isDisabled, isDragTarget | Allocation cell |
| AllocationSchedulerCellValueClass | mode | Cell value display |
| AllocationSchedulerDeltaClass | mode, isOver, isUnder | Delta indicator |
| AllocationSchedulerScenarioStripClass | none | Scenario strip container |
| AllocationSchedulerScenarioChipClass | isActive, isLocked | Scenario chip |
| AllocationSchedulerGhostBarClass | none | Baseline ghost bar |
| AllocationSchedulerContextMenuClass | none | Context menu popup |
| AllocationSchedulerEmptyClass | none | Empty state container |
| AllocationSchedulerLoaderClass | none | Loading state container |

## BEM Class Names

All classes follow the `mar-allocation-scheduler` BEM block prefix.

```
mar-allocation-scheduler                         (block)
mar-allocation-scheduler__cell                   (element)
mar-allocation-scheduler__cell--selected         (modifier)
mar-allocation-scheduler__cell--conflict         (modifier)
mar-allocation-scheduler__row--over-allocated    (modifier)
mar-allocation-scheduler__scenario-chip--active   (modifier)
```

## FluentUI Design Tokens

| Token | Usage |
|---|---|
| `--sunfish-color-primary` | Selected cell, active scenario chip, drag target |
| `--sunfish-color-danger` | Over-allocated rows, conflict cells |
| `--sunfish-color-warning` | Under-allocation indicators |
| `--sunfish-color-success` | Currency value color |
| `--sunfish-color-border` | Cell and row separators |
| `--sunfish-color-surface` | Cell backgrounds |
| `--sunfish-color-subtle-background` | Header and toolbar backgrounds |

## Dark Theme Token Overrides

When `[data-sunfish-theme="dark"]` is active, the FluentUI provider overrides several design tokens to ensure readable contrast on dark surfaces. These overrides are scoped to the `.mar-allocation-scheduler` block.

| Token | Dark-Mode Value | Purpose |
|---|---|---|
| `--sunfish-color-subtle-background` | `#2b2b2b` | Toolbar and header row backgrounds |
| `--sunfish-color-disabled-background` | `#3a3a3a` | Disabled cell backgrounds |
| `--sunfish-color-primary-rgb` | `76, 166, 255` | Used for `color-mix()` tints on selection and drag-target backgrounds |
| `--sunfish-color-text` | `#e0e0e0` | Primary text color for cell values and labels |
| `--sunfish-color-border` | `#404040` | Cell and row separator borders |
| `--sunfish-color-surface` | `#1e1e1e` | Cell and pane backgrounds |

These tokens are consumed by the same SCSS rules that drive light mode — no separate dark-mode selectors are needed beyond the token overrides. Custom themes can override any of these tokens at the host level to create branded dark modes.

## Bootstrap Mapping

| Sunfish class | Bootstrap equivalent |
|---|---|
| Selected row | `table-active` |
| Over-allocated row | `table-danger` |
| Selected cell | `table-primary` |
| Conflict cell | `table-danger` |
| Disabled cell | `text-muted bg-light` |
| Scenario chip | `badge rounded-pill bg-primary`/`bg-secondary` |
| Over delta | `text-danger` |
| Under delta | `text-warning` |
