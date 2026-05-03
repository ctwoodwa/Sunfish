---
title: Data Binding
page_title: AllocationScheduler Data Binding
description: How to bind Resources, Allocations, and Targets to the AllocationScheduler, and how selector functions resolve keys.
slug: allocation-scheduler-data-binding
tags: sunfish,blazor,allocation-scheduler,data-binding,resources,allocations
published: True
position: 2
components: ["allocation-scheduler"]
---

# AllocationScheduler Data Binding

The `SunfishAllocationScheduler` is generic over `TResource`. It binds to three primary collections.

## Resources

The `Resources` parameter accepts any `IEnumerable<TResource>`. The component uses reflection to discover `Id` or `Key` properties for row identity, and `Name` or `Title` properties for display labels.

```razor
<SunfishAllocationScheduler TResource="StaffResource"
                           Resources="@Team"
                           ...>
```

## Allocations

The `Allocations` parameter accepts `IEnumerable<AllocationRecord>`. Each record links a resource, task, time bucket, and value.

| Property | Type | Description |
|---|---|---|
| AllocationId | `Guid` | Unique record identifier |
| ResourceId | `object` | Foreign key matching the resource's Id property |
| TaskId | `object` | Identifier for the task receiving the allocation |
| TaskName | `string` | Display name for the task |
| BucketStart | `DateTime` | Start of the time bucket |
| BucketEnd | `DateTime` | End of the time bucket |
| Value | `decimal` | Hours or currency amount |
| Unit | `AllocationUnit` | Hours or Currency |

### AllocationUnit

The `Unit` property on each `AllocationRecord` uses the `AllocationUnit` enum:

```csharp
public enum AllocationUnit
{
    Hours,    // Time-based allocation
    Currency  // Cost-based allocation
}
```

## Targets

The optional `Targets` parameter accepts `IEnumerable<AllocationTarget>` for delta analysis.

```razor
<SunfishAllocationScheduler TResource="StaffResource"
                           Resources="@Team"
                           Allocations="@Plan"
                           Targets="@DesiredTotals"
                           ShowDeltas="true"
                           ...>
```

## Two-Way Binding

The following parameters support two-way binding:

- `ViewGrain` / `ViewGrainChanged`
- `VisibleStart` / `VisibleStartChanged`
- `ActiveSetId` / `ActiveSetIdChanged`

```razor
<SunfishAllocationScheduler TResource="StaffResource"
                           Resources="@Team"
                           Allocations="@Plan"
                           ViewGrain="@_currentGrain"
                           ViewGrainChanged="@(g => _currentGrain = g)"
                           ...>
```
