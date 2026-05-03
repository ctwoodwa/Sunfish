# sunfish-inverted-stack-conformance Pipeline

**Purpose:** Deliver work that must be verifiable against the architectural specification of *The Inverted Stack: Local-First Nodes in a SaaS World* — and against Kleppmann's seven local-first properties (P1-P7) — using the catalog of foundational concepts in `the-inverted-stack/docs/reference-implementation/`.

## When to Use This Pipeline

Use this pipeline variant when the request involves work where the conformance dimension is the *primary* deliverable, not a side concern. Concrete signals:

- The work implements a primitive named in the book (or in the catalog at `concept-index.yaml` / `design-decisions.md`).
- The work is part of the Sunfish Business MVP (per `the-inverted-stack/docs/business-mvp/mvp-plan.md`).
- The work changes a Kleppmann-property-relevant subsystem (e.g., touches sync, identity, key custody, schema evolution, encryption-at-rest, plain-file export, multi-device data distribution).
- The work needs a baseline conformance scan and a delta against a prior baseline (per phase, per release, per regression).
- The work is governed by the architectural decision register at `the-inverted-stack/docs/decisions/` or by an ADR in this repo that cites the foundational paper.

**Do NOT use this pipeline for:**

- Pure feature work without a conformance dimension (→ `sunfish-feature-change`)
- Pure UI / component / adapter work (→ `sunfish-feature-change`)
- Breaking API changes that don't touch a paper-named primitive (→ `sunfish-api-change`)
- Test-only work without a conformance angle (→ `sunfish-test-expansion`)
- Documentation-only changes (→ `sunfish-docs-change`)

## Affected Sunfish Areas

Typical work areas:

- `packages/kernel-*/` — kernel primitives that map directly to paper §5 / §6 / §11
- `packages/foundation/`, `packages/foundation-persistence/`, `packages/foundation-multitenancy/` — substrate that conformance scans evaluate
- `accelerators/anchor/` — Zone A local-first node implementation
- `accelerators/bridge/` — Zone C hybrid implementation
- `docs/specifications/` — wire protocol, attestation, role-attestation specs
- `docs/adrs/` — architectural decisions citing the paper or the catalog
- `icm/01_discovery/output/` — baseline conformance reports + drift audits live here
- `waves/conformance/` — conformance scan outputs + per-phase deltas

## Typical Deliverables

| Stage | Key Deliverable |
|---|---|
| 00_intake | Intake note explicitly naming the paper section / primitive number / Kleppmann property the work implements or affects. Reference: `the-inverted-stack` repo paths, NOT copies thereof in Sunfish. |
| 01_discovery | Discovery report mapping current Sunfish surface against the paper's spec for the affected area. Identifies pre-existing primitives that satisfy the spec vs. gaps that need new code. **Required**: at least one baseline conformance scan output committed to `icm/01_discovery/output/` using the `local-first-properties` and/or `inverted-stack-conformance` skills. |
| 02_architecture | When the work introduces a new architectural decision (or amends an existing one), an ADR per Sunfish's existing `docs/adrs/` series. The ADR must cite the paper section it's grounded in OR the catalog primitive number. |
| 03_package-design | Per-package API surface for any new contracts. Public APIs that map to paper-named primitives must include XML doc comments referencing the paper section. |
| 04_scaffolding | Optional: scaffolding-CLI templates for paper-pattern adoption (e.g., "module that ships its own `ISunfishEntityModule` per ADR 0015"). |
| 05_implementation-plan | Ordered task list with explicit Kleppmann-property labels per task (P1/P2/P3/P4/P5/P6/P7). Each task must have a verifiable acceptance criterion stated as either (a) a conformance test that fails before the task and passes after, OR (b) a delta against the prior baseline scan. |
| 06_build | Code implementation in `/packages/`, `/apps/`, `/tooling/`, `/accelerators/`. Each PR must include the conformance test added in Stage 05 or a justification for deferral. |
| 07_review | Quality gates extended with conformance dimension: (a) all 4 Sunfish required CI checks pass; (b) conformance scan delta computed and within acceptable tolerance; (c) no regression against baseline; (d) ADR (if any) reviewed; (e) failed-conditions per the relevant primitive checked off. |
| 08_release | Changelog entry must name the paper section / primitive / property the change advances. Conformance baseline is re-scanned and the new baseline committed to `icm/01_discovery/output/`. |

## Stage Emphasis

This variant emphasizes Stages 01 (Discovery) and 07 (Review) more heavily than `sunfish-feature-change`. Discovery does the paper-vs-implementation gap analysis up-front so the work is grounded; Review verifies the resulting implementation actually satisfies the paper.

Stages 02 (Architecture) and 04 (Scaffolding) may be skipped via the fast-track path when the work is purely an implementation of an already-decided architecture (e.g., shipping a primitive whose ADR landed previously).

## Conformance Skills

Two skills are designed for this variant. Both live in the `the-inverted-stack` repo at `.claude/skills/`:

| Skill | Scope | When to invoke |
|---|---|---|
| `local-first-properties` | Foundational concepts (engine-agnostic) — P1-P7 + 538-entry catalog | At Stage 01 (baseline) and Stage 07 (delta). The lighter-weight scan; faster; works on any local-first repo regardless of CRDT engine choice. |
| `inverted-stack-conformance` | Full 562-concept catalog — book-specific architectural choices including Zone A/B/C, Flease, Appendix A wire protocol | At end of each phase (per `mvp-plan.md` §10 phase boundaries). The deeper scan; covers Sunfish's specific architectural choices. |

Both skills are **ICM-aware**: they detect Sunfish's `icm/` structure and write findings into `icm/01_discovery/output/` automatically. From a session that has the `the-inverted-stack` repo path available, invoke as you would any project-skill.

## Coordination with Other Variants

When a piece of conformance work also surfaces a feature need, an API change, or a test-coverage need, file a coordination intake against the other variant — DO NOT collapse multiple variants into one PR. The conformance variant's PR contains the conformance proof; the other variant's PR contains the feature/API/test work.

## See Also

- ICM root context: `../../CONTEXT.md`
- Routing guide: `../../_config/routing.md`
- Stage map: `../../_config/stage-map.md`
- Foundational paper (do not modify): `the-inverted-stack/_shared/product/local-node-architecture-paper.md`
- Concept catalog (do not modify): `the-inverted-stack/docs/reference-implementation/concept-index.yaml`
- Design decisions (do not modify): `the-inverted-stack/docs/reference-implementation/design-decisions.md`
- Business MVP plan (do not modify): `the-inverted-stack/docs/business-mvp/mvp-plan.md`
