---
uid: block-scheduling-service-contract
title: Scheduling — Service Contract
description: Parameter surface of ScheduleViewBlock and how it delegates to Sunfish scheduler components.
keywords:
  - scheduler
  - parameter-forwarding
  - blazor-splat
  - view-switcher
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

## Test surface

`tests/ScheduleViewBlockTests.cs` pins down the public shape rather than exercising a data fixture — the component itself is a thin dispatcher, so the test focus is on what is guaranteed stable:

```csharp
[Fact]
public void ScheduleViewBlock_TypeIsPublicAndInBlocksSchedulingNamespace()
{
    var type = typeof(ScheduleViewBlock<>);
    Assert.True(type.IsPublic);
    Assert.Equal("Sunfish.Blocks.Scheduling", type.Namespace);
}

[Fact]
public void ScheduleBlockView_HasFourCanonicalModes()
{
    var values = Enum.GetNames<ScheduleBlockView>();
    Assert.Contains("Day", values);
    Assert.Contains("Week", values);
    Assert.Contains("Month", values);
    Assert.Contains("Allocation", values);
}
```

These tests double as a promise: the four canonical modes are considered part of the public API and will not be renamed without a version bump.

## Attribute naming requirements

Because the forwarding uses `CaptureUnmatchedValues = true`, attribute names are matched to the underlying component's `[Parameter]` declarations at render time by Blazor. This means:

- **Case-sensitive** — `date` will not bind to `[Parameter] Date`; use `Date`.
- **Typos are silent** — an attribute named `Datw` will appear in the dictionary, get splatted to the rendered component, and quietly do nothing if the component has no matching parameter.
- **Two-way binding supported** — `@bind-SelectedView` style bindings also work through the forward, subject to Blazor's binding rules on the underlying component.

A reasonable convention is to alias the parameters you care about in your own wrapper:

```razor
@inherits ComponentBase
@using Sunfish.Blocks.Scheduling.Models

<ScheduleViewBlock View="@View"
                   Date="@Date"
                   Data="@Data" />

@code {
    [Parameter, EditorRequired] public IEnumerable<object> Data { get; set; } = default!;
    [Parameter] public DateTime Date { get; set; } = DateTime.Today;
    [Parameter] public ScheduleBlockView View { get; set; } = ScheduleBlockView.Week;
}
```

This gives you a typed entry point to the block and still lets you forward the open-ended remainder via a nested `CaptureUnmatchedValues`.

## Composing with theme switchers

The block has no theme parameter of its own. Theme switching is handled by the underlying `SunfishScheduler` / `SunfishAllocationScheduler`, which respect the ambient Sunfish theme provider. Wire `<SunfishThemeProvider>` in the host layout and both scheduler variants pick up the change automatically.

## Troubleshooting

- **"TResource is unspecified"** — you set `View = Allocation` but did not set `TResource`. Allocation mode requires an explicit resource type; Day/Week/Month tolerate the `object` default.
- **Attribute was ignored** — check casing. `Data` is different from `data` as far as Blazor parameter binding is concerned.
- **No data rendering** — verify the underlying scheduler's own parameter contract; the block is a dispatcher and does not validate forwarded attributes.

## Related

- [Overview](overview.md)
- Underlying components: `SunfishScheduler`, `SunfishAllocationScheduler` (ui-adapters-blazor).
