# Cluster-vs-Existing Reconciliation Report — Property-Ops Cluster

**Status:** Complete; recommendations awaiting user decision
**Date:** 2026-04-28
**Author:** research session
**Companion:** [`property-ops-cluster-naming-upf-review-2026-04-28.md`](./property-ops-cluster-naming-upf-review-2026-04-28.md) — UPF review whose 5 rules drive this report's per-module decisions
**Triggered by:** sunfish-PM workstream #24 halt on package-name collision (PR #211 flag); subsequent audit revealed deeper scope-conflation than naming alone resolves.

---

## Executive summary

The property-ops cluster (14 per-domain Stage 00 intakes drafted 2026-04-28, plus 4 Proposed ADRs and 4 hand-offs) **assumed greenfield** for most modules. Audit reveals **6 pre-existing `blocks-*` packages with substantial domain scope** that overlaps cluster intake responsibilities:

| Existing | Purpose | Cluster intake overlap |
|---|---|---|
| `blocks-leases` | Lease + Document + Party + Unit + LeasePhase | ~80% overlap with cluster Leases (#27) |
| `blocks-inspections` | Inspection + Template + ChecklistItem + Response + Report + Deficiency | ~70% overlap with cluster Inspections (#25) |
| `blocks-maintenance` | MaintenanceRequest + Quote + Rfq + **Vendor** + **WorkOrder** + state machine + UI | ~85% overlap with cluster Vendors (#18) + Work Orders (#19) |
| `blocks-rent-collection` | Payment + Invoice + RentSchedule + LateFeePolicy + BankAccount | ~60% overlap with cluster Receipts (#26) — different angle |
| `blocks-accounting` | GL Account + JournalEntry + DepreciationSchedule + QBO export | ~40% overlap with Phase 2 commercial accounting cycle |
| `blocks-assets` | UI catalog over generic asset records (NOT physical equipment) | Naming collision only — different domain |

Plus `foundation-assets-postgres` whose `Sunfish.Foundation.Assets.Common.EntityId` establishes "Asset" as a foundation-tier generic-entity term, requiring entity-name disambiguation per UPF Rule 4.

**Net effect on cluster:** roughly **70% of cluster "domain modules" reduce from `new block` to `extend existing block`**. Three new substrates (messaging, signatures, payments — already in flight as ADRs 0052, 0054, 0051) and three new domain blocks (Properties shipped, Property-Equipment, Property-Receipts) survive as the genuinely net-new cluster contribution.

This reconciliation report maps every cluster artifact to a disposition: `extend` / `new` / `don't` / `compose-existing`. Each disposition cites the existing block's actual scope and identifies the deltas the cluster adds.

---

## Existing block inventory (full — for reference)

| Package | SDK | Domain scope (from csproj + Models/) | Cluster relevance |
|---|---|---|---|
| `blocks-accounting` | Standard | GL Accounts (asset/liability/equity/revenue/expense), JournalEntry + Lines, DepreciationSchedule (multiple methods), QuickBooks IIF exporter, in-memory service | **Phase 2 commercial accounting cycle target** |
| `blocks-assets` | Razor | UI catalog block (`AssetCatalogBlock.razor`); composes SunfishDataGrid + SunfishFileManager; thin `AssetRecord` (Id/Name/Path/SizeBytes/LastModifiedUtc) | **Naming collision only** — different domain (file-listing) than property-equipment |
| `blocks-businesscases` | Razor | `IEntitlementResolver`, bundle-provisioning service, `ISunfishEntityModule` contributions | Out of cluster scope |
| `blocks-forms` | Razor | Form orchestration over `SunfishForm` + `SunfishValidation` | Out of cluster scope (used as substrate) |
| `blocks-inspections` | Razor | Inspection + InspectionTemplate + InspectionChecklistItem + InspectionResponse + InspectionReport + Deficiency (severity/status); IInspectionsService + InMemoryInspectionsService; ScheduleInspection / CreateTemplate / RecordDeficiency operations; references `EntityId UnitId` | **Cluster Inspections (#25) overlaps ~70%** |
| `blocks-leases` | Razor | Lease + LeasePhase + Document + Party + PartyKind + Unit; `EntityId UnitId`; thin first-pass per its own description | **Cluster Leases (#27) overlaps ~80%** |
| `blocks-maintenance` | Razor | MaintenanceRequest + Status, Quote + Status, Rfq + Status, **Vendor + VendorSpecialty + VendorStatus**, **WorkOrder + WorkOrderStatus**; IMaintenanceService + InMemoryMaintenanceService; CreateVendor / CreateWorkOrder / SendRfq / SubmitQuote / SubmitMaintenanceRequest operations; **TransitionTable** state machine; WorkOrderListBlock.razor UI | **Cluster Vendors (#18) overlaps ~85%; Cluster Work Orders (#19) overlaps ~85%** |
| `blocks-rent-collection` | Standard | Payment, Invoice + Status, RentSchedule, LateFeePolicy, BankAccount + Kind, BillingFrequency; `decimal Amount` placeholder + `string Method` opaque (per ADR 0051 deferred) | **Cluster Receipts (#26) overlaps in payment evidence; different ingress angle** |
| `blocks-scheduling` | Razor | Schedule-view orchestration over SunfishScheduler + SunfishCalendar + SunfishAllocationScheduler; IScheduleReservationCoordinator | **Cluster Showings within Leasing Pipeline (#22) likely composes this** |
| `blocks-subscriptions` | (verify) | Billing subscriptions | Out of cluster scope |
| `blocks-tasks` | Razor | Task-board state-machine block; TaskBoardBlock.razor; opinionated kanban-style display | Out of cluster scope (different domain) |
| `blocks-tax-reporting` | (verify) | Tax-prep export | **Cluster tax-advisor depreciation projection target** |
| `blocks-tenant-admin` | Razor | TenantProfile + TenantUser + TenantRole + BundleActivation; references foundation-multitenancy + foundation-persistence + foundation-catalog | Out of cluster scope (multi-tenancy admin) |
| `blocks-workflow` | Standard | **Generic state-machine workflow primitives** — declarative definition + fluent builder + in-memory runtime; deferred Temporal/Elsa/Dapr/BPMN | **Substrate** — work-order state machine likely composes this |
| `foundation-assets-postgres` | Standard | Postgres-backed `IEntityStore`, `IVersionStore`, `IAuditLog`, `IHierarchyService` over EF Core + Npgsql; uses `Sunfish.Foundation.Assets.Common.EntityId` | **Foundation tier "Asset" namespace** — drives Rule 4 entity-name disambiguation |

---

## Per-cluster-intake reconciliation

For each cluster intake (workstreams #16–#30 + adjacent mesh-VPN), this section maps to an existing block + assigns a disposition.

Disposition vocabulary:
- **NEW** — no existing block covers; create new package
- **EXTEND** — existing block covers ≥50% of scope; cluster adds fields/methods/states/child-entities to existing block
- **COMPOSE** — cluster orchestration consumes one or more existing blocks without modifying them
- **DON'T** — cluster scope retracts; existing block is sufficient as-is
- **SUBSTRATE** — kernel-tier or foundation-tier addition (not a domain block)

### Workstream #16 — Property-operations vertical cluster (umbrella)

**Existing:** N/A (umbrella). **Disposition:** N/A. **Action:** No change; INDEX file references this report.

### Workstream #17 — Properties domain (cluster #1 spine)

**Existing:** None. `Property` (real-estate parcel) was net-new.
**Disposition:** **NEW** ✅ shipped via PR #210.
**Action:** None. Already built. Convention: `blocks-properties` (cluster root, unprefixed per Rule 2).

### Workstream #18 — Vendors domain (cluster #2 spine)

**Existing:** `blocks-maintenance.Models.Vendor` + `VendorSpecialty` + `VendorStatus`; `IMaintenanceService.CreateVendorRequest`, `ListVendorsQuery`. Substantial existing scope.

**Cluster delta over existing:**
- W-9 / TIN capture (encrypted at rest) — new fields
- Magic-link onboarding flow (Bridge surface) — new flow
- VendorPerformanceRecord lifecycle event log — new child entity
- Multi-actor vendor identity (vendor with multiple contact people) — new child entity (`VendorContact`)
- New ADR ("Vendor onboarding posture") — new architectural decision

**Disposition:** **EXTEND** — add the cluster delta to `blocks-maintenance.Vendor` rather than parallel `blocks-property-vendors`. The existing block already has Vendor + VendorSpecialty + VendorStatus; cluster's contribution is fields + onboarding flow + performance log.

**Action:**
- Rewrite cluster intake `property-vendors-intake-2026-04-28.md` to scope as extension to `blocks-maintenance`
- New ADR ("Vendor onboarding posture") still drafts; it specifies the W-9 + magic-link flow regardless of which block hosts it
- Hand-off (when written) targets `packages/blocks-maintenance/` not a new package
- Workstream #18 row updated: "extend `blocks-maintenance`"

**Estimated cluster cost reduction:** ~4-6 hours of new-block scaffold avoided; replaced by ~2-3 hours of extension PRs.

### Workstream #19 — Work Orders coordination spine (cluster #3 spine)

**Existing:** `blocks-maintenance.Models.WorkOrder` + `WorkOrderStatus` + `TransitionTable.cs` + `IMaintenanceService.CreateWorkOrderRequest`, `ListWorkOrdersQuery`. Substantial existing scope including state machine.

**Cluster delta over existing (per ADR 0053):**
- 13-state machine (existing has its own state machine — must reconcile)
- Multi-party threads (owner ↔ vendor ↔ tenant) — new child entity (`PrimaryThread` ref to `blocks-messaging`)
- Right-of-entry notice (`WorkOrderEntryNotice`) — new child entity
- Completion attestation (`WorkOrderCompletionAttestation`) — new child entity
- WorkOrderSource polymorphism (event-sourced) — new pattern
- WorkOrderAppointment with CP-class lease — new child entity
- Audit-substrate emission (11 typed audit records) — new addition

**Reconciliation question:** Does ADR 0053's 13-state machine align with `blocks-maintenance.TransitionTable`? Need to read the existing transition table to know.

**Disposition:** **EXTEND** (with ADR 0053 amendment likely) — keep `blocks-maintenance.WorkOrder` as the canonical entity; add cluster's child entities (Thread, EntryNotice, CompletionAttestation, Appointment) and the audit-emission discipline as extensions. ADR 0053 amends to clarify the state-machine reconciliation.

**Action:**
- **Read `blocks-maintenance/Services/TransitionTable.cs`** to know existing states and transitions
- Rewrite cluster intake `property-work-orders-intake-2026-04-28.md` to scope as extension
- ADR 0053 amends: state-machine section updates to "compose existing TransitionTable; add extensions for entry-notice + completion-attestation transitions"; references existing `WorkOrderStatus` enum + extensions
- ADR 0053 entity-name field `WorkOrder.Asset: AssetId?` → `WorkOrder.Equipment: EquipmentId?` (Rule 4)
- Hand-off (when written) targets `packages/blocks-maintenance/` not new package
- Workstream #19 row updated: "extend `blocks-maintenance`"

**Estimated cluster cost reduction:** ~6-8 hours new-block scaffold avoided; replaced by ~3-4 hours of extension PRs + ADR amendment.

**RISK:** This is the highest-overlap module; reconciliation must verify `TransitionTable.cs` existing states are compatible with ADR 0053's added states. If incompatible, escalate.

### Workstream #20 — Bidirectional Messaging Substrate (cluster #4 spine)

**Existing:** None for messaging. `Foundation.Integrations` (existing) hosts the substrate per ADR 0052.

**Disposition:** **NEW SUBSTRATE** — `Foundation.Integrations.Messaging` namespace + `blocks-messaging` package (new) + `providers-email-*` + `providers-sms-*` adapters per ADR 0052.

**Action:** No change; ADR 0052 (PR #201, merged Proposed) is correct as drafted. Cluster intake remains valid. Future hand-off scaffolds new packages.

### Workstream #21 — Signatures + Document Binding (cluster cross-cutting)

**Existing:** None. `kernel-signatures` (new substrate) per ADR 0054.

**Disposition:** **NEW SUBSTRATE** — `kernel-signatures` package per ADR 0054. ADR 0046 amendment for signature survival under key rotation.

**Action:** No change; ADR 0054 (PR #209, merged Proposed) is correct. Cluster intake remains valid.

### Workstream #22 — Leasing Pipeline + Fair Housing (cluster cross-cutting)

**Existing:** None for leasing pipeline. Composes existing `blocks-leases.Lease` (terminal state after pipeline approval) + `blocks-scheduling` (showings).

**Disposition:** **NEW + COMPOSE** — new `blocks-property-leasing-pipeline` package (or `blocks-leasing-pipeline` if no collision; verify). Composes `blocks-leases` + `blocks-scheduling`.

**Action:**
- Audit `packages/blocks-leasing-pipeline/` (does it exist? `ls` confirms no — net-new)
- Bare name `blocks-leasing-pipeline` is collision-free (Rule 1 audit)
- Per Rule 2, cluster siblings prefix when collision → no collision here, so bare name OK
- Per Rule 2 alternative interpretation: cluster consistency might prefer `blocks-property-leasing-pipeline` for clarity
- **Recommend: `blocks-property-leasing-pipeline`** for cluster consistency (verbose but explicit cluster membership)
- Cluster intake stays largely as drafted; new ADR (Leasing Pipeline + FHA) drafts when this workstream advances

### Workstream #23 — iOS Field-Capture App (cluster cross-cutting)

**Existing:** None. `accelerators/anchor-mobile-ios/` is net-new.

**Disposition:** **NEW** — new accelerator. ADR 0028 amendment + ADR 0048 amendment per cluster intake.

**Action:** No change; cluster intake remains valid.

### Workstream #24 — Property-Assets domain (cluster module) ⚠️ COLLISION

**Existing:**
- `blocks-assets` (UI catalog; thin AssetRecord; different domain than physical equipment)
- `Sunfish.Foundation.Assets.Common.EntityId` (foundation-tier generic-entity reference)

**Cluster delta:** Physical-equipment inventory: water heaters, HVAC, appliances, vehicles, structural elements. Lifecycle event log (Installed, Serviced, Inspected, Replaced, Disposed). Cost basis for tax depreciation. AssetClass discriminator. Vehicle subtype + Trip events for mileage logging.

**Disposition:** **NEW** with **entity-name rename per Rule 4** — package `blocks-property-equipment` (NOT `blocks-property-assets`); entity `Equipment` (NOT `Asset`). Avoids both the package collision AND the foundation-tier "Asset" overload.

**Action:**
- **Revert PR #212's `blocks-property-assets` rename** in favor of `blocks-property-equipment` per Rule 4
- Hand-off `property-assets-stage06-handoff.md` rewritten:
  - Package: `packages/blocks-property-equipment/`
  - Namespace: `Sunfish.Blocks.PropertyEquipment`
  - Entity: `Equipment` + `EquipmentId` + `EquipmentClass` + `EquipmentLifecycleEvent`
  - Vehicle subtype + Trip events follow same naming
- Cluster intake `property-assets-intake-2026-04-28.md` updated:
  - Title: "Property-Equipment domain module (formerly Assets)"
  - Body: "Asset" → "Equipment" throughout
- ADR 0053 (work-order) amended: `WorkOrder.Asset` → `WorkOrder.Equipment`
- Workstream #24 row updated: package + namespace + entity name

**Estimated cost:** ~30 minutes of edits across 4 files.

**Alternative if user rejects rename:** keep `Asset` entity name with namespace qualification. UPF Rule 4 supports either; default is rename.

### Workstream #25 — Inspections domain (cluster module)

**Existing:** `blocks-inspections` with full domain (Inspection + Template + ChecklistItem + Response + Report + Deficiency + InspectionPhase + InspectionItemKind + DeficiencySeverity + DeficiencyStatus). IInspectionsService + InMemoryInspectionsService. Substantial.

**Cluster delta:**
- Move-in / move-out triggers — new InspectionItemKind value + scheduling rule
- AssetConditionAssessment children — could be modeled as Deficiency variant OR new child entity
- iOS walkthrough wizard — accelerator surface (not block scope)

**Disposition:** **EXTEND** — `blocks-inspections` already has the entire substrate. Cluster's contribution is:
- Adding move-in / move-out InspectionItemKind enum values (or scheduling tags)
- Adding AssetConditionAssessment as a Deficiency variant (`DeficiencyKind.AssetCondition` or similar) OR as a new child entity that references both Inspection and Equipment (per Rule 4)

**Action:**
- Read `blocks-inspections/Models/InspectionItemKind.cs` + `Deficiency.cs` to determine extension shape
- Rewrite cluster intake `property-inspections-intake-2026-04-28.md` as extension to `blocks-inspections`
- Hand-off (when written) targets `packages/blocks-inspections/` not new package
- Workstream #25 row updated: "extend `blocks-inspections`"

### Workstream #26 — Property-Receipts domain (cluster module)

**Existing:** None for "receipts" specifically. `blocks-rent-collection.Payment` is the closest (records payment events on invoices), but Receipt is a different artifact (capture-and-categorize purchase evidence; not necessarily payment-event-linked).

**Disposition:** **NEW** — `blocks-property-receipts` (or `blocks-receipts` per Rule 1+2). Recommend `blocks-property-receipts` for cluster consistency (Rule 2 recommendation).

**Action:**
- Hand-off (already drafted in PR #212 with `blocks-property-receipts`) is correct
- No changes needed beyond entity-name guidance: receipts reference Equipment (not Asset) when linking to physical equipment per Rule 4
- Update Receipt schema field: `AssetRef: AssetId?` → `EquipmentRef: EquipmentId?` (placeholder string in first-slice; typed when Equipment ships)

### Workstream #27 — Leases domain (cluster module)

**Existing:** `blocks-leases` with Lease + Document + Party + PartyKind + Unit + LeasePhase + LeaseId + DocumentId + PartyId. Self-described as "thin for the first pass; full workflow surface (signature, execution, renewal, termination) deferred."

**Cluster delta:**
- Lease versioning — `LeaseDocumentVersion` entity (multiple versions per lease)
- Signature binding — `Lease.SignatureEvent` ref per ADR 0054
- Renewal workflow — new state-machine transition + reminder
- Termination workflow — new transition + move-out inspection trigger
- Lease holder roles (multi-tenant lease, co-tenant, occupant, guarantor) — already covered by `Party` + `PartyKind`

**Disposition:** **EXTEND** — `blocks-leases` already has most of the structure. Cluster's contribution is the deferred-by-original-author features (versioning, signature binding, renewal, termination), per the existing block's own description.

**Action:**
- Cluster intake `property-leases-intake-2026-04-28.md` rewritten as extension to `blocks-leases`
- Specifically: `LeaseDocumentVersion` as new child entity referencing existing `Document`; `Lease.SignatureEventRef` field added; `LeasePhase` enum extended with renewal/termination states (verify existing values)
- Hand-off targets `packages/blocks-leases/`
- Workstream #27 row updated: "extend `blocks-leases`"

**RISK:** existing `LeasePhase` may conflict with cluster's proposed phases. Read existing values before drafting hand-off.

### Workstream #28 — Public Listings surface (cluster module)

**Existing:** None.

**Disposition:** **NEW** — `blocks-property-listings` (or `blocks-public-listings`; verify Rule 1).

**Action:** No change; cluster intake remains valid. New ADR (Public listing surface) drafts when workstream advances.

### Workstream #29 — Owner Web Cockpit (cluster module)

**Existing:** None for "owner cockpit" composite view. Composes every other cluster module + existing Sunfish blocks.

**Disposition:** **COMPOSE** — no new domain block. Cockpit views distribute across each domain block (per cluster intake OQ-OC1) OR a new `blocks-property-cockpit-views` block as composition layer.

**Action:** Cluster intake remains largely valid. Recommend distributed cockpit views (one per domain block) over composition block.

### Workstream #30 — Mesh VPN / Cross-Network Transport (adjacent)

**Existing:** None.

**Disposition:** **NEW SUBSTRATE** + provider adapters — kernel-tier transport ADR; `providers-mesh-headscale/`, `providers-mesh-tailscale/`, etc.

**Action:** No change; cluster intake remains valid. New ADR ("Three-tier peer transport") drafts when workstream advances.

---

## Summary table

| WS# | Title | Original disposition | Reconciled disposition | Existing block | Action |
|---|---|---|---|---|---|
| 16 | Cluster umbrella | N/A | N/A | — | Reference this report |
| 17 | Properties | NEW | NEW ✅ shipped | none | None |
| 18 | Vendors | NEW | **EXTEND** | `blocks-maintenance.Vendor` | Rewrite intake; redirect hand-off |
| 19 | Work Orders | NEW | **EXTEND** | `blocks-maintenance.WorkOrder` | Rewrite intake; ADR 0053 amend; redirect hand-off |
| 20 | Messaging Substrate | NEW SUBSTRATE | NEW SUBSTRATE | none | No change |
| 21 | Signatures | NEW SUBSTRATE | NEW SUBSTRATE | none | No change |
| 22 | Leasing Pipeline | NEW | **NEW + COMPOSE** | composes `blocks-leases` + `blocks-scheduling` | Add audit step in hand-off |
| 23 | iOS App | NEW | NEW | none | No change |
| 24 | Property-Assets | NEW (renamed from `blocks-assets`) | **NEW (rename to `blocks-property-equipment`)** | `blocks-assets` (different domain) + foundation-tier `Asset` overload | Hand-off rewrite per Rule 4 |
| 25 | Inspections | NEW | **EXTEND** | `blocks-inspections` | Rewrite intake; redirect hand-off |
| 26 | Property-Receipts | NEW | NEW | none | Already correct (post-PR #212); minor entity-name update |
| 27 | Leases | NEW | **EXTEND** | `blocks-leases` | Rewrite intake; redirect hand-off |
| 28 | Public Listings | NEW | NEW | none | No change |
| 29 | Owner Cockpit | NEW | **COMPOSE** | distributes across all blocks | Confirm distributed pattern in intake |
| 30 | Mesh VPN | NEW SUBSTRATE | NEW SUBSTRATE | none | No change |

**Net effect:**
- 4 modules shift from NEW to EXTEND (#18 #19 #25 #27) — substantial cluster cost reduction
- 1 module shifts from NEW to NEW-with-rename (#24) — entity-name correction
- 1 module shifts from NEW to NEW + COMPOSE (#22)
- 1 module shifts from NEW to COMPOSE (#29)
- 6 modules unchanged

---

## ADR amendments triggered by this reconciliation

| ADR | Status | Amendment |
|---|---|---|
| **0051 (Payments)** | Proposed (PR #203) | None directly. References `Money Amount` + `PaymentMethodReference`; no `Asset` reference. |
| **0052 (Bidirectional Messaging Substrate)** | Proposed (PR #201) | None directly. Substrate-level; no domain-entity coupling. |
| **0053 (Work-Order Domain Model)** | Proposed (PR #205) | **Required amendment**: (a) `WorkOrder.Asset: AssetId?` → `WorkOrder.Equipment: EquipmentId?` per Rule 4; (b) clarify state machine composes existing `blocks-maintenance.TransitionTable` rather than introducing new; (c) clarify cluster's WorkOrder is an *extension* to `blocks-maintenance.WorkOrder`, not a new entity. |
| **0054 (Electronic Signature Capture & Document Binding)** | Proposed (PR #209) | None directly. Substrate-level; no domain-entity coupling. |
| **0046 (Key-loss recovery)** | Accepted (per PR #201's reference) | Pending amendment for ADR 0054 (signature survival under key rotation). Already noted. |
| **0049 (Audit-trail substrate)** | Accepted | None directly. Adding 5 typed audit records (per ADR 0054) and 11 typed audit records (per ADR 0053) — these are vocabulary additions, not amendments. |

**One ADR (0053) requires amendment** — within the FAILED threshold of 4 from the UPF review.

---

## Hand-off / intake amendments triggered

| Artifact | Action |
|---|---|
| `icm/_state/handoffs/property-assets-stage06-handoff.md` | **Rewrite** — package `blocks-property-equipment`; entity `Equipment`; namespace `Sunfish.Blocks.PropertyEquipment` (per Rule 4) |
| `icm/_state/handoffs/property-receipts-stage06-handoff.md` | **Minor edit** — Receipt's `AssetRef` field renamed to `EquipmentRef` (placeholder string in first-slice) |
| `icm/_state/handoffs/property-properties-stage06-handoff.md` | None — Properties shipped; no Asset references in shipped scope |
| `icm/00_intake/output/property-properties-intake-2026-04-28.md` | None |
| `icm/00_intake/output/property-vendors-intake-2026-04-28.md` | **Rewrite** — scope as extension to `blocks-maintenance.Vendor`; preserve cluster delta (W-9, magic-link, performance log) |
| `icm/00_intake/output/property-work-orders-intake-2026-04-28.md` | **Rewrite** — scope as extension to `blocks-maintenance.WorkOrder`; reconcile state machines |
| `icm/00_intake/output/property-inspections-intake-2026-04-28.md` | **Rewrite** — scope as extension to `blocks-inspections`; AssetConditionAssessment as Deficiency variant or new child |
| `icm/00_intake/output/property-leases-intake-2026-04-28.md` | **Rewrite** — scope as extension to `blocks-leases.Lease`; LeaseDocumentVersion as new child; Phase enum extension |
| `icm/00_intake/output/property-assets-intake-2026-04-28.md` | **Rewrite** — title and body "Asset" → "Equipment" per Rule 4 |
| `icm/00_intake/output/property-receipts-intake-2026-04-28.md` | **Minor edit** — `Asset FK` → `Equipment FK` references |
| `icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md` | **Update** — add "Naming convention" section pointing at UPF review; update disposition table |

**Estimated cost:** ~3 hours of editing across 8 files. None of this is code; all is documentation + planning artifacts.

---

## Workstream ledger updates triggered

For workstreams #18, #19, #24, #25, #27, #29 — update the row's **Reference** column to point at the existing block being extended (or new package being created with the corrected name) and update the **Notes** column to reflect disposition.

For workstreams #20, #21, #22, #23, #26, #28, #30 — minor or no changes per dispositions above.

---

## Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Existing `blocks-maintenance.TransitionTable` state machine conflicts with ADR 0053's 13-state machine | Medium | High (forces ADR 0053 redraft) | Read transition table before drafting amendment; if conflict, propose unified state set |
| User rejects "Equipment" rename and prefers namespace-qualification of `Asset` | Medium | Low (alternative is documented in Rule 4) | Default to rename; pivot if user prefers |
| Existing `blocks-leases.LeasePhase` doesn't accommodate cluster's renewal/termination phases | Low | Medium (intake redraft) | Read enum before drafting hand-off |
| Existing `blocks-rent-collection.Payment.Method` pattern conflicts with ADR 0051's `PaymentMethodReference` | Low | Medium (already known per ADR 0051 implementation checklist) | ADR 0051 already addresses |
| Sunfish-PM picks up `blocks-property-assets` hand-off (PR #212) before this reconciliation lands; Equipment rename retroactive | High | Medium | Communicate the rename to sunfish-PM via ledger update + memory note ASAP; their PR can be amended pre-merge or follow-up rename |

---

## Recommended sequence of corrections

If user accepts this reconciliation:

1. **Halt sunfish-PM on workstream #24** — they're starting on `blocks-property-assets`. Update their hand-off + ledger row to `blocks-property-equipment` BEFORE they ship Phase 1 scaffold.
2. **Ship UPF review + this report as PR** (current branch).
3. **Ship corrected hand-off for #24** — `blocks-property-equipment` per Rule 4. Bundle with ADR 0053 amendment.
4. **Rewrite cluster intakes for extend-disposition workstreams** (#18, #19, #25, #27) — substantial editing; could ship as 4 separate PRs or one bundled PR.
5. **Update workstreams ledger rows** to reflect new dispositions.
6. **Resume cluster ADR drafting cadence** (Leasing Pipeline + FHA next; Vendor Onboarding now scoped as extension; etc.)
7. **Sunfish-PM resumes** on corrected `blocks-property-equipment` hand-off + ADR 0053 amendment-aware Work-Orders extension hand-off.

If user rejects "Equipment" rename (Rule 4):
- Skip step 3 entity-name change; keep `blocks-property-assets` from PR #212
- Update memory note to reflect namespace-qualification convention instead
- Other steps unchanged

---

## Sign-off

Research session — 2026-04-28
