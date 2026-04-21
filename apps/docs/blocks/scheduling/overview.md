---
uid: block-scheduling-overview
title: Scheduling — Overview
description: Introduction to the blocks-scheduling package — a thin view switcher over the Sunfish scheduler family.
---

# Scheduling — Overview

## Overview

The `blocks-scheduling` package exposes a single opinionated block — `ScheduleViewBlock` — that picks between `SunfishScheduler` (Day / Week / Month) and `SunfishAllocationScheduler` (resource-allocation) based on a `ScheduleBlockView` parameter. The heavy lifting lives in the underlying scheduler components; this block is a thin, ergonomic view-switcher with opinionated defaults for demos and typical app shells.

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

## Related

- [Service Contract](service-contract.md)
