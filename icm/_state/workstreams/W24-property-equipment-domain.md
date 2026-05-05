---
sort_order: 23
number: 24
slug: property-equipment-domain
title: "Property-Equipment domain (cluster module; **renamed from Property-Assets per UPF Rule 4**)"
status: "built"
status_cell: "`built` (first-slice + Equipment rename)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "https://github.com/ctwoodwa/Sunfish/pull/213 (first-slice merged 2026-04-29 00:22Z as `blocks-property-assets`) + https://github.com/ctwoodwa/Sunfish/pull/216 (Equipment rename)"
---

## Notes

First-slice shipped as `blocks-property-assets` + `Sunfish.Blocks.PropertyAssets.Asset` (PR #213). **Equipment rename shipped 2026-04-28 (PR #216)** per UPF Rule 4: `Asset` overloaded `Sunfish.Foundation.Assets.Common.EntityId` (foundation-tier generic-entity term); cluster's physical-equipment entity now named `Equipment` (industry-standard for facilities management). Mechanical refactor: package renamed → `blocks-property-equipment`, namespace → `Sunfish.Blocks.PropertyEquipment`, entity types → `Equipment*`. 33/33 tests still passing post-rename. `Sunfish.Blocks.PropertyEquipment.Equipment` is now the canonical entity name. After this rename, follow-up hand-offs (Vehicle/Trip events + EquipmentConditionAssessment integration + OCR ingest) build on `Equipment` canonical name.
