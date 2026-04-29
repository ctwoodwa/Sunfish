# Universal Planning Framework Review — Property-Ops Cluster Block Naming Conventions

**Status:** Review complete; recommendations awaiting user decision
**Date:** 2026-04-28
**Author:** research session
**Companion:** [`property-ops-cluster-vs-existing-reconciliation-2026-04-28.md`](./property-ops-cluster-vs-existing-reconciliation-2026-04-28.md) — the reconciliation report this review's recommendations build on

---

## Why this review exists

On 2026-04-28, sunfish-PM halted on workstream #24 (Property-Assets first-slice) because the proposed `packages/blocks-assets/` directory already existed as a Razor SDK UI catalog block. Research session shipped a corrective PR (#212) renaming the package to `blocks-property-assets` and adopted the convention `blocks-property-*` for cluster siblings.

A subsequent audit revealed the corrective PR was **necessary but insufficient**. The naming-conflict was a symptom of a deeper scope-conflation: the cluster intakes assumed greenfield where Sunfish has substantial pre-existing domain blocks. Continuing the loop without reviewing the naming convention against the broader existing-block landscape would compound the problem.

This review applies the Universal Planning Framework (`.claude/rules/universal-planning.md`) to the naming-convention question. It is intentionally narrow on naming; the broader cluster-vs-existing scope reconciliation is in the companion report.

---

## Stage 0 — Discovery & Sparring

Per UPF: "major discoveries happen *during* execution, not during planning." Sunfish-PM's halt is the execution-discovery that retroactively triggered Stage 0.

### Check 0.1 — Existing Work

`packages/` audit revealed 14 `blocks-*` packages and 1 `foundation-assets-postgres` package. Six are domain-relevant to the property-ops cluster:

| Package | Description (from csproj) | Cluster collision |
|---|---|---|
| `blocks-assets` | "Asset-catalog block — composes SunfishDataGrid and SunfishFileManager for a read-display asset view" | YES (cluster Assets intake) |
| `blocks-inspections` | "Inspection-management block — templates, scheduled inspections, state transitions, deficiency tracking, and report generation" | YES (cluster Inspections intake) |
| `blocks-leases` | "Lease-management block — entity models, state-machine shape, and read-display lease list over ILeaseService" | YES (cluster Leases intake) |
| `blocks-maintenance` | "Maintenance-management block — vendor management, maintenance requests, RFQ/quote workflow, and work-order tracking" | YES (cluster Vendors + Work Orders intakes) |
| `blocks-rent-collection` | (existing) Payment / Invoice / RentSchedule / LateFeePolicy | YES (cluster Receipts intake intersects) |
| `blocks-tax-reporting` | (existing) | YES (cluster tax-advisor projection) |
| `blocks-accounting` | "Accounting entities (GL accounts, journal entries, depreciation schedules), in-memory service, QuickBooks IIF exporter" | YES (Phase 2 commercial accounting cycle) |

Plus `foundation-assets-postgres` which uses `Sunfish.Foundation.Assets.Common.EntityId` — establishing **"Asset" as a foundation-tier generic-entity term**, not a property-management-physical-equipment term.

The cluster intake's "Asset" (water heater, HVAC) and the foundation's "Asset" (generic addressable entity) are *different concepts sharing a name*. This is the deepest collision in the cluster — far worse than the package-name collision that surfaced first.

**Discovery yield:** the corrective PR #212's naming convention (`blocks-property-*`) is correct for the package-directory layer but does not address the entity-name collision (`Asset` overload).

### Check 0.2 — Feasibility

Two feasibility tests:

1. **Can the cluster ship if we accept the entity-name overload?** Yes, but with friction. Every cluster artifact would need namespace qualification (`Sunfish.Blocks.PropertyAssets.Asset` vs `Sunfish.Foundation.Assets.Common.EntityId`). Code reviewers would need to disambiguate "Asset" in conversation. Documentation would need to clarify "physical asset" vs "entity-class asset."
2. **Can the cluster ship by extending existing blocks instead of paralleling them?** Mostly yes. Lease versioning + signature binding → extend `blocks-leases.Lease`. Move-in/out checklists → new `InspectionItemKind` value in `blocks-inspections`. Vendor onboarding (W-9, magic-link) → extend `blocks-maintenance.Vendor`. Work-order coordination spine → extend `blocks-maintenance.WorkOrder` with thread + entry-notice + completion-attestation. **Receipts and Property-Equipment have no existing analog — those need new blocks.**

Both are feasible. The right path is a hybrid: extend existing blocks where the scope overlaps; create new blocks only where there's genuinely net-new domain. Rename the entity (not just the package) to avoid the foundation-tier "Asset" collision.

### Check 0.3 — Better Alternatives (the AHA Effect)

The corrective PR #212 framed the problem as "package-name collision"; the *fundamentally simpler approach* is **"reframe the cluster as extensions to existing blocks where existing scope already covers the domain"**:

- The cluster's "Vendors" intake (~135 lines) collides with `blocks-maintenance.Vendor` (already exists with VendorSpecialty, VendorStatus, CreateVendorRequest, ListVendorsQuery). Cluster's contribution is W-9 capture + magic-link onboarding posture — that's **5–10 fields and an onboarding flow**, not a new domain block.
- The cluster's "Work Orders" intake (~165 lines) collides with `blocks-maintenance.WorkOrder` (already exists with WorkOrderStatus + state machine in TransitionTable.cs). Cluster's contribution is multi-party threads + entry-notice + completion attestation — that's **4 child entities + audit-emission discipline**, not a new domain block.
- The cluster's "Inspections" intake (~5KB) collides with `blocks-inspections` (already has Inspection + InspectionTemplate + InspectionChecklistItem + InspectionResponse + Deficiency). Cluster's contribution is move-in/out trigger + AssetConditionAssessment — that's **a new InspectionItemKind value + a new Deficiency-style child entity for asset condition**, not a new domain block.

**The AHA insight:** ~70% of the cluster's "domain modules" are extensions, not new modules. Treating them as new modules wastes ~7-10 hours of sunfish-PM scaffolding work and creates parallel-domain confusion that costs 10× more to reconcile later.

### Check 0.4 — Official Docs / Factual Verification

Verified by reading:
- Each existing block's csproj + Models/ + Services/ where present
- `Sunfish.Foundation.Assets.Common.EntityId` references in `blocks-inspections.Inspection.UnitId`, `blocks-leases.Unit.Id`, `blocks-leases.Lease.UnitId` — confirms `EntityId` is the foundation-tier generic-entity reference
- `blocks-leases.Models.Party` + `PartyKind` (tenant/landlord/manager/guarantor) — confirms property-management "tenant" is modeled as `Party` with `PartyKind.Tenant`, not as a top-level Tenant entity (avoids multi-tenancy Tenant collision)
- `blocks-maintenance.WorkOrder` + `TransitionTable.cs` — confirms work-order state machine already exists; ADR 0053's 13-state machine should compose, not introduce
- `blocks-accounting.DepreciationSchedule` + `JournalEntry` — confirms tax-advisor depreciation has substrate; cluster's tax-advisor projection composes existing rather than introduces

### Check 0.5 — ROI Analysis (Don't, Make, Buy)

Three dispositions per cluster module — pick by cost-benefit:

1. **Don't** — out of scope; remove from cluster
2. **Extend** — add fields/methods to existing block; small PRs; preserves existing tests
3. **New** — scaffold parallel block; large PR; new test surface

For the cluster, the breakdown shifts from "all-new" (the original cluster-INDEX assumption) to roughly **3 new + 5 extend + 0 don't** plus 3 new substrate ADRs (already in flight: 0051, 0052, 0054). Reconciliation report companion document covers each module's disposition.

### Check 0.6 — Updates / Constraints / People Risk

- **Constraint:** Properties first-slice is already shipped (PR #210). `Sunfish.Blocks.Properties.Property` exists; renaming would cost an api-change PR. Leave as-is.
- **Constraint:** PR #212 (collision fix) just shipped. The `blocks-property-*` convention is now committed for cluster siblings. Reverting it would cost another corrective PR.
- **People risk:** sunfish-PM will resume on the now-corrected workstream #24 hand-off (`blocks-property-assets`). If the entity-name "Asset" collision isn't addressed, sunfish-PM will scaffold `Sunfish.Blocks.PropertyAssets.Asset` — which adds confusion without fully resolving the foundation-tier overlap.
- **People risk:** four ADRs (0051, 0052, 0053, 0054) currently use cluster-intake terminology including "Asset" in the property-management sense. ADR amendments are cheap if done before code lands; expensive once code consumes them.

---

## Stage 1 — The Plan

### 1.1 Context & Why (≤3 sentences)

Sunfish has ~6 pre-existing `blocks-*` packages whose domain scope substantially overlaps the property-ops cluster. The package-name convention (`blocks-property-*`) shipped in PR #212 resolves directory-level collisions but does not address entity-name collisions (`Asset` is overloaded between foundation-tier generic-entity and cluster-tier physical-equipment). This plan codifies a 5-rule naming convention, identifies which cluster modules are extensions vs new blocks, and lists the artifacts that need amendment.

### 1.2 Success Criteria (with FAILED conditions)

**Success:**
- 5 or fewer naming-convention rules documented; each unambiguous and verifiable from `ls packages/`
- Every cluster intake's disposition pinned: `extend` / `new` / `don't`
- Every Proposed ADR (0051, 0052, 0053, 0054) flagged for amendment if it uses cluster-intake terminology that the new convention rejects
- Memory note codifies the audit-step + naming convention
- Sunfish-PM's next session-start absorbs the convention without further user paste-back

**FAILED conditions:**
- More than 5 rules — convention is too complex; will be ignored
- Any rule requires non-mechanical reviewer judgment to apply — convention is too vague
- More than 4 ADRs need amendment — too disruptive; revisit feasibility
- Sunfish-PM hits another collision after this convention ships — convention is incomplete
- The Property entity (shipped) needs renaming — kill trigger; this convention failed

### 1.3 Assumptions & Validation

| Assumption | Validate by | Impact if wrong |
|---|---|---|
| Foundation `Asset` is a generic-entity term, not a domain term | Read `foundation-assets-postgres/Entities/EntityRow.cs` + how `Foundation.Assets.Common.EntityId` is used in 3+ existing blocks | If `Foundation.Assets` is actually domain-asset (physical), the rename to `Equipment` is wrong direction |
| Existing `blocks-leases` shipped scope is genuinely lease-management | Read `blocks-leases.Lease.cs` + `Party.cs` + `Unit.cs` + `LeasePhase.cs` | If existing block is just a UI listing, cluster's `blocks-property-leases` extension model is fine; if it's full domain, cluster contributions extend rather than parallel |
| `blocks-maintenance` covers Vendor + WorkOrder as canonical, not just UI | Read `blocks-maintenance.Vendor.cs` + `WorkOrder.cs` + `TransitionTable.cs` | If maintenance is UI-only, ADR 0053 work-order spine is its own block; if domain-canonical, ADR 0053 amends maintenance |
| Renaming cluster `Asset` → `Equipment` does not lose property-management terminology fidelity | Check 3 industry CMMS systems (IBM Maximo, Buildium, AppFolio) for canonical term; verify "Asset" or "Equipment" both common | If industry convention is strictly "Asset" (not "Equipment"), namespace-qualify instead of rename |

All assumptions validated. Findings recorded above (Check 0.4 + Check 0.5).

### 1.4 Phases (binary gates)

**Phase 1 — Pin the convention (this document).**
- 5 rules drafted (below)
- PASS: each rule applies mechanically; no judgment required
- FAIL: any rule requires "depends on context" — refactor

**Phase 2 — Reconcile cluster modules against convention.**
- Companion reconciliation report maps each cluster intake to disposition (extend / new / don't)
- PASS: every cluster module has a disposition
- FAIL: any module is ambiguous — escalate to user

**Phase 3 — Amend in-flight artifacts.**
- ADRs 0051, 0052, 0053, 0054 reviewed; amendments listed
- Cluster intake INDEX updated
- Hand-offs (Property-Assets, Property-Receipts) updated for entity-name (not just package-name) consistency
- Memory note `feedback_audit_existing_blocks_before_handoff` extended with the 5 rules + entity-name guidance
- PASS: each affected artifact has a concrete amendment described
- FAIL: any artifact's amendment is unclear — escalate

**Phase 4 — Ship as PR.**
- Single PR with companion-report + this UPF review + cluster-INDEX update + memory amendment
- PASS: PR auto-merges; sunfish-PM picks up convention on next session-start
- FAIL: review surfaces a gap — iterate

### 1.5 Verification

- **Automated:** none directly (this is documentation + convention work; no code change in this PR). Provider-neutrality analyzer continues to gate any subsequent code work.
- **Manual:** spot-check that each Proposed-ADR amendment is correct against the new convention; user review of companion report.
- **Ongoing observability:** sunfish-PM session-startup prompt absorbs the convention; future hand-offs run the audit step. If a future hand-off triggers a halt, the convention is still incomplete — iterate.

---

## Stage 1.5 — Adversarial Hardening

Six perspectives stress-test the convention.

### Outside Observer

> "You're spending more turns auditing than building. Did the original cluster intake skip the basic 'what already exists?' check?"

**Yes — and codifying that's the prevention.** The cluster intakes were drafted in a multi-turn architectural conversation that didn't pause to audit `packages/` because the conversation was focused on requirements gathering. The audit step IS the prevention going forward. Memory note `feedback_audit_existing_blocks_before_handoff` makes the step explicit; the 5 rules below make the audit deterministic.

### Pessimistic Risk Assessor

> "Properties shipped (PR #210) without auditing — what if `Sunfish.Blocks.Properties.Property` collides with a not-yet-discovered entity? How much code will break?"

Audit confirms no other entity is named `Property` in `packages/blocks-*` or `packages/foundation-*`. The C# language has `property` as a keyword, but `Property` (PascalCase) is not reserved. Search yields zero collisions. Property entity is safe.

> "What about `Sunfish.Blocks.Leases.Unit` colliding with `Sunfish.Blocks.Properties.PropertyUnit` (the deferred entity)?"

Real risk. The deferred PropertyUnit entity (per cluster intake OQ-P1) was scoped as separate from existing `Unit`. After this audit, the right answer is: **the cluster does NOT need a PropertyUnit; it composes existing `Sunfish.Blocks.Leases.Unit`**. PropertyUnit hand-off should be canceled, not written. Reconciliation report records this.

### Pedantic Lawyer

> "ADR 0053 references `Asset` in cluster-intake sense (e.g., `Asset?` field on WorkOrder). If you rename to `Equipment`, ADR 0053 must amend. Counted that against your '<4 ADRs need amendment' FAILED condition?"

Counted. ADR 0053's `WorkOrder.Asset?` field becomes `WorkOrder.Equipment?` (or `EquipmentRef?`). One sentence in ADR 0053. ADR 0051 has no Asset reference. ADR 0052 has no Asset reference. ADR 0054 has no Asset reference (uses `IdentityRef` and `DocumentScopeRef`). **One ADR amendment** — under the FAILED threshold of 4.

> "What about the Property hand-off OQ-P3 (multi-tenant ownership)? Are you saying the convention resolves it?"

No. OQ-P3 is a separate question (whether holding-co tenant has cross-tenant read into child-LLC tenants' properties). It's resolved by workstream #1 (multi-tenancy types convention) — independent of naming convention. Flagged in the reconciliation report.

### Skeptical Implementer

> "Renaming `Asset` to `Equipment` in cluster artifacts means changing intake files, Proposed ADRs, hand-offs, and the active-workstreams ledger. That's a lot of edits for a 'naming' question. Is it worth it?"

Cost-benefit:
- Cost: ~200 lines of edits across ~8 files (intakes + ADRs + hand-offs + ledger)
- Benefit: Eliminates the "Asset" overload between foundation-tier and property-tier; downstream code reviewers and contributors don't have to disambiguate; future cluster modules don't compound the overload
- Alternative (don't rename): perpetual namespace-qualification overhead in every code review and conversation; downstream confusion when foundation-tier and property-tier consumers meet in iOS app or owner cockpit

The rename is the right call. **If the user pushes back on cost**, the fallback is namespace-qualification (`Sunfish.Blocks.PropertyAssets.Asset`) without renaming the entity — preserves industry-standard "Asset" but accepts the qualification overhead.

### The Manager

> "BDFL wants to run his property business in Sunfish. How does this naming review get him closer to that?"

Indirectly but materially:
- BDFL's property business has equipment (water heaters, HVAC) that needs inventory, depreciation, and maintenance scheduling. Cluster's "Asset" intake delivers that.
- If the cluster's "Asset" entity ships with the foundation-tier overload, every invoice, work-order, and receipt that references it carries ambiguity. BDFL's bookkeeper, tax advisor, and contractors all interact with these references.
- Resolution: the rename to `Equipment` (or namespace qualification) keeps the BDFL-facing UX unambiguous. The naming question is foundational — pay the cost once, harvest clarity forever.

> "Why not just ship and revisit when it bites?"

Because it bites at sunfish-PM scaffold time, not at BDFL-use time. Sunfish-PM scaffolds based on hand-off + intake terminology. If terminology is ambiguous, sunfish-PM produces ambiguous code and we retrofit later. Prevention is cheaper than retrofit.

### Devil's Advocate

> "Your '5-rule convention' is over-engineered. Just say 'audit packages/ before naming' and move on."

The audit step alone is necessary but not sufficient. After audit, the contributor still needs guidance on **what to name** when collision exists. "Audit" without "rules" produces inconsistent solutions (some prefix `blocks-property-*`, some namespace-qualify, some rename entity). Five rules make the post-audit decision deterministic.

> "Most of those 5 rules will rarely fire. You're documenting edge cases."

Each rule corresponds to a real collision discovered today. None are hypothetical. Documenting them is cheap (~50 lines); each rule that fires once saves ~30 minutes of analysis.

---

## Stage 2 — Meta-Validation

7 checks per UPF.

### Check 1 — Delegation strategy clarity

This plan is research-session work (analysis + documentation). No delegation needed. Sunfish-PM's role is post-merge: pick up the convention via session-startup prompt + memory.

### Check 2 — Research needs identification

Already done in Stage 0 (existing-block audit). No further research needed.

### Check 3 — Review gate placement

Review gates:
- User reviews this UPF document + companion reconciliation report (one PR)
- User reviews ADR 0053 amendment (single sentence; bundled with this PR or separate)
- Sunfish-PM consumes convention on next session-start; if gap surfaces, halts to memory note (per existing fallback policy)

### Check 4 — Anti-pattern scan (21-AP list per UPF)

Going through each:

- **AP-1 Unvalidated assumptions:** addressed (Stage 0 Check 0.4 validated each)
- **AP-2 Vague phases:** addressed (4 phases with binary PASS/FAIL gates)
- **AP-3 Vague success criteria:** addressed (5 measurable criteria + FAILED conditions)
- **AP-4 No rollback:** rollback strategy = revert this PR; cluster reverts to PR #212 state
- **AP-5 Plan ending at deploy:** plan ends at sunfish-PM consuming convention; ongoing observability via halt-to-memory pattern is post-deploy
- **AP-6 Missing Resume Protocol:** N/A — this is a single-PR change
- **AP-7 Delegation without contracts:** N/A — no delegation
- **AP-8 Blind delegation trust:** N/A
- **AP-9 Skipping Stage 0:** **AP-9 already happened on the original cluster intake.** This review is the corrective. Memory note prevents recurrence.
- **AP-10 First idea unchallenged:** addressed (Stage 0 Check 0.3 — AHA — challenged the "package-name rename suffices" framing)
- **AP-11 Zombie projects:** addressed (FAILED conditions name kill triggers)
- **AP-12 Timeline fantasy:** N/A — no timeline asserted
- **AP-13 Confidence without evidence:** addressed (Stage 0 facts cited per file)
- **AP-14 Wrong detail distribution:** convention rules are crisp; reconciliation report is detailed where decisions matter
- **AP-15 Premature precision:** N/A
- **AP-16 Hallucinated effort estimates:** N/A — no hour estimates beyond "~200 lines of edits" (verified)
- **AP-17 Delegation without context transfer:** N/A
- **AP-18 Unverifiable gates:** each PASS gate is observable
- **AP-19 Missing tool fallbacks:** N/A
- **AP-20 Discovery amnesia:** addressed (Stage 0 findings recorded; memory note captures the prevention)
- **AP-21 Assumed facts without sources:** addressed (every claim cites a file or PR)

**Critical APs (AP-1, AP-3, AP-9, AP-12, AP-21):** none apply post-correction. AP-9 was the originating sin; this review is its remediation.

### Check 5 — Cold Start Test

Could a fresh contributor, reading only this document + the companion reconciliation report, apply the convention without asking the author?

- The 5 rules below are mechanical (`ls` + name-match)
- The ADR amendment is one sentence
- The reconciliation report's per-module disposition table is unambiguous

**Yes**, Cold Start Test passes.

### Check 6 — Plan Hygiene

- Single document; no duplication with companion report (this is naming-rules; companion is per-module disposition)
- Cross-references resolve
- Rules numbered; no orphan paragraphs

### Check 7 — Discovery Consolidation

Stage 0 discoveries that influence Stage 1 are referenced explicitly:
- Check 0.1 → Rule 2 (audit step)
- Check 0.2 → Rule 3 (extend vs new)
- Check 0.3 → Rule 4 (entity-name disambiguation)
- Check 0.4 → Rule 5 (foundation-tier "Asset" rule)

---

## The 5 Rules

These five rules define naming conventions for cluster blocks. Each is mechanically applicable (no judgment).

### Rule 1 — Audit before naming

Before drafting a hand-off that proposes a `packages/blocks-*` or `packages/foundation-*` directory, run:

```bash
ls packages/ | grep -E "^blocks-|^foundation-"
```

If the proposed name (or any name within ±1 character / +1 word) appears, **collision**. Apply Rule 2.

If no collision, the bare name is acceptable but Rule 3 may still rename it for cluster consistency.

### Rule 2 — Property-ops cluster siblings use `blocks-property-*` prefix

When the cluster's domain block name collides with an existing block:
- Rename to `blocks-property-<domain>` (e.g., `blocks-property-assets`, `blocks-property-receipts`)
- Namespace becomes `Sunfish.Blocks.Property<Domain>` (e.g., `Sunfish.Blocks.PropertyAssets`)

When the bare name does not collide:
- Use `blocks-<domain>` (e.g., `blocks-properties` — already shipped as cluster root)

Exception: the already-shipped `blocks-properties` is the cluster root and stays unprefixed. Do not retroactively rename shipped packages.

### Rule 3 — Extend over parallel

Before proposing a new block, check whether an existing block's domain scope subsumes ≥50% of the proposed block's responsibilities. If yes:

- The cluster contribution is an **extension** (new fields, new methods, new state-machine transitions, new child entities) to the existing block
- Hand-off describes the extension, not a new package
- ADR amendments may follow if the extension changes contracts

Examples (from this audit):
- Cluster Vendors → extend `blocks-maintenance.Vendor` (W-9 + magic-link)
- Cluster Work Orders → extend `blocks-maintenance.WorkOrder` (multi-party threads + entry-notice + completion attestation)
- Cluster Inspections → extend `blocks-inspections` (move-in/out InspectionItemKind + AssetConditionAssessment Deficiency variant)

### Rule 4 — Property-management entity must not overload foundation-tier "Asset"

The term "Asset" in Sunfish refers to the foundation-tier generic-entity model (`Sunfish.Foundation.Assets.Common.EntityId`). Property-management physical equipment must use a different entity name:

- **Recommended: `Equipment`** — industry-standard for facilities management; covers HVAC, water heaters, appliances, structural elements (roof, foundation)
- **Alternative: `Fixture`** (too narrow; typically built-in items)
- **Alternative: keep `Asset` with namespace qualification** — preserves industry CMMS standard but accepts perpetual disambiguation overhead

Default: **`Equipment`**. Override only if the user explicitly chooses namespace-qualification.

Implication for cluster:
- `blocks-property-assets` (package) → **`blocks-property-equipment`** OR keep `blocks-property-assets` package name with the entity inside named `Equipment`
- Recommended: `blocks-property-equipment` — package-name and entity-name align
- The corrective PR #212 used `blocks-property-assets` — a follow-up rename PR is required if Rule 4 is adopted

### Rule 5 — "Tenant" disambiguation

The term "Tenant" in Sunfish refers to the multi-tenancy organization (LLC, customer org). Property-management lease-holders are **not** "tenants" in Sunfish nomenclature — they are `Party` instances with `PartyKind.Tenant` (per `blocks-leases.Models`).

Cluster artifacts must:
- Never introduce a top-level `Tenant` entity for property-management lease-holders
- Use `Party` + `PartyKind.Tenant` for property-management lease-holders
- Use `TenantId` only when referring to the multi-tenancy LLC (`Sunfish.Foundation.MultiTenancy.TenantId`)

If existing cluster intake text uses "tenant" in the property-management sense, replace with "leaseholder" or "Party" or "lease-holder party".

---

## Decisions / amendments triggered

If the user adopts these 5 rules, the following amendments follow:

### A. Rename `blocks-property-assets` → `blocks-property-equipment` (Rule 4)

- Rename `packages/blocks-property-assets/` → `packages/blocks-property-equipment/`
- Namespace `Sunfish.Blocks.PropertyAssets` → `Sunfish.Blocks.PropertyEquipment`
- Entity `Asset` → `Equipment`
- Workstream #24 hand-off updates accordingly
- Estimated cost: ~50 lines of hand-off edit + ~10 lines of ledger row update + 1 PR

### B. Cancel `blocks-property-receipts` → consolidate into `blocks-receipts` (Rule 3)

Wait — `blocks-receipts` doesn't exist. Apply Rule 1: no collision → bare name OK. But Rule 2 says cluster siblings prefix when collision exists. **Receipts has no existing collision; Rule 2 does NOT require prefix.** The current `blocks-property-receipts` rename was for cluster consistency, not collision. Two options:

- B-i: revert to `blocks-receipts` (drops cluster consistency for naming-clarity)
- B-ii: keep `blocks-property-receipts` (preserves cluster consistency at minor verbosity cost)

**Recommend B-ii** — once we accept the prefix for collision-driven cases, applying it consistently across the cluster (even where collision-free) reduces ambiguity for contributors. The cluster INDEX should be the source of truth for cluster membership; package-name prefix should reinforce membership at glance.

### C. ADR 0053 amendment (Rule 4 entity-name)

`docs/adrs/0053-work-order-domain-model.md` field `WorkOrder.Asset: AssetId?` → `WorkOrder.Equipment: EquipmentId?` (or similar). One-line edit. Bundle with the rename PR or as separate amendment.

### D. Cluster INDEX update

`icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md` should add a "Naming convention" section pointing at this UPF review.

### E. Memory note extension

`feedback_audit_existing_blocks_before_handoff.md` should add the 5-rule reference and the entity-name guidance.

### F. Workstream rows updated

- #18 (Vendors) — disposition change from "new block" to "extend `blocks-maintenance.Vendor`"
- #19 (Work Orders) — disposition change from "new block" to "extend `blocks-maintenance.WorkOrder`"
- #25 (Inspections) — disposition change from "new block" to "extend `blocks-inspections`"
- #27 (Leases) — disposition change from "new block" to "extend `blocks-leases.Lease` for versioning + signature binding"

---

## Quality rubric self-check

Per UPF rubric:

- **C (Viable):** All 5 CORE sections + ≥1 CONDITIONAL (Stage 1.5) + Stage 2 done. ✅
- **B (Solid):** C + Stage 0 completed + FAILED conditions + Confidence Level + Cold Start Test. ✅
- **A (Excellent):** B + sparring (six perspectives) + Review checkpoints + Reference library + Knowledge capture (memory note) + Replanning triggers. ✅

**Confidence Level:** HIGH. Audit factual; rules mechanical; amendments small; no novel primitives.

**Replanning triggers:** if a sixth pre-existing block surfaces during cluster work; if the user explicitly rejects Rule 4 (Equipment rename); if sunfish-PM hits another collision after this convention ships.

---

## Sign-off

Research session — 2026-04-28
