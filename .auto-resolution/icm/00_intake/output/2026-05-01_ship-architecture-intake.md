# Intake — Sunfish Ship Architecture Research

**Date:** 2026-05-01
**Requestor:** CO (BDFL) via XO research session
**Request:** Specify the ship-wide architecture for Sunfish's consolidated operator/admin/dev experience (Aspire-shaped) — the **two-layer model** (locations × responsibilities × deck-depth, composing as permission tuple `(role, location, deck, action)`), the v1 location list, the v1 responsibility hierarchy, the cross-axis primitives (OOD pattern + watch rotation + stretcher-bearer pattern + first-aid baseline), and the shared design system contract that downstream UI ADRs will compose on.
**Pipeline variant:** `sunfish-gap-analysis`
**Stage:** 00 → 01 (proceed to discovery)
**UPF plan:** `~/.claude/plans/sunfish-ship-architecture-research.md` (UPF v1.2 Grade A; meta-UPF pass executed 2026-05-01)
**Active workstream:** W#35 in `icm/_state/active-workstreams.md` (`design-in-flight`)

---

## Problem Statement

Sunfish has an Aspire-shaped consolidation gap. Configuration UX (W#34 Wayfinder) is specified but isolated; observability surfaces (logs / traces / metrics / health from `Microsoft.Extensions.Logging` + ADR 0049 audit) have infrastructure but no UI; recovery + identity (ADR 0046 + W#32) has substrate but no aggregate UX; per-tenant aggregation (W#29 Owner Web Cockpit) is in-flight but not architecturally framed; provider/integration config (ADRs 0013 + 0051 + 0052 + 0061) is contractually specified but lacks an operator surface; Phase 2 commercial admin (billing / subscriptions / customer ops) is entirely future. There is no canonical IA, no shared role/permission tuple, no shared design system, no consistent navigation pattern, no rotating-watch-officer concept (the OOD pattern surfaced during brainstorm), and no architectural guidance for how new departments are added in v2 (Wardroom multi-actor approval; Brig quarantine).

Without a unified Ship Architecture, every operator/admin/dev ADR re-derives navigation, permission, audit-tagging, and accessibility baseline from scratch. Late-stage UI inconsistency is the most expensive failure mode.

This research project produces the canonical **Sunfish Ship Architecture** discovery doc that maps the two-layer model, surveys consolidated-dashboard precedents, identifies coverage gaps per location and per role, and queues 3–6 follow-on ADR intakes (OOD pattern + Shared design system + per-department gaps). The matrix is a *map*, not a *specification* — each follow-on ADR specifies the actual contract.

## Naming + Architecture (locked 2026-05-01)

Per multi-pass brainstorm output (full reasoning in `project_workstream_35_ship_architecture_naming.md`):

**v1 Locations (7 + 2 v2):**

| # | Location | Sub-rooms |
|---|---|---|
| 1 | **Quarterdeck** | (flat — entry point + OOD station) |
| 2 | **Wayfinder** *(W#34 locked)* | Helm, Atlas, Standing Orders log, Radio Room, Periscope |
| 3 | **Engine Room** | Main Propulsion, Electrical, Damage Control, QA Workshop |
| 4 | **Tactical** | Sonar Room, Lookout, Fire Control |
| 5 | **Sick Bay** | Pharmacy, Lab, Atmosphere monitor |
| 6 | **Ship's Office** | (flat — Scribe's content domain) |
| 7 | **Supply Office** *(deferred to Phase 2 commercial)* | (flat) |
| 8 | **Wardroom** *(v2)* | (flat — multi-actor approval inbox) |
| 9 | **Brig** *(v2)* | (flat — quarantine) |

**v1 Responsibilities (8 roles + cross-cutting watch):**

| Tier | Roles | Primary location |
|---|---|---|
| Tenant ownership | Captain / XO | Quarterdeck |
| Department Heads | ENG / NAV / TAC | Engine Room / Wayfinder / Tactical |
| Division Officers | MPA / DCA / Comms Officer / Sonar Officer / Electrical Officer / QA Officer | Sub-rooms within their Department Head's location |
| Specialized Staff | IDC ("Doc") / Scribe / SUPPO | Sick Bay / Ship's Office / Supply Office (Phase 2) |
| Cross-cutting watch | OOD / EOOW | Quarterdeck (current shift) / Engine Room (current shift) |

**Cross-axis primitives:**
- Permission tuple: `(role, location, deck, action)` — composes to grant/deny decision
- Watch rotation: OOD designation rotates independently of Department-Head assignment; logged via Standing Orders
- Stretcher-bearer pattern: cross-trained Division Officers respond in adjacent departments
- First-aid baseline: every user surface inherits a baseline contextual-help layer regardless of role

## Affected Areas

This research is systemic — all packages and accelerators are within the Ship Architecture's analytical reach. Implementation work flows out via downstream ADR intakes.

- **foundation:** referenced (ADR 0007 bundle manifest; ADR 0028 CRDT; ADR 0049 audit; ADR 0009 FeatureManagement)
- **foundation-recovery:** Sick Bay's Pharmacy substrate (ADR 0046-A2 + W#32)
- **foundation-integrations:** Wayfinder's Radio Room (ADRs 0013 + 0051 + 0052)
- **foundation-taxonomy:** referenced (ADR 0056 — Wayfinder Atlas domain config)
- **ui-core:** the shared design system contract is load-bearing for every downstream UI ADR
- **ui-adapters-blazor / ui-adapters-react:** per-adapter rendering of locations
- **blocks-\*:** consumed downstream by W#22 / W#23 / W#28 / W#29 / W#31 / W#32 leads
- **apps/docs:** future Ship Architecture documentation
- **apps/kitchen-sink:** future Ship Architecture demonstration
- **accelerators/anchor + accelerators/bridge:** per-accelerator Ship Architecture rendering — Anchor is desktop-style ship; Bridge is web-admin ship

## Dependencies and Constraints

- **No hard blockers.** Naming-collision resolution is the only structural risk and is resolved by this intake (full reasoning in `project_workstream_35_ship_architecture_naming.md`).
- **30-day update check (Stage 0.8 re-run, executed 2026-05-01):** clean. **Notable**: ADRs 0062 + 0063 (Mission Space Negotiation Protocol + Requirements; both Status `Proposed` 2026-04-30/05-01 — council review in-flight per cohort discipline) inform Tactical Mission Envelope integration; ADR 0028 amendments A5/A6/A7/A8 shipped within last 24h informing version-vector + cross-form-factor migration patterns.
- **A4 spot-check (covered by W#35 meta-UPF executed 2026-05-01):** all cited predecessor ADRs verified existing. Coverage gradient (estimated; refined during Phase 3 drafting): Specified — Wayfinder (1); Partial — Engine Room, Sick Bay, Ship's Office, Captain/XO identity (4); Gap — Quarterdeck, Tactical, Supply Office, most Department Heads + Division Officers + Specialized Staff + OOD/EOOW + permission tuple + shared design system (rest).
- **Effort budget:** xhigh ~16–22h XO/COB time; ~120 min CO sparring/review across 5 phase gates. Hard stop 24h.
- **Soft dependencies:** outputs of W#33 follow-on authoring (~ADR 0064 regulatory still queued); W#34 follow-on outputs (~ADR 0065 Wayfinder + Standing Order will inform Quarterdeck's deck-log materialization; ~ADR 0068 security policy will inform OOD authority).
- **Pipeline exit:** **"Approved Gap"** per `icm/pipelines/sunfish-gap-analysis/routing.md`.

## Deliverables

- **Stage 01:** `icm/01_discovery/output/2026-05-01_ship-architecture.md` — 8,000–11,000 words, 9-section structure (Executive Summary + Research Question + Method + Substrate Recap + Per-location §5 + Per-responsibility §6 + Cross-axis primitives §7 + Synthesis §8 + Implementation Guidance §9 + consolidated-dashboard appendix). Two verdict tables (locations + roles). Per-location 6-field schema; per-role 6-field schema (symmetric per meta-UPF).
- **Stage 04 (synthesis):** 3–6 follow-on intake stubs at `icm/00_intake/output/2026-05-01_<gap-slug>-intake.md`. Confirmed for promotion: **OOD + Watch rotation** (bundled — interdependent primitives), **Shared design system** (load-bearing for all downstream UIs). Plus 1–4 per-department new-ADRs as gaps surface during drafting.
- **Stage 05 (handoff):** ADR-amendment intakes for predecessors that need updates (likely ADR 0046 amendment for Sick Bay aggregation; ADR 0036 amendment for Quarterdeck pulse rendering; ADR 0049 amendment for OOD-tagging).

## Next Steps

Proceed to **Stage 01 Discovery**. Author drafts the Ship Architecture discovery doc per the meta-plan's Phase 3 acceptance criteria. CO review gate at end of Phase 3 (load-bearing review). **Stage 1.5 narrow exception**: WCAG / a11y subagent dispatched once at Phase 3 review on §7 (cross-axis primitives — shared design system) + per-location §5 surfaces (~1.5h additional effort) per meta-plan §13.3.

## Cross-references

- UPF plan: `~/.claude/plans/sunfish-ship-architecture-research.md`
- Active workstream ledger: `icm/_state/active-workstreams.md` row W#35
- Project memory (naming + architecture): `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_35_ship_architecture_naming.md`
- Precedent discovery (W#33 Mission Space): `icm/01_discovery/output/2026-04-30_mission-space-matrix.md`
- Precedent discovery (W#34 Wayfinder): `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md`
- W#33 research-methodology playbook: `~/.claude/plans/mission-space-research-methodology.md`
- Pipeline contract: `icm/pipelines/sunfish-gap-analysis/{README,routing,deliverables}.md`
- Predecessor ADRs: 0007 + 0009 + 0013 + 0028 (+A1–A8) + 0029 + 0032 + 0036 + 0041 + 0043 + 0046 + 0046-a1 + 0048 + 0048-A1 + 0049 + 0051 + 0052 + 0055 + 0056 + 0057 + 0057-A1 + 0061 + 0062 (Proposed) + 0063 (Proposed)
