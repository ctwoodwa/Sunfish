---
uid: block-inspections-service-contract
title: Inspections — Service Contract
description: The IInspectionsService public surface — templates, scheduling and executing inspections, recording responses and deficiencies, and generating summary reports.
keywords:
  - sunfish
  - inspections
  - service-contract
  - iinspectionsservice
  - lifecycle
---

# Inspections — Service Contract

## Overview

`IInspectionsService` is the single public contract for the inspections block. It is
framework-agnostic and has no UI coupling.

The contract is organised into four groups: templates, inspections (scheduling and
state), deficiencies, and reports. State transitions in the inspection-lifecycle methods
are enforced — see [state-transitions.md](state-transitions.md).

## Templates

```csharp
ValueTask<InspectionTemplate> CreateTemplateAsync(
    CreateTemplateRequest request, CancellationToken ct = default);

ValueTask<InspectionTemplate?> GetTemplateAsync(
    InspectionTemplateId id, CancellationToken ct = default);

IAsyncEnumerable<InspectionTemplate> ListTemplatesAsync(CancellationToken ct = default);
```

`CreateTemplateRequest` carries the template name, description, and ordered checklist items.

## Inspections

```csharp
ValueTask<Inspection> ScheduleAsync(
    ScheduleInspectionRequest request, CancellationToken ct = default);

ValueTask<Inspection> StartAsync(
    InspectionId id, CancellationToken ct = default);

ValueTask<Inspection> RecordResponseAsync(
    InspectionId id, InspectionResponse response, CancellationToken ct = default);

ValueTask<Inspection> CompleteAsync(
    InspectionId id, CancellationToken ct = default);

ValueTask<Inspection?> GetInspectionAsync(
    InspectionId id, CancellationToken ct = default);

IAsyncEnumerable<Inspection> ListInspectionsAsync(
    ListInspectionsQuery query, CancellationToken ct = default);
```

- `ScheduleAsync` always creates an inspection in `InspectionPhase.Scheduled`.
- `StartAsync` requires `Scheduled`. It transitions to `InProgress` and sets `StartedAtUtc`.
- `RecordResponseAsync` requires `InProgress`. It appends the response.
- `CompleteAsync` requires `InProgress`. It transitions to `Completed` and sets
  `CompletedAtUtc`.
- All three transition methods throw `InvalidOperationException` when the inspection is
  not in the expected phase.

## Deficiencies

```csharp
ValueTask<Deficiency> RecordDeficiencyAsync(
    RecordDeficiencyRequest request, CancellationToken ct = default);

IAsyncEnumerable<Deficiency> ListDeficienciesAsync(
    InspectionId inspectionId, CancellationToken ct = default);
```

Deficiencies are passive records in this pass. Transitions on `DeficiencyStatus` (e.g.
`Open → Acknowledged`) are deferred — there is no `UpdateDeficiencyStatus` method yet.

## Reports

```csharp
ValueTask<InspectionReport> GenerateReportAsync(
    InspectionId inspectionId, CancellationToken ct = default);
```

`GenerateReportAsync` can be called at any point but is most meaningful after the
inspection is `Completed`. The method throws `InvalidOperationException` when the
inspection does not exist.

## Typical workflow

1. **Define a template** once per inspection kind:
   `CreateTemplateAsync(new CreateTemplateRequest("Move-in", ..., items))`.
2. **Schedule an inspection** against a unit:
   `ScheduleAsync(new ScheduleInspectionRequest(templateId, unitId, "A. Park", 2026-04-22))`.
3. **Start** when the inspector arrives: `StartAsync(id)`.
4. **Record responses** as the inspector progresses:
   `RecordResponseAsync(id, new InspectionResponse(itemId, "yes", null))`.
5. **Record deficiencies** for failures:
   `RecordDeficiencyAsync(new RecordDeficiencyRequest(inspectionId, itemId, Severity.High, "Smoke detector missing"))`.
6. **Complete** when the inspector finishes: `CompleteAsync(id)`.
7. **Generate a report**: `GenerateReportAsync(id)`.

## Concurrency

`InMemoryInspectionsService` uses a per-inspection lock for response recording, so
concurrent `RecordResponseAsync` calls on the same inspection serialise rather than race.
The `ConcurrentRecordResponseAsync_OnSameInspection_AreSerializedNoLostResponses` test
fires 10 concurrent appends and asserts the final `Responses` list has exactly 10 entries.
Other endpoints protect shared dictionaries with the same strategy; callers can treat the
service as thread-safe under ordinary mixed workloads.

## Query filters

Several list endpoints accept filter records. Filter semantics:

| Query | Fields | Combinator |
|---|---|---|
| `ListInspectionsQuery` | `UnitId?`, `Phase?`, `InspectorName?`, `FromDate?`, `ToDate?` | AND across all non-null fields. |
| `ListInspectionsQuery.Empty` | — | Returns all inspections. |

The `FromDate` / `ToDate` pair bounds `ScheduledDate` inclusively; pass only one to leave
the other end open.

## Pass/fail heuristic

`GenerateReportAsync` walks the inspection's responses and scores each against the matching
checklist item's `Kind`:

- `YesNo` — `"yes"` counts as pass.
- `PassFail` — `"pass"` counts as pass.
- `Rating1to5` — numeric value `>= 3` counts as pass.
- `FreeText`, `Photo` — any non-empty value counts as pass.

`DeficiencyCount` is the total number of deficiencies linked to the inspection (any
`Severity`, any `Status`). Reports are read-through: regenerate to reflect new responses or
deficiencies.

## Default implementation

`InMemoryInspectionsService` is registered by `AddInMemoryInspections`. State is held in
process; replace with a persistence-backed implementation for production. The in-memory
service keeps dictionaries keyed by each identifier type plus a per-inspection lock
dictionary for response appends.

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- [State Transitions](state-transitions.md)
