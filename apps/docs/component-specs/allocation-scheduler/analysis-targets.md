---
title: Analysis and Targets
page_title: AllocationScheduler - Analysis and Targets
description: Compare actual allocations against desired totals using AllocationTarget records, delta display modes, and status coloring.
slug: allocation-scheduler-analysis-targets
tags: sunfish,blazor,allocation-scheduler,analysis,targets,variance
published: True
position: 30
components: ["allocation-scheduler"]
---

# AllocationScheduler Analysis and Targets

The `SunfishAllocationScheduler` supports variance analysis between actual allocation values and desired totals. Targets are **overlay records** — they never mutate the authoritative `AllocationRecord` collection, so you can enable, disable, and compare targets at runtime without touching the underlying plan.

## When to Use Targets

Typical scenarios:

- **Budget vs actual** — planners set a quarterly hour or currency budget per resource; the scheduler surfaces over- and under-allocated resources in real time.
- **Capacity planning** — set a weekly capacity target per role (e.g. 32 hours for a 4-day week after PTO); the component highlights overcommitted staff.
- **Baseline tracking** — snapshot the initial plan into `Targets`, then edit the live plan; the delta display shows drift without losing either version.

## AllocationTarget Entity

```csharp
public class AllocationTarget
{
    public string          Id          { get; set; }
    public int?            ResourceId  { get; set; }   // null = applies to all resources
    public int?            TaskId      { get; set; }   // null = applies to all tasks
    public DateTime        PeriodStart { get; set; }
    public DateTime        PeriodEnd   { get; set; }
    public double          Value       { get; set; }
    public AllocationUnit  Unit        { get; set; }
}
```

A target record describes a **desired total** for a slice of the allocation plan. Slices can be narrow (one resource, one task, one week) or broad (all resources, all tasks, one quarter). The component resolves the most specific target first: a `task + resource + week` target wins over a `task + week` target, which wins over a `resource` target.

## Wiring Targets to the Component

```razor
<SunfishAllocationScheduler Resources="@Team"
                             Allocations="@Plan"
                             Targets="@Budgets"
                             ShowTargets="true"
                             ShowDeltas="true"
                             DeltaDisplayMode="DeltaDisplayMode.Percentage"
                             AuthoritativeLevel="TimeGranularity.Week" />
```

Binding `Targets` activates three features:

1. **Target overlay** — the target value renders as a secondary label in each matching cell (controlled by `ShowTargets`).
2. **Delta display** — the difference between actual and target renders alongside the cell value (controlled by `ShowDeltas`).
3. **Status coloring** — cells receive `over-allocated`, `under-allocated`, or `on-target` status classes based on the variance and your configured thresholds.

## Delta Display Modes

The `DeltaDisplayMode` parameter controls how variance is presented:

| Mode          | Example       | Use case                                                       |
| ------------- | ------------- | -------------------------------------------------------------- |
| `Value`       | `+4h`         | When stakeholders think in raw hours or currency.              |
| `Percentage`  | `+13%`        | When proportional over/under matters more than absolute.       |
| `StatusIcon`  | coloured icon | Compact summary views and executive dashboards.                |

## Variance Thresholds

Status classes are assigned by comparing each cell's delta against two thresholds:

- **On-target tolerance** — deltas within ± this value render as `on-target`.
- **Critical threshold** — deltas above this value render as `critical-over` or `critical-under`; smaller deviations render as `mild-over` or `mild-under`.

The default tolerance is `0.01` (effectively exact match). Override via a provider-level theme token, or per-instance via a CSS custom property on the component's root element.

## Events

| Event              | When it fires                                                                                                                                                  |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OnTargetChanged`  | A target value is set or updated through the context menu (`Set desired total for task` / `Set desired total for resource`). The host persists the new record. |

Targets are never edited directly in a cell. All target edits go through the context menu or a programmatic API call, so variance stays deterministic and auditable.

## Programmatic API

```csharp
// From a component reference
await schedulerRef.SetTargetAsync(new AllocationTarget { /* ... */ });
await schedulerRef.RemoveTargetAsync(targetId);
IReadOnlyList<AllocationTarget> current = schedulerRef.GetTargets();
```

## Demo Scenarios

1. **Fixed weekly capacity** — set a per-resource weekly target of 32 hours, load a plan that over-allocates one resource, confirm the over-allocated cells render red.
2. **Quarterly budget** — set a per-task quarterly target of 500 hours; plan spans two quarters; confirm each quarter's rollup displays its own delta independently.
3. **Baseline comparison** — snapshot an initial plan into `Targets`, edit the live plan, toggle `ShowDeltas` on and off to compare before and after without mutating the baseline.
4. **Percentage vs value mode** — switch `DeltaDisplayMode` between `Value` and `Percentage`, confirm labels update without remounting the component.
5. **Status icon compact view** — switch `DeltaDisplayMode` to `StatusIcon`, confirm only coloured icons render and no numeric delta is shown.

## See Also

- [AllocationScheduler Overview](slug:allocation-scheduler-overview)
- [AllocationScheduler Events](slug:allocation-scheduler-events)
- [Context Menu](slug:allocation-scheduler-context-menu)
- [Scenario Planning](slug:allocation-scheduler-scenario-planning)
