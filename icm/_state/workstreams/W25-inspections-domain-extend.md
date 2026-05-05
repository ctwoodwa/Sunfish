---
sort_order: 24
number: 25
slug: inspections-domain-extend
title: "Inspections domain (cluster module) — **EXTEND `blocks-inspections`**"
status: "built"
status_cell: "`built` (extension shipped)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "https://github.com/ctwoodwa/Sunfish/pull/222"
---

## Notes

**Extension shipped 2026-04-29 (PR #222).** Adds (additive; no behavior change to existing Inspection lifecycle): `InspectionTrigger` enum + `Inspection.Trigger` nullable field; `EquipmentConditionAssessment` child entity (parallel to Deficiency; references `Sunfish.Blocks.PropertyEquipment.EquipmentId`); `RecordEquipmentConditionAsync` + `ListEquipmentConditionsAsync` + `ListConditionHistoryForEquipmentAsync` + `GetMoveInOutDeltaAsync` on `IInspectionsService`; `MoveInOutDelta` + `ResponseDelta` + `EquipmentConditionDelta` projections for security-deposit reconciliation. 36/36 tests passing (existing 21 + 15 new). All 4 hand-off OQs resolved per recommendations. Phase 5 (kitchen-sink seed) deferred — no precedent for blocks-into-kitchen-sink wiring (matches Properties + Equipment first-slice pattern). iOS walkthrough wizard + signature sign-off + photo blob storage deferred to follow-up hand-offs.
