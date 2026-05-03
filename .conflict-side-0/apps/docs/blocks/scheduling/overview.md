---
uid: block-scheduling-overview
title: Scheduling — Overview
description: Introduction to the blocks-scheduling package — a thin view switcher over the Sunfish scheduler family.
keywords:
  - scheduler
  - calendar
  - allocation
  - day-week-month
  - blazor-block
---

# Scheduling — Overview

## Overview

The `blocks-scheduling` package exposes a single opinionated block — `ScheduleViewBlock<TResource>` — that picks between `SunfishScheduler` (Day / Week / Month) and `SunfishAllocationScheduler` (resource-allocation) based on a `ScheduleBlockView` parameter. The heavy lifting lives in the underlying scheduler components; this block is a thin, ergonomic view-switcher with opinionated defaults for demos and typical app shells.

## Package path

`packages/blocks-scheduling` — assembly `Sunfish.Blocks.Scheduling`.

## When to use it

- You want a single block in a kitchen-sink or demo page that can toggle between a day/week/month calendar and a resource allocation timeline.
- You need an ergonomic shim over the scheduler components that accepts all scheduler attributes passed through transparently.
- You are prototyping and do not yet know whether your domain wants a calendar view or a resource-allocation view.

If you need a single fixed mode (always Week, always Allocation), you can use the underlying component directly without going through this block.

## Key types

- **`ScheduleViewBlock<TResource>`** — Blazor block; dispatches on `View` to render either `SunfishScheduler` (for `Day`, `Week`, `Month`) or `SunfishAllocationScheduler<TResource>` (for `Allocation`).
- **`ScheduleBlockView`** — enum: `Day`, `Week`, `Month`, `Allocation`.

## Typing note

`ScheduleViewBlock` is generic over `TResource` because `SunfishAllocationScheduler` is generic. For consumers who only use `Day`, `Week`, or `Month`, `TResource` defaults to `object` so the type parameter never has to be specified. Consumers using `Allocation` mode should set the type argument explicitly to get the right resource shape.

## Unmatched-attribute forwarding

Any attributes not captured by `View` are forwarded verbatim to the underlying scheduler via `CaptureUnmatchedValues`. This means any `SunfishScheduler` or `SunfishAllocationScheduler` parameter — `Date`, `StartTime`, `SelectedView`, `Data`, events, render fragments — is accepted without extra wrapping.

## DI wiring

No DI registration is required. The block resolves to a Blazor component from the Sunfish adapters package, so the standard `AddSunfishBlazorUi()` registration is sufficient.

## Where things live in the package

| Path (under `packages/blocks-scheduling/`) | Purpose |
|---|---|
| `Models/ScheduleBlockView.cs` | `ScheduleBlockView` enum (Day/Week/Month/Allocation) with `follow-up` comment for Day-agenda/Timeline/Resource-timeline. |
| `ScheduleViewBlock.razor` | The dispatching Blazor component itself. |
| `_Imports.razor` | Global `using` directives for the component package. |
| `tests/ScheduleViewBlockTests.cs` | Type-level tests on publicness and the canonical enum set. |
| `Sunfish.Blocks.Scheduling.csproj` | Package manifest. |

## Minimal end-to-end example

```razor
@page "/scheduler-demo"
@using Sunfish.Blocks.Scheduling
@using Sunfish.Blocks.Scheduling.Models

<PageTitle>Scheduler</PageTitle>

<ScheduleViewBlock View="@_view"
                   Date="DateTime.Today"
                   StartTime="new DateTime(2026, 1, 1, 8, 0, 0)"
                   EndTime="new DateTime(2026, 1, 1, 18, 0, 0)"
                   Data="@_events" />

<button @onclick="() => _view = ScheduleBlockView.Day">Day</button>
<button @onclick="() => _view = ScheduleBlockView.Week">Week</button>
<button @onclick="() => _view = ScheduleBlockView.Month">Month</button>

@code {
    private ScheduleBlockView _view = ScheduleBlockView.Week;
    private IEnumerable<MyEvent> _events = Array.Empty<MyEvent>();

    private sealed record MyEvent(DateTime Start, DateTime End, string Title);
}
```

Switching `_view` toggles between day, week, and month layouts while holding every other parameter steady — the attribute forwarding keeps `Date`, `StartTime`, `EndTime`, and `Data` intact across mode changes.

## Resource allocation example

```razor
<ScheduleViewBlock TResource="Technician"
                   View="ScheduleBlockView.Allocation"
                   Resources="@_technicians"
                   Data="@_assignments" />

@code {
    private Technician[] _technicians = Array.Empty<Technician>();
    private Assignment[] _assignments = Array.Empty<Assignment>();

    public sealed record Technician(Guid Id, string Name);
    public sealed record Assignment(Guid TechnicianId, DateTime Start, DateTime End, string JobTitle);
}
```

Because `SunfishAllocationScheduler` is generic, setting `TResource="Technician"` on the block flows through to the underlying component and keeps the resource table strongly typed.

## Typical host integration

```razor
@* Layout.razor *@
<SunfishThemeProvider>
    <SunfishDrawer>
        <nav>...</nav>
    </SunfishDrawer>
    <main>
        @Body
    </main>
</SunfishThemeProvider>
```

```razor
@* Pages/Schedule.razor *@
@page "/schedule"
@using Sunfish.Blocks.Scheduling
@using Sunfish.Blocks.Scheduling.Models

<div class="toolbar">
    <button @onclick="() => _view = ScheduleBlockView.Day">Day</button>
    <button @onclick="() => _view = ScheduleBlockView.Week">Week</button>
    <button @onclick="() => _view = ScheduleBlockView.Month">Month</button>
    <button @onclick="() => _view = ScheduleBlockView.Allocation">Allocation</button>
</div>

<ScheduleViewBlock TResource="Technician"
                   View="_view"
                   Date="DateTime.Today"
                   Data="@_events"
                   Resources="@(_view == ScheduleBlockView.Allocation ? _technicians : null)" />

@code {
    private ScheduleBlockView _view = ScheduleBlockView.Week;
    private IEnumerable<CalendarEvent> _events = Array.Empty<CalendarEvent>();
    private Technician[] _technicians = Array.Empty<Technician>();
}
```

Passing `Resources` only in Allocation mode keeps the calendar modes from receiving a parameter they don't know about — though the underlying scheduler gracefully ignores extra attributes, the conditional keeps the code honest.

## Status and deferred items

- **Planned views** — the `ScheduleBlockView` enum file calls out three follow-up modes: Day-agenda, Timeline, and Resource-timeline. These are reserved names; no code path is wired for them yet.
- **No DI, no service contract** — the block is purely a presentation shim. Data shape, event model, selection, and edits all live on the underlying scheduler components.
- **Unmatched-attribute forwarding is untyped** — the `CaptureUnmatchedValues` dictionary is `IReadOnlyDictionary<string, object>`. Typos in attribute names do not raise a compile error; they simply don't appear on the rendered component.

## When *not* to use this block

- **You always want a calendar view.** Use `SunfishScheduler` directly — the view-switcher overhead buys you nothing.
- **You always want allocation view.** Use `SunfishAllocationScheduler<TResource>` directly — avoiding the dispatch also avoids the `object` default for `TResource`.
- **You need a non-scheduler timeline UI.** Neither the block nor the underlying components ship a freeform timeline; consider a Gantt-style third-party component.
- **You need to block or defer specific slots.** Slot-level validation lives on the underlying scheduler components, not on this block.

## Related

- [Service Contract](service-contract.md)
- Underlying components: `SunfishScheduler`, `SunfishAllocationScheduler` (ui-adapters-blazor).
- ADR 0022 — `docs/adrs/0022-example-catalog-and-docs-taxonomy.md`
