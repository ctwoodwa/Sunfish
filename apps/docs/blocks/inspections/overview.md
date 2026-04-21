---
uid: block-inspections-overview
title: Inspections — Overview
description: Inspection templates, scheduled inspections, deficiency tracking, and summary report generation for Sunfish-hosted property apps.
---

# Inspections — Overview

## What this block is

`Sunfish.Blocks.Inspections` is the domain block for recording unit and property
inspections. It ships four bounded responsibilities:

1. **Templates** — reusable checklist definitions (e.g. "Move-in inspection",
   "Annual HVAC inspection").
2. **Inspections** — a scheduled execution of a template against a specific unit by a named
   inspector.
3. **Deficiencies** — passive records of issues observed during an inspection.
4. **Reports** — computed, snapshot summaries of inspection outcomes.

The first pass is a clean domain surface. Explicitly deferred:

- Work-order auto-rollup from deficiencies (will live in `blocks-maintenance` second pass).
- Offline mobile capture and photo/voice attachments.
- Event-bus integration (so inspections and deficiencies can drive downstream workflow).
- `BusinessRuleEngine` hookup.

## Package

- Package: `Sunfish.Blocks.Inspections`
- Source: `packages/blocks-inspections/`
- Namespace roots:
  - `Sunfish.Blocks.Inspections.Models`
  - `Sunfish.Blocks.Inspections.Services`
  - `Sunfish.Blocks.Inspections.DependencyInjection`
- Razor components: `InspectionListBlock.razor`

## When to use it

Use this block when your app needs:

- A canonical way to describe an inspection-style checklist (yes/no, pass/fail, 1-5 rating,
  free text, photo placeholder).
- A state-enforced workflow for executing inspections: `Scheduled → InProgress → Completed`.
- A place to record deficiencies against a completed or in-progress inspection.
- A zero-work summary report renderer.

## Key entities and services

- `InspectionTemplate` + `InspectionChecklistItem` — template + items.
- `Inspection` + `InspectionResponse` + `InspectionPhase` — scheduled execution and responses.
- `Deficiency` + `DeficiencySeverity` + `DeficiencyStatus` — issue records.
- `InspectionReport` — computed outcome snapshot.
- `IInspectionsService` — single write/read contract.
- `InspectionListBlock` — Blazor component for rendering a list of inspections.

See [entity-model.md](entity-model.md), [service-contract.md](service-contract.md), and
[state-transitions.md](state-transitions.md).

## DI wiring

```csharp
using Sunfish.Blocks.Inspections.DependencyInjection;

services.AddInMemoryInspections();
```

Registers `InMemoryInspectionsService` as the singleton `IInspectionsService`. Suitable for
development, tests, and demos — replace with a persistence-backed implementation for
production.

## Related ADRs

- ADR 0015 — Module-Entity Registration (for the eventual persistence-backed implementation).

## Related pages

- [Entity Model](entity-model.md)
- [Service Contract](service-contract.md)
- [State Transitions](state-transitions.md)
