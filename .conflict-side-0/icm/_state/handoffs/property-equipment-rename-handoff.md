# Hand-off — Rename `blocks-property-assets` → `blocks-property-equipment`

**From:** research session
**To:** sunfish-PM session
**Created:** 2026-04-28
**Status:** `ready-to-build`
**Spec source:** [`property-ops-cluster-naming-upf-review-2026-04-28.md`](../../07_review/output/property-ops-cluster-naming-upf-review-2026-04-28.md) Rule 4 + [`property-ops-cluster-vs-existing-reconciliation-2026-04-28.md`](../../07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md) workstream #24 row.
**Approval:** User accepted default recommendations from UPF review 2026-04-28: cluster's "Asset" entity overloads foundation-tier `Sunfish.Foundation.Assets.Common.EntityId`. Rename to `Equipment` (industry-standard for facilities management; covers HVAC + structural + appliances + vehicles).
**Estimated cost:** ~1–2 hours sunfish-PM (mechanical rename; no behavior change)
**Pipeline:** `sunfish-api-change` (public namespace + entity name rename — breaking for any consumer; though no consumers yet)
**Predecessor:** PR #213 — `blocks-property-assets` first-slice shipped without rename (research-session reconciliation hadn't landed yet); this hand-off retroactively applies Rule 4.

---

## Context (one paragraph)

The shipped `Sunfish.Blocks.PropertyAssets` package (PR #213) uses entity name `Asset`. Per the UPF review's Rule 4, "Asset" overloads `Sunfish.Foundation.Assets.Common.EntityId` (the foundation-tier generic-entity reference). That overload makes "Asset" ambiguous in cluster artifacts and downstream code. **Rename**: package → `blocks-property-equipment`; namespace → `Sunfish.Blocks.PropertyEquipment`; entity types → `Equipment*`. Mechanical refactor; no behavior change. After this hand-off, any cluster module that referenced `AssetId` (Receipts hand-off; ADR 0053 work-order field) will follow up to update its references.

---

## Phases (binary gates)

### Phase 1 — Directory rename + csproj update

**Files:**

- `git mv packages/blocks-property-assets/ packages/blocks-property-equipment/`
- `packages/blocks-property-equipment/Sunfish.Blocks.PropertyAssets.csproj` → rename to `Sunfish.Blocks.PropertyEquipment.csproj` AND edit:
  - `<PackageId>Sunfish.Blocks.PropertyAssets</PackageId>` → `<PackageId>Sunfish.Blocks.PropertyEquipment</PackageId>`
  - `<AssemblyName>` if specified → `Sunfish.Blocks.PropertyEquipment`
  - `<RootNamespace>` if specified → `Sunfish.Blocks.PropertyEquipment`
  - `<Description>` text "asset" / "Asset" → "equipment" / "Equipment" where it refers to property-management physical equipment
- Tests project: `packages/blocks-property-equipment/tests/Sunfish.Blocks.PropertyAssets.Tests.csproj` → rename to `Sunfish.Blocks.PropertyEquipment.Tests.csproj`; same csproj-edit pattern
- Update `Sunfish.slnx`: replace all `Sunfish.Blocks.PropertyAssets` references with `Sunfish.Blocks.PropertyEquipment`; replace path `blocks-property-assets` → `blocks-property-equipment`

**PASS gate:** `dotnet build` green; package id + paths consistent.

### Phase 2 — Namespace + type renames in all `.cs` files

**Mechanical rename across all files in the package** (production + tests):

| Before | After |
|---|---|
| `namespace Sunfish.Blocks.PropertyAssets` | `namespace Sunfish.Blocks.PropertyEquipment` |
| `using Sunfish.Blocks.PropertyAssets` | `using Sunfish.Blocks.PropertyEquipment` |
| `Sunfish.Blocks.PropertyAssets.` (qualified refs) | `Sunfish.Blocks.PropertyEquipment.` |
| Type `Asset` | `Equipment` |
| Type `AssetId` | `EquipmentId` |
| Type `AssetClass` | `EquipmentClass` |
| Type `AssetLifecycleEvent` | `EquipmentLifecycleEvent` |
| Type `AssetLifecycleEventType` | `EquipmentLifecycleEventType` |
| Type `IAssetRepository` | `IEquipmentRepository` |
| Type `InMemoryAssetRepository` | `InMemoryEquipmentRepository` |
| Type `AssetEntityModule` | `EquipmentEntityModule` |
| Type `IAssetLifecycleEventStore` | `IEquipmentLifecycleEventStore` |
| Type `InMemoryAssetLifecycleEventStore` | `InMemoryEquipmentLifecycleEventStore` |
| DI extension method `AddAssetBlock` | `AddEquipmentBlock` |
| File names matching the renamed types | rename file to match (`AssetId.cs` → `EquipmentId.cs`, etc.) |

**Constraint:** **DO NOT rename references to `Sunfish.Foundation.Assets.Common.EntityId` or other `Foundation.Assets` symbols.** Foundation-tier "Asset" is a different namespace — generic-entity model. Keep foundation references intact. (If any `Asset.cs` file imports `Sunfish.Foundation.Assets.Common`, that import stays; only the cluster's own `Asset` types rename.)

**Constraint:** XML doc strings that say "asset" in property-management sense → "equipment". XML doc strings that reference foundation-tier Asset (rare; flag if present) stay.

**Tools:** Use a combination of `find . -name "*.cs"` + targeted `sed` or `Edit` calls. Avoid blanket repo-wide rename — limit scope to `packages/blocks-property-equipment/` and any consumer (none yet, but verify).

**PASS gate:** `dotnet build packages/blocks-property-equipment/` green; `dotnet test packages/blocks-property-equipment/tests/` green; provider-neutrality analyzer passes.

### Phase 3 — Audit consumers

Run `grep -rn "Sunfish.Blocks.PropertyAssets" .` repo-wide. Every reference outside `packages/blocks-property-equipment/` must update:

- `apps/kitchen-sink/` seed code (if seed referenced the old namespace)
- `apps/docs/blocks/assets.md` → rename file to `equipment.md`; update all "asset" → "equipment" in property-management context; update doc nav config if present
- Any block that consumes `IAssetRepository` (Receipts? Inspections? — unlikely yet, but check)

**PASS gate:** zero `Sunfish.Blocks.PropertyAssets` references remain anywhere in repo (use `grep -rn "PropertyAssets" .` to confirm; the only allowed result is `Foundation.Assets` matches which are different).

### Phase 4 — Workstream ledger flip

**File:** `icm/_state/active-workstreams.md`

Update workstream #24 row:
- Reference: append " + this rename PR's link"
- Notes: append "Renamed package + namespace + entity types per UPF Rule 4 (PR #214). `Sunfish.Blocks.PropertyEquipment.Equipment` is now the canonical entity name."

**PASS gate:** ledger updated; PR ready to merge.

---

## What sunfish-PM should NOT touch

- `packages/foundation-assets-postgres/` (foundation-tier; different namespace; keep as-is)
- `packages/blocks-assets/` (existing UI catalog block; different scope; keep as-is)
- `packages/blocks-properties/` (cluster root; already shipped; no rename)
- Any `using Sunfish.Foundation.Assets.Common;` import (foundation-tier; legitimate)
- Any consumer block that hasn't been written yet (Receipts hand-off updates `AssetRef` → `EquipmentRef` separately when sunfish-PM picks up #26)

---

## Open questions

| ID | Question | Resolution |
|---|---|---|
| OQ-EQ1 | If any `XML doc` text describes the cluster Asset by the word "asset" in lowercase prose (e.g., "this asset's serial number"), rename to "equipment"? | Yes — match the entity-name change in prose. |
| OQ-EQ2 | If a test file is named `AssetTests.cs`, rename to `EquipmentTests.cs`? | Yes — file-name parity with type. |
| OQ-EQ3 | Is there any existing seed data with hard-coded JSON-serialized `"AssetClass"` enum values? | Unlikely (block just shipped); but check kitchen-sink seed. If found, update JSON to `"EquipmentClass"`. |

---

## Acceptance criteria

- [ ] All 4 phases complete with PASS gates
- [ ] `dotnet build` + `dotnet test` repo-wide green
- [ ] `grep -rn "Sunfish.Blocks.PropertyAssets" .` returns zero results
- [ ] `grep -rn "PropertyAssets" .` returns only `Foundation.Assets` matches (different namespace)
- [ ] apps/docs page renamed `assets.md` → `equipment.md`; doc nav (if present) updated
- [ ] Workstream #24 ledger row updated
- [ ] PR description names this as the Equipment rename per UPF Rule 4
- [ ] Single PR with auto-merge

---

## After this hand-off ships

- Receipts hand-off (`icm/_state/handoffs/property-receipts-stage06-handoff.md`) needs a 1-line update: `AssetRef` field → `EquipmentRef` (placeholder string) + reference to `EquipmentId` when typed. Research session does this.
- ADR 0053 (work-order) field reference `WorkOrder.Asset: AssetId?` → `WorkOrder.Equipment: EquipmentId?`. Already done in this same PR's ADR 0053 amendment section.
- Other cluster work (Inspections asset-condition assessments referencing Equipment, etc.) follows naturally from the canonical name.

---

## Sign-off

Research session — 2026-04-28
