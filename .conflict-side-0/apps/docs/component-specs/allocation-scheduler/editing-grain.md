---
title: Editing Grain Design Decision
page_title: AllocationScheduler Editing Grain
description: Design decision record for AllocationScheduler's AuthoritativeLevel parameter - why a single editing granularity is used, how rollup views work, and how distribution commands operate at coarser zoom levels.
slug: allocation-scheduler-editing-grain
tags: sunfish,blazor,allocation-scheduler,design-decision,authoritative-level,editing-grain
published: True
position: 1
components: ["allocation-scheduler"]
---

# AllocationScheduler — Editing Grain Design Decision

This page documents the design decision for how `AllocationScheduler` handles editing across multiple timeline granularities. The chosen approach is **Option A: Single Authoritative Level**.


## The Core Problem

AllocationScheduler supports five timeline zoom levels: Day, Week, Month, Quarter, and Year. A user could be viewing a monthly summary and want to enter or change a value. The question is: should that edit be allowed directly, and if so, how should the monthly value be reflected when the user zooms into weekly or daily view?

Time buckets at different zoom levels do not map cleanly to each other. A month does not contain exactly 4 weeks. Quarters do not divide evenly into months every time. Fiscal periods rarely align with calendar periods. This means translating a monthly edit into weekly sub-values is inherently a policy decision, not a math problem.


## Options Considered

### Option A — Single Authoritative Level (Selected)

One granularity is configured as the editing level via `AuthoritativeLevel`. All cells at that grain are writable. All coarser zoom levels are read-only aggregate sums. Zoom level changes are purely display changes — they never change the meaning or storage of any value.

**Advantages:**
- No ambiguity about what editing a "monthly value" means.
- The data model stays flat: one `AllocationRecord` type at one grain.
- Zoom changes are purely visual; no state mutation occurs on pan or zoom.
- Editing rules are simple: if `ViewGrain` is coarser than `AuthoritativeLevel`, cells are read-only.
- Shift, spread, and move operations always act on real stored records.
- Test coverage is straightforward because behavior is deterministic.

**Disadvantages:**
- A manager who thinks in monthly budgets must use bulk-fill or a distribution command rather than direct cell entry.
- Coarser zoom levels feel informational rather than interactive by default.

**Best fit for:** Operational staffing and resource scheduling, where the primary workflow is assigning actual hours to actual people at a fixed cadence.

---

### Option B — Multi-Level Editing with Distribution Policy (Not selected)

Values can be entered at any zoom level. The component distributes or aggregates values according to a configurable distribution policy when the user edits at a coarser grain.

**Advantages:**
- Supports top-down planning: set a quarterly budget and let the component spread it to months or weeks.
- Aligns with how senior planners and finance teams work.

**Disadvantages:**
- Significant implementation and state complexity.
- Distribution results can surprise users: entering 40 hours for March and seeing 8.7 hours on week 3 erodes trust.
- Round-trip editing (edit at month, drill to week, edit a week, return to month) creates ambiguous parent values.
- The data model needs a two-level structure: authoritative records plus derived records plus dirty flags.
- Undo/redo semantics become complex when one edit generates many downstream changes.

**Best fit for:** Top-down financial planning tools with fiscal calendars and portfolio-level budget entry.

---

## Selected Approach: Option A with Distribution Commands

The baseline is Option A. A single `AuthoritativeLevel` is required. Direct cell editing is only available at that grain.

However, coarser zoom cells are not passive. They are **command surfaces**. A right-click on a month rollup cell can invoke a distribution command that writes to the underlying authoritative records. This makes distribution an **explicit user-initiated transformation**, not a silent cascade.

The key principle: **making distribution explicit through a named command preserves trust**. The user chooses when and how a higher-level period maps to lower-level buckets. There are no surprising rewrites, and undo semantics remain simple because the distribution is a single committed operation.


## How Rollup Cells Work

When `ViewGrain` is coarser than `AuthoritativeLevel`:

- The cell displays the sum of all `AllocationRecord` values whose `BucketStart` falls within the coarser period.
- The cell is read-only for direct typing.
- The right-click context menu offers distribution commands:
  - **Distribute to [sub-grain] — Even spread**: divides the entered target evenly across sub-buckets.
  - **Distribute to [sub-grain] — Proportional**: preserves the relative weight of existing sub-values.
  - **Distribute to [sub-grain] — Working-days weighted**: weights by working days in each sub-bucket.
  - **Set desired total**: stores an `AllocationTarget` overlay without writing to allocation records.

When a distribution command is confirmed, the component fires `OnDistributeRequested`. The host can intercept, inspect the proposed records, override values, or cancel. If not cancelled, the component writes the resulting `AllocationRecord` set and fires `OnRangeEdited`.


## Worked Example

Setup: `AuthoritativeLevel = Week`, `ViewGrain = Month`.

The user wants to plan 100 hours across March for Alice Chen on the Backend API task.

1. User right-clicks the March cell for Alice / Backend API.
2. Context menu shows: **Distribute 100 hours to weeks — Even spread**.
3. User selects the command and enters 100 as the target total.
4. `OnDistributeRequested` fires with:
   - `SourcePeriod = March 2026`
   - `TargetValue = 100`
   - `TargetGranularity = Week`
   - `ProposedDistribution = { Week1: 20, Week2: 20, Week3: 20, Week4: 20, Week5: 20 }`
5. Host can accept or override the distribution.
6. On confirmation, five `AllocationRecord` objects are written (one per ISO week in March).
7. The March cell now shows 100 as the aggregate sum.
8. Zooming to `ViewGrain = Week` shows the five stored weekly values.


## Behavior Rules

| Scenario | Behavior |
| --- | --- |
| `ViewGrain == AuthoritativeLevel` | Cell is editable. Edit commits one `AllocationRecord`. |
| `ViewGrain` is coarser than `AuthoritativeLevel` | Cell is read-only. Shows aggregate sum. Context menu offers distribution commands. |
| `ViewGrain` is finer than `AuthoritativeLevel` | Cell is read-only. Shows the fraction of the authoritative bucket that falls within this finer period. No sub-bucket records exist unless `AuthoritativeLevel` was changed. |
| User drags across authoritative cells | Drag-fill applies to all touched cells in one `OnRangeEdited` event. |
| User drags across rollup cells | Drag not available on rollup cells by default. Context menu command must be used. |
| `AllowZoomEdit = true` | Opt-in override that allows direct typing in rollup cells. The component applies `DefaultDistributionMode` silently. Not recommended unless the consumer fully controls validation and undo. |


## API Summary

| Parameter | Type | Description |
| --- | --- | --- |
| `AuthoritativeLevel` | `TimeGranularity` | Required. The one level where cell editing is enabled. |
| `ViewGrain` | `TimeGranularity` | Current display level. Two-way bindable. |
| `DefaultDistributionMode` | `DistributionMode` | Policy applied when distributing via context menu command. |
| `AllowZoomEdit` | `bool` | Advanced opt-in for direct multi-level editing. Default `false`. |
| `OnDistributeRequested` | `EventCallback<DistributeArgs>` | Fires before distribution writes. Host can override or cancel. |


## Related

* [AllocationScheduler Overview](slug:allocation-scheduler-overview)
* [Context Menu Commands](slug:allocation-scheduler-context-menu)
* [AllocationScheduler Events](slug:allocation-scheduler-events)
