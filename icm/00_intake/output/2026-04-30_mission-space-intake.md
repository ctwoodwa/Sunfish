# Intake — Sunfish Mission Space Matrix

**Date:** 2026-04-30
**Requestor:** CO (BDFL) via XO research session
**Request:** Map every dimension along which Sunfish features can be enabled / reduced / disabled / made unviable, plus the negotiation, transition, and migration semantics that govern moving between dimensional states.
**Pipeline variant:** `sunfish-gap-analysis`
**Stage:** 00 → 01 (proceed to discovery)
**UPF plan:** `~/.claude/plans/this-looks-pretty-comprehensive-concurrent-floyd.md` (Grade A; meta-UPF spot-check executed 2026-04-30)
**Active workstream:** W#33 in `icm/_state/active-workstreams.md` (`design-in-flight`)

---

## Problem Statement

Sunfish has fragmented, ADR-scattered coverage of the dimensions that gate which features a deployment can actually run (hardware, role/user, jurisdiction, runtime version, form factor, commercial tier, network, trust, sync state, lifecycle, migration, version vector). Three of those dimensions have no current artifact coverage at all — a minimum-spec negotiation protocol, runtime regulatory/jurisdictional evaluation, and the version-vector compatibility contract for mixed-version clusters. Without a unified analytical artifact, downstream consumers (pre-install UX, mixed-cluster operators, compliance reviewers, the iOS field-capture workstream W#23, the leasing pipeline W#22, and Phase 2 commercial-tier work) re-derive the answer from primary sources each time and risk inconsistency.

This research project produces a single canonical **Mission Space Matrix** discovery doc that maps every dimension to current coverage, identifies gaps, and queues 3–5 follow-on ADR-amendment intakes. The matrix is a *map*, not a *specification* — protocol design, regulatory evaluation logic, and version-vector contracts each become their own ADR amendments downstream.

## Naming

- **Umbrella concept:** **Sunfish Mission Space** — borrowed from NASA mission-concept doctrine ("the multi-dimensional bounded region within which a mission operates, covering environment, payload, objectives, time, and constraints").
- **Discovery deliverable:** **Mission Space Matrix**.
- **Why not "Capability Matrix":** collides with five existing Sunfish usages (`packages/foundation/Capabilities/` auth-proof closures, `packages/foundation-featuremanagement/` feature flags, `packages/federation-capability-sync/` federation tokens, `packages/blocks-public-listings/Capabilities/` macaroons, `packages/blocks-property-leasing-pipeline/Capabilities/` FHA gates).
- **Why not "Trade Space":** commerce/stock-trading connotation, problematic given Phase 2 property-business / accounting / billing scope.
- **Subordinate vocabulary** (introduced only in §6 Synthesis of the discovery): **Mission Envelope** (per-deployment instance of the Mission Space), **Mission Space Negotiation Protocol** (runtime layer; future ADR), **Mission Space Requirements** (install-UX layer; future ADR).

## Affected Areas

This research is systemic — all packages and accelerators are within the Mission Space's analytical reach. The matrix reads existing artifacts; it does not modify any package directly. Implementation work flows out via downstream ADR-amendment intakes.

- **foundation:** referenced (CP/AP per record class; schema epoch; vector clocks; managed-relay sustainability)
- **ui-core:** referenced (graceful-degradation taxonomy; per-feature force-enable surface)
- **ui-adapters-blazor / ui-adapters-react:** referenced (adapter parity dimension)
- **compat-telerik / compat-vendor pattern:** referenced (compat-vendor layer dimension)
- **blocks-\*:** consumed downstream by W#22 (regulatory/commercial-tier rows), W#23 (hardware/form-factor/version-vector rows), W#28 (form-factor/commercial-tier rows), W#31 (regulatory/classification rows)
- **apps/docs:** future install-time UX surface (Mission Space Requirements page); not modified by this research
- **apps/kitchen-sink:** not affected
- **tooling/scaffolding-cli:** not affected (could surface future capability-aware scaffolds)
- **accelerators/anchor / accelerators/bridge:** referenced (Zone A vs Zone C; per-zone capability profiles)

## Dependencies and Constraints

- **No hard blockers.** Naming-collision resolution is the only structural risk and is resolved by this intake.
- **30-day update check (Stage 0.8 re-run, executed 2026-04-30):** clean. Recent ADR amendments (0028-A1/A3/A4, 0048-A1, 0046-A2/A3/A4, 0061 amendments) all consistent with the meta-plan's coverage gradient; no in-flight ADR edits invalidate plan citations.
- **A4 spot-check (executed 2026-04-30):** 2 of 3 randomly-selected dimensions PASS predecessor-coverage check; 1 (Version Vector) FAILed substantive coverage but matches the plan's pre-identified "genuine gap — net-new" tag, so the coverage gradient holds.
- **Effort budget:** xhigh; ~10–14h XO/COB time (Phases 2–5 inclusive); ~90 min CO sparring/review across 5 phase gates. Hard stop: 18h cumulative.
- **Soft dependency:** Property-ops cluster outputs orthogonal but informative (W#22, W#23, W#28, W#31 will *consume* the matrix; they do not block its production).
- **Pipeline exit:** **"Approved Gap"** per `icm/pipelines/sunfish-gap-analysis/routing.md` — discovery doc + sign-off is sufficient closure; gap-closure work flows out as 3–5 follow-on ADR-amendment intakes.

## Dimensions in scope (target 8–12 top-level)

1. Hardware / environment (CPU, GPU, RAM, disk, network, power, sensors, trust hardware, display/input, storage tier, OS capability, accessibility)
2. Identity / user (provenance, role, subscription tier, quota state, device-trust, consent state, age/jurisdiction class)
3. Regulatory / jurisdictional (data residency, export control, industry compliance HIPAA/FERPA/PCI/SOC2/FHA, sanctions, EU AI Act tier)
4. Trust / security (device attestation, code-signing, network trust class, MFA enrollment)
5. Sunfish-architecture-native (CP/AP per record class, schema epoch, relay availability, cluster topology, sync state, zone A/B/C, adapter, compat-vendor layer)
6. Lifecycle / negotiation (discovery method, re-evaluation cadence, cache-vs-probe, graceful-degradation taxonomy, user communication, force-enable, telemetry)
7. Migration (snapshot portability, key transfer, data-loss-vs-feature-loss, cross-form-factor, forward compat, rollback semantics)
8. Version vector (kernel × plugin × adapter × schema-epoch × stable/beta channel × self-host/managed)
9. Form factor (laptop, desktop, tablet, watch, TV, IoT, headless)
10. Commercial tier (open-source, commercial, trial/preview, edition/SKU)

Anything beyond 12 → "track-as-deferred" row in the matrix; anything below 8 → gap risk.

## Deliverables

- **Stage 01:** `icm/01_discovery/output/2026-04-30_mission-space-matrix.md` — 5,500–7,500 words, 7-section structure mirroring the precedent `2026-04-30_microsoft-fabric-capability-evaluation.md`. Verdict table at top (each dimension × coverage tag {Specified, Partial, Gap} × confidence × recommended next step). Per-dimension §5 with 6-field schema (gate name, examples, current coverage cite, gap description, recommendation, confidence).
- **Stage 04 (synthesis):** 3–5 follow-on intake stubs at `icm/00_intake/output/2026-04-30_<gap-slug>-intake.md` covering the genuine gaps (current candidates: minimum-spec negotiation protocol, regulatory runtime evaluation, version-vector compatibility contract; +1–2 surfaced during research).
- **Stage 05 (handoff):** ADR-amendment intakes routed against predecessors {0028, 0031, 0046, 0048, 0061, 0009/0041} per Phase 4 synthesis. Recommended routing: amendments first; new ADR (~0062) for minimum-spec negotiation since no clean predecessor exists.

## Next Steps

Proceed to **Stage 01 Discovery**. Author drafts the Mission Space Matrix discovery doc per the meta-plan's Phase 3 acceptance criteria. CO review gate at end of Phase 3 (load-bearing review). Pedantic-Lawyer adversarial pass invoked once at Phase 3 review (~1h additional effort) per meta-plan §13.3.

## Cross-references

- UPF plan: `~/.claude/plans/this-looks-pretty-comprehensive-concurrent-floyd.md`
- Active workstream ledger: `icm/_state/active-workstreams.md` row W#33
- Project memory: `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_33_mission_space_matrix.md`
- Precedent discovery: `icm/01_discovery/output/2026-04-30_microsoft-fabric-capability-evaluation.md`
- Pipeline contract: `icm/pipelines/sunfish-gap-analysis/{README,routing,deliverables}.md`
- Foundational paper: `_shared/product/local-node-architecture-paper.md` (§2.2, §6.1, §7, §17.2, §20.7 are load-bearing for the matrix)
