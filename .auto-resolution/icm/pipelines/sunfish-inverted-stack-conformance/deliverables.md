# sunfish-inverted-stack-conformance Deliverables

Standard outputs expected at each stage when using this variant. Only the **bold** items are mandatory; the rest are conditional.

## Stage 00_intake

- **Intake note** at `icm/00_intake/output/<descriptor>-intake-<YYYY-MM-DD>.md` containing:
  - Paper section reference (§x.y in `the-inverted-stack/_shared/product/local-node-architecture-paper.md`)
  - Catalog primitive number (if applicable; see `the-inverted-stack/docs/reference-implementation/design-decisions.md` §5)
  - Kleppmann property labels (subset of P1-P7)
  - Affected Sunfish areas with impact markers (heavy / affected / possible / stable / new)
  - Open Questions list
  - Proposed first 3 milestones (if multi-week scope)
  - Pipeline-variant declaration ("sunfish-inverted-stack-conformance")
  - Reference to baseline conformance scan output (or "no prior baseline")

## Stage 01_discovery

- **Discovery report** at `icm/01_discovery/output/<descriptor>-discovery-<YYYY-MM-DD>.md` containing:
  - Per-Open-Question resolution status with evidence
  - Paper-vs-implementation gap analysis (per-element AS-PAPER / DRIFT / MISSING / ADDED-NOT-IN-PAPER verdicts)
  - Pre-existing primitives that satisfy the paper spec vs. gaps requiring new code
  - Proposed ADRs (if any) with draft titles and decision drivers
  - Recommended next-stage path (full pipeline vs. fast-track)
- **Baseline conformance scan output** at `icm/01_discovery/output/<descriptor>-{localfirst,conformance}-baseline-<YYYY-MM-DD>.md` produced by invoking the relevant skill (`local-first-properties` or `inverted-stack-conformance`)
- *Optional*: drift audit at `waves/architecture/<YYYY-MM-DD>-paper-vs-impl-drift-audit.md` using PR #147's pattern

## Stage 02_architecture

- *Conditional*: ADR at `docs/adrs/0xxx-<descriptor>.md` matching house style of recent ADRs (0037 onward)
- Each ADR must include:
  - **Resolves:** clause naming the Open Question(s) or audit finding it closes
  - **References** including the paper section and any catalog primitive numbers
  - **Revisit triggers** that would invalidate the decision

## Stage 03_package-design

- *Conditional*: API design document at `icm/03_package-design/output/<descriptor>-api-<YYYY-MM-DD>.md` covering:
  - Public types + members introduced or modified
  - XML doc comments referencing paper section per public type
  - Adapter parity considerations (if applicable)
  - Migration path for existing consumers (if breaking)

## Stage 04_scaffolding

- *Rare*: scaffolding-CLI templates at `tooling/scaffolding-cli/Templates/<template-name>/` codifying a paper-pattern

## Stage 05_implementation-plan

- **Implementation plan** at `icm/05_implementation-plan/output/<descriptor>-plan-<YYYY-MM-DD>.md` containing:
  - Ordered task list with dependencies
  - Per-task Kleppmann property labels (P1-P7 subset)
  - Per-task acceptance criterion (conformance test OR baseline-delta check)
  - Risk register (failure modes + mitigations)
  - Stage-completion checklist

## Stage 06_build

- Code in `/packages/`, `/apps/`, `/tooling/`, `/accelerators/` per the implementation plan
- Per PR:
  - Conformance test added (or deferred with tracked follow-up issue)
  - PR title + body conformant to ADRs 0039/0040/0042 (commitlint type-enum, ≤100-char subject, ≤100-char body lines)
  - Diff scope strictly within the affected-areas table from intake

## Stage 07_review

- **Conformance scan delta** at `icm/07_review/output/<descriptor>-conformance-delta-<YYYY-MM-DD>.md` containing:
  - Re-run of the same skill that produced the baseline
  - Per-property delta (P1-P7 if `local-first-properties`; per-concept if `inverted-stack-conformance`)
  - Regression report (any property that worsened)
  - Failed-conditions check per primitive touched
  - Kill-trigger check per primitive cluster touched
- ADR review notes (if any introduced or amended)
- Sign-off checklist (4 required CI checks pass + delta acceptable + no regressions)

## Stage 08_release

- Changelog entry at `CHANGELOG.md` naming the paper section / primitive / property advanced
- Updated baseline at `icm/01_discovery/output/<area>-baseline-<latest>.md` (the post-release conformance state becomes the next baseline)
- Per-phase release report at `waves/conformance/<phase>-exit-report-<YYYY-MM-DD>.md` (for phase-boundary work; e.g., end of Phase 1 of MVP)

## Templates

- ADR house-style template: see ADR 0037 / 0038 / 0044 for current shape
- Intake template: see `icm/00_intake/output/business-mvp-phase-1-foundation-intake-2026-04-26.md` for a worked conformance-variant intake
- Discovery report template: see `icm/01_discovery/output/business-mvp-phase-1-discovery-interim-2026-04-26.md` for a worked partial discovery
