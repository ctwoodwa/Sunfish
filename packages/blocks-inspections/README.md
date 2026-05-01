# Sunfish.Blocks.Inspections

Block for property-inspection management — templates, scheduled inspections, state transitions, deficiency tracking, equipment-condition assessment, and report generation.

Cluster module under the property-operations cluster; **EXTEND target** for the W#25 inspections workstream (extension shipped 2026-04-29 in PR #222 per the cluster reconciliation).

## What this ships

### Models

- **`InspectionTemplate`** — reusable inspection template (e.g., "Move-in", "Quarterly", "Annual").
- **`Inspection`** — scheduled or completed inspection entity (`IMustHaveTenant`); tracks `Trigger` (MoveIn / MoveOut / Routine / Complaint / etc.), `Status`, scheduled/completed timestamps.
- **`InspectionResponse`** — per-template-question answer.
- **`Deficiency`** — child entity for issues identified during the inspection; severity + remediation status.
- **`EquipmentConditionAssessment`** — child entity referencing `Sunfish.Blocks.PropertyEquipment.EquipmentId`; condition projection + notes (W#25 extension).

### Services

- **`IInspectionsService`** — full lifecycle: scheduling, completion, deficiency recording, equipment-condition recording, projections (`MoveInOutDelta` / `ResponseDelta` / `EquipmentConditionDelta`) for security-deposit reconciliation.
- **`InMemoryInspectionsService`** — reference impl.
- **Projections:**
  - `MoveInOutDelta` — per-listing move-in vs move-out comparison (security deposit basis)
  - `ResponseDelta` — per-question answer changes
  - `EquipmentConditionDelta` — equipment-condition transitions tracked over inspection history

## Cluster role

Per the 2026-04-29 reconciliation, this block is the **EXTEND target** for the W#25 cluster module rather than a new package. The W#25 extension added `InspectionTrigger`, `Inspection.Trigger?`, `EquipmentConditionAssessment`, 4 new service methods, and 3 projections.

## Deferred follow-ups

- iOS walkthrough wizard (W#23 territory)
- Photo-blob storage integration
- Signature sign-off (ADR 0054 consumer)
- Kitchen-sink seed page

## ADR map

- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration pattern
- [ADR 0053](../../docs/adrs/0053-work-order-domain-model.md) — work-order coordination (Equipment cross-reference)
- Property-ops cluster reconciliation: `icm/07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md` §#25

## See also

- [apps/docs Overview](../../apps/docs/blocks/inspections/overview.md)
- [Sunfish.Blocks.PropertyEquipment](../blocks-property-equipment/README.md) — `EquipmentId` FK target
- [Sunfish.Blocks.Properties](../blocks-properties/README.md) — `PropertyId` FK target
