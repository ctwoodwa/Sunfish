# sunfish-inverted-stack-conformance Routing

## Stage Navigation

This variant follows the default 9-stage ICM pipeline with emphasis tilted toward Stages 01 (Discovery) and 07 (Review). Use the fast-track path (`00_intake` → `05_implementation-plan` → `06_build` → `07_review` → `08_release`) for routine conformance work; expand to the full pipeline when introducing a new ADR or a new public API surface.

### Stage 00_intake

The intake note for this variant must include:

1. **Paper section reference** — the §x.y in `the-inverted-stack/_shared/product/local-node-architecture-paper.md` this work grounds in
2. **Catalog primitive number** — if the work implements a numbered primitive from `the-inverted-stack/docs/reference-implementation/design-decisions.md` §5
3. **Kleppmann property labels** — which of P1-P7 the work advances or affects
4. **Baseline reference** — link to the most recent conformance scan output, or "no prior baseline; first scan" if the area hasn't been measured

If any of these can't be filled in, this variant is the wrong fit — re-route to `sunfish-feature-change` or another variant.

### Stage 01_discovery — emphasized

Discovery for this variant produces TWO outputs in `icm/01_discovery/output/`:

1. **Paper-vs-implementation gap report** — narrative analysis mapping current Sunfish surface against the paper section / catalog primitive named in intake. Per-element verdicts (AS-PAPER / DRIFT / MISSING / ADDED-NOT-IN-PAPER per the pattern from PR #147's audit).
2. **Baseline conformance scan output** — invoke either `local-first-properties` (lighter, faster, engine-agnostic) or `inverted-stack-conformance` (deeper, full 562-concept catalog) skill from a session with the `the-inverted-stack` repo path available. The skill writes its output directly to `icm/01_discovery/output/`.

Discovery exit criterion: the gap is named, scoped, and measurable. Open Questions enumerated with a path to resolution.

### Stage 02_architecture — conditional

Required when:

- A new ADR is needed (cite the paper section)
- An existing ADR needs amendment
- The work introduces a cross-package contract that doesn't yet have one

Skipped when:

- The architecture is already settled (the work is an implementation of an already-accepted ADR)
- The work is purely incremental within an existing primitive

### Stage 03_package-design — conditional

Required when:

- A new public API surface is introduced
- Existing public API needs reshaping to align with paper

Skipped when:

- The work is internal-only
- The work is a test-only addition

### Stage 04_scaffolding — rarely needed

Only required when introducing scaffolding-CLI templates that codify a paper-pattern (e.g., generator for a new `ISunfishEntityModule` per ADR 0015).

### Stage 05_implementation-plan — emphasized

The plan must include:

- Per-task Kleppmann property labels (P1-P7)
- Per-task acceptance criterion: either a conformance test or a baseline-delta check
- Explicit ordering by dependency (e.g., "wire kernel-security primitive X before Anchor identity flow Y")
- Failure-mode handling: what happens if a conformance test fails mid-build?

### Stage 06_build

PRs must include the conformance test (added in Stage 05) OR a justification for deferral with a tracked follow-up.

### Stage 07_review — emphasized

Quality gates extended:

1. All 4 Sunfish required CI checks pass (Lint PR commits, Analyze csharp, CodeQL, semgrep — per ADR 0039)
2. **Conformance scan delta** — re-run the same skill that produced the baseline; compute delta; verify no regression
3. **Failed-conditions check** — for each primitive touched, run through the catalog's `failed-conditions` list; verify none triggered
4. **Kill-trigger check** — for each primitive cluster touched, run through the catalog's `kill-triggers` list; verify none triggered
5. ADR review (if any introduced or amended)
6. Conformance baseline update committed to `icm/01_discovery/output/`

### Stage 08_release

Changelog entry must name the paper section / primitive / property the change advances. The new conformance baseline becomes the reference for the next iteration.

## Cross-references

- Default stage map: `../../_config/stage-map.md`
- Default routing guide: `../../_config/routing.md`
- ADR 0039 (required-check minimalism): `../../../docs/adrs/0039-required-check-minimalism-public-oss.md`
- ADR 0015 (module-entity registration): `../../../docs/adrs/0015-module-entity-registration.md`
- Skill home: `the-inverted-stack/.claude/skills/{local-first-properties,inverted-stack-conformance}/SKILL.md`
