---
uid: block-scheduling-service-contract
title: Scheduling — Service Contract
description: Parameter surface of ScheduleViewBlock and how it delegates to Sunfish scheduler components.
---

# Scheduling — Service Contract

## Overview

`blocks-scheduling` does not expose a domain service interface — it is a presentation-layer block that delegates to the Sunfish scheduler family. This page documents the parameter surface of `ScheduleViewBlock<TResource>` and how it composes the underlying components.

## Public surface

`ScheduleViewBlock<TResource>` has two declared parameters plus an unmatched-values catch-all:

| Parameter | Type | Purpose |
|---|---|---|
| `View` | `ScheduleBlockView` | View mode — one of `Day`, `Week`, `Month`, `Allocation`. Defaults to `Week`. |
| `ForwardedAttributes` | `IReadOnlyDictionary<string, object>?` | Populated by `CaptureUnmatchedValues = true`. Every attribute not bound to `View` ends up here and is splatted onto the underlying component with `@attributes`. |

## Dispatch table

```razor
@switch (View)
{
    case ScheduleBlockView.Allocation:
        <SunfishAllocationScheduler TResource="TResource" @attributes="ForwardedAttributes" />
        break;

    case ScheduleBlockView.Day:
    case ScheduleBlockView.Week:
    case ScheduleBlockView.Month:
    default:
        <SunfishScheduler @attributes="ForwardedAttributes" />
        break;
}
```

The dispatch is intentionally exhaustive over `ScheduleBlockView`. Unknown values fall through to the `default` case and render `SunfishScheduler`.

## Typical workflow

```razor
<ScheduleViewBlock View="ScheduleBlockView.Week"
                   Date="DateTime.Today"
                   StartTime="new DateTime(2026, 1, 1, 8, 0, 0)"
                   EndTime="new DateTime(2026, 1, 1, 18, 0, 0)"
                   Data="@events" />
```

For allocation mode with a concrete resource type:

```razor
<ScheduleViewBlock TResource="Technician"
                   View="ScheduleBlockView.Allocation"
                   Resources="@technicians"
                   Data="@assignments" />
```

## Planned extensions

Per the `ScheduleBlockView` comment, the follow-up work is to add:

- **Day-agenda** — single-day list view.
- **Timeline** — horizontal timeline.
- **Resource-timeline** — resource-grouped timeline variant.

These are modelled as planned enum additions; no code is wired for them yet.

## Related

- [Overview](overview.md)
