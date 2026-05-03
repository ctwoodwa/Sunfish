---
uid: block-inspections-property-extension
title: Inspections — Property-Operations Extension
description: InspectionTrigger enum, EquipmentConditionAssessment child entity, and move-in/out delta projection added per property-operations cluster workstream #25 (EXTEND disposition).
keywords:
  - sunfish
  - blocks
  - inspections
  - property-management
  - move-in
  - move-out
  - equipment
  - condition
  - delta
  - security-deposit
---

# Inspections — Property-Operations Extension

This page documents the property-operations cluster's extension to `Sunfish.Blocks.Inspections`
landed in workstream **#25** (EXTEND disposition per UPF Rule 3 — extend over parallel —
and Rule 4 — Equipment not Asset). The extension is purely additive; existing Inspection,
Deficiency, and report semantics are unchanged.

## Trigger types

The new `InspectionTrigger` enum on `Inspection.Trigger` (nullable for backward compat)
categorizes inspections by why they were scheduled:

| Trigger | When it applies |
|---|---|
| `Annual` | Routine annual inspection (default for property-management cadence). |
| `MoveIn` | Move-in baseline at lease start; documents unit + equipment condition before tenancy. Pairs with `MoveOut` in the delta projection. |
| `MoveOut` | Move-out delta at lease end; documents unit + equipment condition for security-deposit reconciliation. Pairs with `MoveIn`. |
| `PostRepair` | Verification inspection after maintenance/repair work; confirms work-order completion quality. |
| `OnDemand` | Ad-hoc inspection initiated by owner or contractor; not on a regular cadence. |

`Trigger` is optional — pre-extension inspections have `Trigger = null`. New code should
always set a trigger so the move-in / move-out delta projection can pair the right inspections.
Set on `ScheduleInspectionRequest.Trigger`; carries into the persisted `Inspection.Trigger`.

## Equipment condition assessments

`EquipmentConditionAssessment` is a child entity recorded against a specific inspection,
parallel to `Deficiency` but for proactive ongoing-condition rating of physical equipment
(water heaters, HVAC units, appliances) rather than for issues found.

Distinction:

- **Deficiency** — by definition something wrong (cracked tile, leaking faucet). Severity ∈
  `{Minor, Major, Critical}`; status ∈ `{Open, Acknowledged, Resolved}`.
- **EquipmentConditionAssessment** — proactive ongoing rating any equipment carries.
  Condition ∈ `{Good, Fair, Poor, Failed}`. The two coexist on the same inspection.

`EquipmentConditionAssessment.EquipmentId` references
`Sunfish.Blocks.PropertyEquipment.EquipmentId` — the equipment is owned by the property,
the condition assessment is owned by the inspection.

Service surface (additive on `IInspectionsService`):

```csharp
ValueTask<EquipmentConditionAssessment> RecordEquipmentConditionAsync(
    RecordEquipmentConditionRequest request,
    CancellationToken ct = default);

IAsyncEnumerable<EquipmentConditionAssessment> ListEquipmentConditionsAsync(
    InspectionId inspectionId,
    CancellationToken ct = default);

IAsyncEnumerable<EquipmentConditionAssessment> ListConditionHistoryForEquipmentAsync(
    EquipmentId equipmentId,
    CancellationToken ct = default);
```

`RecordEquipmentConditionAsync` requires the inspection to be in `InspectionPhase.InProgress`
(matches `RecordResponseAsync` semantics).

`ListConditionHistoryForEquipmentAsync` returns assessments oldest-first, useful for
"show me this water heater's condition trend across multiple annual inspections."

## Move-in / move-out delta

`GetMoveInOutDeltaAsync` pairs the **most recent** `MoveIn` inspection with the **most recent**
`MoveOut` inspection for a given unit and computes:

- `ResponseDeltas` — per-checklist-item before/after, with `Changed` flag (case-sensitive
  inequality). Items present in only one of the two inspections appear with the missing
  side's value as `string.Empty`.
- `EquipmentConditionDeltas` — per-equipment before/after, with `Degraded` flag (true when
  the move-out condition is worse than the move-in condition; uses the natural enum ordering
  `Good < Fair < Poor < Failed`). Only emits deltas for equipment present in BOTH inspections.

Returns `null` when either inspection is missing for the unit. Most-recent-pair semantics
are intentional for the first slice; tenancy-pairing (which move-in matches which move-out
across multiple tenancies on the same unit) is a Phase 2.2 enhancement gated on the leasing-pipeline state machine.

Consumed downstream by security-deposit reconciliation (Phase 2 commercial scope; lives in
`blocks-rent-collection` or `blocks-accounting`).

## What's not in this extension (deferred to follow-up hand-offs)

- **iOS walkthrough wizard** — gated on iOS Field-Capture App intake (#23). The trigger
  + condition primitives are usable from any UI; the iOS-specific wizard is a separate
  accelerator concern.
- **Move-in/out signature sign-off** — gated on signatures ADR (0054) acceptance and
  `kernel-signatures` package shipping. `Phase = Completed` doesn't yet bind to a
  SignatureEvent.
- **Photo blob storage** — `EquipmentConditionAssessment.PhotoBlobRefs` is a placeholder
  list of opaque strings; matches Deficiency's deferred-photo pattern. Real blob ingest
  is gated on Bridge blob-ingest API spec (cluster cross-cutting OQ3).
- **Security-deposit reconciliation calculation** — `MoveInOutDelta` provides the data;
  the actual deposit-vs-damage computation lives downstream.
- **Work-order rollup from failed condition assessment** — gated on workstream #19
  (Work Orders EXTEND to `blocks-maintenance`).

## Related ADRs

- [ADR 0008 — Foundation MultiTenancy](../../../docs/adrs/0008-foundation-multitenancy.md)
- [ADR 0054 — Electronic Signature Capture & Document Binding](../../../docs/adrs/0054-electronic-signature-capture.md) — gates the deferred move-in/out signature binding.

## Related intake

- [Property-Inspections cluster intake](../../../icm/00_intake/output/property-inspections-intake-2026-04-28.md)
- [Property-operations cluster vs existing reconciliation report](../../../icm/07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md) — workstream #25 row.
- [UPF naming review](../../../icm/07_review/output/property-ops-cluster-naming-upf-review-2026-04-28.md) — Rule 3 (extend over parallel) + Rule 4 (Equipment not Asset).
