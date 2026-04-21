---
uid: block-inspections-entity-model
title: Inspections — Entity Model
description: Templates, checklist items, inspections, responses, deficiencies, and reports — the entities that make up Sunfish.Blocks.Inspections.
keywords:
  - sunfish
  - inspections
  - entity-model
  - inspection-template
  - deficiency
  - inspection-report
---

# Inspections — Entity Model

## Overview

The inspections block defines five top-level records and several supporting enums. All
records are immutable value types; state changes happen through `IInspectionsService`
methods which return new records.

## InspectionTemplate

A reusable checklist definition.

| Field           | Type                                   | Notes |
|-----------------|----------------------------------------|-------|
| `Id`            | `InspectionTemplateId`                 | Unique identifier. |
| `Name`          | `string`                               | Human-readable template name. |
| `Description`   | `string?`                              | Optional description. |
| `Items`         | `IReadOnlyList<InspectionChecklistItem>` | Ordered checklist items. |
| `CreatedAtUtc`  | `Instant`                              | Creation timestamp. |

### InspectionChecklistItem

| Field      | Type                          | Notes |
|------------|-------------------------------|-------|
| `Id`       | `InspectionChecklistItemId`   | Unique identifier. |
| `Prompt`   | `string`                      | Inspector-facing prompt. |
| `Kind`     | `InspectionItemKind`          | Expected response shape. |
| `Required` | `bool`                        | When `true`, a response is required before completing the inspection. |

`InspectionItemKind`: `YesNo`, `PassFail`, `Rating1to5`, `FreeText`, `Photo` (photo is a
placeholder pending mobile capture work).

## Inspection

A single execution of a template against a unit.

| Field            | Type                               | Notes |
|------------------|------------------------------------|-------|
| `Id`             | `InspectionId`                     | Unique identifier. |
| `TemplateId`     | `InspectionTemplateId`             | The template that defines the checklist. |
| `UnitId`         | `EntityId`                         | The unit being inspected (e.g. `unit:acme/3B`). |
| `InspectorName`  | `string`                           | Display name of the inspector. |
| `ScheduledDate`  | `DateOnly`                         | Scheduled calendar date. |
| `Phase`          | `InspectionPhase`                  | `Scheduled`, `InProgress`, `Completed`, `Cancelled`. |
| `StartedAtUtc`   | `Instant?`                         | Set on transition to `InProgress`. |
| `CompletedAtUtc` | `Instant?`                         | Set on transition to `Completed`. |
| `Responses`      | `IReadOnlyList<InspectionResponse>`| Appended as the inspector records answers. |

### InspectionResponse

A response is always stringified regardless of the underlying `InspectionItemKind`:

| Kind         | Stored value                                              |
|--------------|-----------------------------------------------------------|
| `YesNo`      | `"yes"` or `"no"`                                         |
| `PassFail`   | `"pass"` or `"fail"`                                      |
| `Rating1to5` | `"1"` through `"5"`                                       |
| `FreeText`   | Inspector-supplied text                                   |
| `Photo`      | Placeholder string (e.g. `"[photo-deferred]"`)            |

| Field            | Type                         | Notes |
|------------------|------------------------------|-------|
| `ItemId`         | `InspectionChecklistItemId`  | The checklist item this answers. |
| `ResponseValue`  | `string`                     | See table above. |
| `Notes`          | `string?`                    | Optional inspector notes. |

## Deficiency

A passive record of an issue observed during an inspection.

| Field           | Type                          | Notes |
|-----------------|-------------------------------|-------|
| `Id`            | `DeficiencyId`                | Unique identifier. |
| `InspectionId`  | `InspectionId`                | The parent inspection. |
| `ItemId`        | `InspectionChecklistItemId`   | The checklist item that triggered the deficiency. |
| `Severity`      | `DeficiencySeverity`          | `Low`, `Medium`, `High`, `Critical`. |
| `Description`   | `string`                      | Human-readable description. |
| `ObservedAtUtc` | `Instant`                     | When the deficiency was recorded. |
| `Status`        | `DeficiencyStatus`            | `Open`, `Acknowledged`, `Resolved`, `Deferred`. |

Work-order rollup from a deficiency (via `blocks-maintenance`) is deferred.

## InspectionReport

A computed, snapshot summary of an inspection's outcome.

| Field              | Type                   | Notes |
|--------------------|------------------------|-------|
| `Id`               | `InspectionReportId`   | Unique identifier. |
| `InspectionId`     | `InspectionId`         | The inspection this report summarises. |
| `GeneratedAtUtc`   | `Instant`              | When the report was generated. |
| `Summary`          | `string`               | Human-readable summary text. |
| `TotalItems`       | `int`                  | Total items in the template. |
| `PassedItems`      | `int`                  | Items with a "passing" response (see heuristic below). |
| `DeficiencyCount`  | `int`                  | Total deficiencies linked to the inspection. |

### Pass/fail heuristic

`PassedItems` counts a response as passing when:

- `YesNo` = `"yes"`
- `PassFail` = `"pass"`
- `Rating1to5` ≥ 3
- `FreeText` or `Photo` = any non-empty value

## Relationships

```
InspectionTemplate 1 ─── N InspectionChecklistItem

InspectionTemplate 1 ─── N Inspection           (by TemplateId)
Inspection         1 ─── N InspectionResponse   (appended over time)
Inspection         1 ─── N Deficiency
Inspection         1 ─── N InspectionReport     (multiple reports possible)
```

## Strong-typed identifiers

Each record has a dedicated identifier struct with a `NewId()` factory:

- `InspectionTemplateId`
- `InspectionChecklistItemId`
- `InspectionId`
- `DeficiencyId`
- `InspectionReportId`

Identifiers are opaque string values. The in-memory service uses GUIDs internally; a
persistence-backed implementation is free to use any collision-resistant scheme.
`InspectionChecklistItem`'s identifier is caller-controlled so the template author can
cross-reference items from seed scripts.

## Immutability and transitions

All records are immutable. Each lifecycle change on an `Inspection` returns a _new_
`Inspection` instance with the updated phase and timestamps. Callers must treat the
returned record as the current state — mutating the local copy (e.g. casting the
`Responses` list) is not supported.

`RecordResponseAsync` appends to the responses list by producing a new record whose
`Responses` list contains the old elements plus the new one. Under concurrent calls the
in-memory service serialises append operations so there are no lost updates; the
`ConcurrentRecordResponseAsync` test asserts 10 parallel appends all land.

## Usage example (drawn from tests)

```csharp
// Build a reusable template request
var request = new CreateTemplateRequest
{
    Name        = "Standard Move-In",
    Description = "Standard move-in checklist",
    Items       =
    [
        new InspectionChecklistItem(InspectionChecklistItemId.NewId(),
            "Smoke detector operational?", InspectionItemKind.YesNo, Required: true),
        new InspectionChecklistItem(InspectionChecklistItemId.NewId(),
            "Water heater functional?", InspectionItemKind.PassFail, Required: true),
        new InspectionChecklistItem(InspectionChecklistItemId.NewId(),
            "Overall cleanliness rating", InspectionItemKind.Rating1to5, Required: false),
    ],
};

var svc      = new InMemoryInspectionsService();
var template = await svc.CreateTemplateAsync(request);
```

## Related pages

- [Overview](overview.md)
- [Service Contract](service-contract.md)
- [State Transitions](state-transitions.md)
