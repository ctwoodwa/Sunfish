---
id: 71
title: ADR Portfolio System (Event-Sourcing-with-Snapshots)
status: Proposed
date: 2026-05-02
tier: tooling
pipeline_variant: sunfish-quality-control

concern:
  - governance
  - dev-experience

enables:
  - adr-discoverability-o1
  - supersession-tracking
  - drift-detection
  - gap-visibility
  - quarterly-architecture-snapshot

composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0071 — ADR Portfolio System (Event-Sourcing-with-Snapshots)

**Status:** Proposed
**Date:** 2026-05-02
**Authors:** XO research session
**Pipeline variant:** `sunfish-quality-control`
**Tier:** tooling

---

## Context

By May 2026, Sunfish had 65 Architecture Decision Records accumulated across roughly 14 months of
active development. Each ADR is a durable record: written, reviewed, and committed; never edited
retroactively. That append-only discipline is correct — ADRs are architecture history. But it
created an emerging problem: **a purely chronological journal scales O(N) for every "current
state" query.**

Concrete pain points observed in the 2026 development cycle:

- "Which ADRs touch security?" required manually scanning all 65 files or running ad-hoc grep
  across `docs/adrs/`.
- "Was ADR 0026 superseded?" required opening the file, reading the `**Status:**` line, and
  chasing the supersession link — no machine-readable aggregate.
- "What's the tier distribution of our ADRs?" had no answer short of reading every header.
- Cross-ADR relationship graphs (`composes`, `extends`, `supersedes`) existed in prose but were
  not extractable without parsing natural-language references.
- The 2026-Q2 quarterly snapshot review cycle flagged **"tooling tier" as a gap**: only one
  prior ADR (ADR 0042 — Subagent-Driven Development) carried `tier: tooling`, and the
  scaffolding CLI, Roslyn analyzers, and ADR projection tooling were entirely undocumented at
  the architectural decision level (Stage 5 quarterly snapshot review, PR #487).

These pain points are exactly the set of problems event-sourced systems solve. The journal
(ADRs) is the source of truth; derived read-models (projections) serve "current state" queries
efficiently; periodic snapshots prevent having to replay the full log to understand the
present state of the architecture.

The Stage 5 snapshot review (PR #487) also revealed a **structural-citation failure pattern**
in the 2026-04-29 cohort of substrate ADRs: approximately 65% of amendments in a representative
cohort of 19 substrate ADR amendments required council corrections before merge. The dominant
failure mode was cited-symbol drift — either symbols that had been renamed, symbols that did
not yet exist and were not marked "introduced by this ADR," or cross-ADR claims that could
not be verified against the cited ADR's text. Manual grep was the only tool available; there
was no systematic way to detect when an ADR's relationship fields drifted from reality.

This ADR formalizes the architectural decision that was operationalized across PRs
#481/#483/#484/#485/#487/#490 in the 2026-05-02 sprint.

---

## Decision drivers

- **O(1) discoverability.** "Which ADRs touch security?" must read a single derived file
  (`INDEX.md`), not grep 65+ source files. As the portfolio grows to 100+ ADRs this matters.
- **Supersession tracking.** Consumers must be able to determine in one lookup whether an ADR
  they depend on has been superseded — without opening every ADR.
- **Drift detection.** Relationship metadata (composes, extends, consumed_by) must be derivable
  from the journal so that inconsistencies surface as validation errors, not runtime surprises.
- **Gap visibility.** Tier and concern distributions must be queryable — the quarterly snapshot
  review process exposed a whole class of "tooling" decisions that had never been formally
  documented.
- **Immutability of the journal.** No approach may require renumbering, merging, or editing
  existing ADR bodies. ADR numbers are immutable IDs; every cross-reference in 65 ADRs, CI
  config, and memory files would break if numbers changed.
- **Zero new runtime dependencies.** The projection tool must run in CI without installing
  non-stdlib Python packages. CI gates depend on this; adding a `PyYAML` or `pydantic` dep
  means every fresh environment needs package installation before validation can run.
- **Human-readable fallback.** The journal must remain readable without tooling. A contributor
  who does not run the projection tool must still be able to read the ADR body and understand
  the decision.
- **Cohort batting average pressure.** The ~65% structural-citation failure rate in the
  2026-04-29 cohort established that machine-readable metadata reduces a class of pre-merge
  council corrections from "found by reviewing prose" to "caught by validator."
- **Quarterly architecture legibility.** The quarterly snapshot process (PR #487) surfaced the
  need for a hand-curated narrative layer that is neither as granular as individual ADRs nor
  as abstract as the foundational paper.

---

## Considered options

### Option A — Renumber ADRs by topic (e.g., 0001–0099 = kernel, 0100–0199 = foundation)

Group ADRs into topic ranges during a one-time renumbering. Topical grouping becomes implicit
in the file number.

**Pro:** No tooling needed; topic is visible from the filename.

**Con:**
- Breaks every cross-reference: 65 ADRs × average 3 `composes`/`extends`/`supersedes`
  references = ~195 hard cross-references to update; plus CI config, memory files,
  `active-workstreams.md`, hand-off specs, and the foundational paper.
- Topic boundaries are fuzzy and shift over time. An ADR that starts as `kernel` may be
  partially superseded by a `foundation` ADR; no static range assignment handles this.
- Renumbering severs the commit-log connection between an ADR and its review history.
- Fundamentally does not scale — a range filled to capacity requires another renumbering.

**Verdict:** Rejected. The cost is high, the benefit is cosmetic, and the approach does not
solve "current state" queries — it only reorganizes the problem.

### Option B — Replace with a wiki or linked document system

Migrate architecture decisions to a wiki (e.g., GitHub Wiki, Notion, Confluence). Use wiki
linking and categories to provide topical navigation.

**Pro:** Rich navigation; no parsing needed; familiar to many contributors.

**Con:**
- Loses the audit trail. A wiki edit is not a pull request; there is no review gate, no
  approval record, and no commit history anchoring the decision to the state of the codebase
  at the time it was made.
- Breaks offline-first discipline. Sunfish is a local-first framework; the architecture
  documentation should be readable from a local clone without an internet connection.
- Duplicates rather than derives. A wiki "current state" page requires manual maintenance;
  it becomes stale the moment someone merges an ADR without updating the wiki.
- Eliminates CI validation. The projection tool's `--check-only` mode runs in CI; there is
  no equivalent for a wiki.

**Verdict:** Rejected. Auditable history is the core value of the ADR practice; a wiki trades
it for convenience that the projection approach provides more cheaply.

### Option C — Journal + projections + snapshots (four-layer event-sourcing model) [RECOMMENDED]

Retain ADRs as an append-only journal. Add a lightweight YAML frontmatter block to every ADR
(required fields: `id`, `title`, `status`, `date`, `tier`; optional: `concern`, `composes`,
`extends`, `supersedes`, relationship arrays). Build a pure-stdlib Python projection tool that
reads frontmatter and emits derived read-models (`STATUS.md`, `INDEX.md`, `GRAPH.md`).
Add a quarterly hand-curated snapshot that synthesizes the projection output into a narrative
layer. Define the foundational paper as the stable long-horizon layer.

**Pro:**
- Journal is append-only and immutable. No renumbering; no broken cross-references.
- Projections are derived from the journal; they cannot drift from reality (the tool emits
  them; it does not accept them as inputs to decision logic).
- Pure stdlib Python — no installation step in CI. `--check-only` mode validates every ADR
  without emitting output files.
- Human-readable at every layer. The frontmatter block is a handful of YAML lines at the top
  of the ADR; the ADR body is unchanged.
- `consumed_by` derivation (PR #484) means authors do not need to update an ADR's metadata
  every time another ADR references it — the reverse-link is computed.
- Quarterly snapshots decompose the gap between "read the full log" (65 ADRs) and "read the
  foundational paper" (high-level narrative), making architecture legible to new contributors.
- CI validation (PR in flight via `adr-validation.yml`) catches malformed frontmatter before
  merge.

**Con:**
- Requires a one-time migration to add frontmatter to all existing ADRs (56-ADR bulk apply,
  PR #483). Migration is mechanical but large.
- Projection tool is a custom tool; contributors must understand it exists and run it when
  authoring ADRs.
- Controlled vocabularies (`tier`, `concern`, etc.) need maintenance as the architecture
  evolves; adding a vocabulary value requires updating both `project.py` and `_FRONTMATTER.md`.

**Verdict:** Adopted. The event-sourcing pattern is the correct model for an append-only
journal with derived current-state queries. The migration cost is one-time; the tooling
overhead is small and fully validated in CI.

---

## Decision

**Adopt Option C.** The Sunfish ADR portfolio is structured as a four-layer
event-sourcing-with-snapshots system:

```
Layer 4 — Foundational paper         (stable long-horizon narrative)
Layer 3 — Quarterly snapshot         (hand-curated, per-quarter synthesis)
Layer 2 — Projections                (auto-derived read-models from frontmatter)
Layer 1 — Journal                    (append-only ADRs; source of truth)
```

### Layer 1 — Journal

`docs/adrs/` contains the append-only journal of architecture decisions. Files are named
`NNNN-short-slug.md` where `NNNN` is a sequentially assigned, zero-padded, immutable integer.
ADR bodies are never edited retroactively. Supersession is accomplished by authoring a new
ADR and setting the old ADR's `superseded_by` field plus `status: Superseded`. Amendment
variants (e.g., `0046-a1-historical-keys-projection.md`) extend an ADR without replacing it.

### Layer 2 — Projections

Three auto-derived read-models live in `docs/adrs/` alongside the journal:

| File | Content |
|---|---|
| `STATUS.md` | All ADRs grouped by current status (Proposed / Accepted / Superseded / Deprecated / Withdrawn). |
| `INDEX.md` | Topical index — ADRs grouped by `tier` and by `concern`. |
| `GRAPH.md` | Mermaid dependency graph derived from `composes` / `extends` / `supersedes` edges. |

Projections are generated by `tools/adr-projections/project.py` and **committed to the
repository** so they are visible in PR diffs and readable without running the tool.
They are never the source of truth; they are always rebuildable from the journal.

The `consumed_by` field in the frontmatter schema is also tooling-derived: `project.py`
computes reverse-links from the `composes`/`extends` arrays of other ADRs when generating
projections. Authors do not maintain `consumed_by` manually.

### Layer 3 — Quarterly snapshot

`docs/architecture/snapshot-YYYY-QX.md` is a hand-curated narrative synthesizing the current
state of the architecture across all layers. The first instance was produced in PR #487
(`snapshot-2026-Q2.md`) and covers 65 ADRs, 10 tier categories, 20 concern categories,
and the current accelerator map.

Snapshots are updated quarterly by the XO research session, or when a major wave of ADRs
lands that materially changes the architecture picture. They are not auto-generated.

### Layer 4 — Foundational paper

`_shared/product/local-node-architecture-paper.md` serves as the long-horizon stable narrative.
It is a synced copy of *Inverting the SaaS Paradigm* (v10.0, April 2026). The paper covers
the kernel/plugin split, four-tier UI layering, CP/AP per-record-class, event-sourced ledger,
schema epochs, managed-relay sustainability, and compat-vendor-adapter pattern. It changes
infrequently and is authoritative for architectural intent.

### YAML frontmatter schema

Every ADR (new and existing) carries a YAML frontmatter block before the H1 title. The schema
is fully specified in `docs/adrs/_FRONTMATTER.md`. The minimum required fields are:

```yaml
---
id: 71
title: ADR Portfolio System (Event-Sourcing-with-Snapshots)
status: Proposed
date: 2026-05-02
tier: tooling
---
```

Controlled vocabularies:

- **`tier`** (10 values): `foundation` / `kernel` / `ui-core` / `adapter` / `block` /
  `accelerator` / `governance` / `policy` / `tooling` / `process`
- **`concern`** (20 values): `security` / `persistence` / `ui` / `accessibility` /
  `regulatory` / `distribution` / `multi-tenancy` / `audit` / `identity` /
  `capability-model` / `configuration` / `observability` / `threat-model` /
  `governance` / `dev-experience` / `operations` / `commercial` / `mission-space` /
  `data-residency` / `version-management`
- **`status`** (5 values): `Proposed` / `Accepted` / `Superseded` / `Deprecated` / `Withdrawn`
- **`pipeline_variant`** (7 values): from ICM — `sunfish-feature-change` /
  `sunfish-api-change` / `sunfish-scaffolding` / `sunfish-docs-change` /
  `sunfish-quality-control` / `sunfish-test-expansion` / `sunfish-gap-analysis`

Adding a vocabulary value requires updating both `project.py` (`VALID_*` sets) and
`_FRONTMATTER.md` (the spec document).

### Projection tool contract

`tools/adr-projections/project.py` is the canonical projection tool. It:

1. Scans `docs/adrs/[0-9][0-9][0-9][0-9]-*.md` for frontmatter.
2. Validates per the 12 rules in `_FRONTMATTER.md`.
3. With no flags: emits `STATUS.md`, `INDEX.md`, `GRAPH.md`.
4. With `--check-only`: validates without writing output; exits non-zero on errors.
5. Uses Python 3 stdlib only — no third-party dependencies.

CI runs `python3 tools/adr-projections/project.py --check-only` on every PR touching
`docs/adrs/`. Validation failures block merge.

### Migration

The one-time migration (`bulk_apply_frontmatter.py`, shipped in PR #483) applied frontmatter
to all 56 ADRs that predated the schema introduction (PRs #481 introduced the schema + 4-ADR
pilot). The tool is retained at `tools/adr-projections/bulk_apply_frontmatter.py` as a
reference for future bulk operations.

A follow-on pass (PR #485) refined `concern` tags based on body analysis, correcting cases
where the initial bulk pass assigned incorrect or missing tags. A further pass (PR #490, open)
backfills `composes`/`extends` cross-references across all 61 ADRs to complete the dependency
graph.

### Immutability invariants

The journal is append-only. This means:

- **Do not renumber ADRs.** Numbers are immutable IDs; every cross-reference breaks.
- **Do not edit ADR bodies retroactively.** If the decision was wrong, supersede with a new
  ADR. If only the metadata was wrong, update the frontmatter (frontmatter is metadata, not
  the decision record itself).
- **Do not merge ADRs.** Even when two ADRs look similar, they record distinct decisions at
  distinct points in time. Keep originals; supersede when appropriate.
- **Projections are not authoritative.** Never treat `STATUS.md` or `INDEX.md` as the source
  of truth; always trace back to the ADR if there is any doubt.

---

## Consequences

### Positive

- **O(1) current-state queries.** "Which ADRs touch multi-tenancy?" reads `INDEX.md` (one
  file, filtered section) rather than grepping 65+ source files.
- **Drift detection.** Inconsistent `composes`/`extends` cross-references surface as
  validation errors in CI before merge, not months later during architectural review.
- **Supersession chain legibility.** `STATUS.md` shows the complete superseded set; following
  `superseded_by` links is mechanical.
- **Tier gap visibility.** The projection tool's tier distribution immediately reveals
  underdocumented areas (as it did for `tooling` tier in the Stage 5 review that prompted
  this ADR).
- **CI enforcement.** `--check-only` in the ADR validation workflow means malformed frontmatter
  cannot merge; new ADRs inherit the discipline automatically.
- **Quarterly review cadence.** The snapshot layer (Layer 3) makes architecture legible to new
  contributors and to the CO without requiring them to read all 65+ ADRs.
- **Reduced pre-merge council corrections.** Machine-readable relationship metadata reduces
  the subset of council findings that arise from manual cross-reference errors. In the
  2026-04-29 cohort, ~65% of amendments required corrections; the structural-citation
  validator directly addresses this failure mode.

### Negative

- **Ongoing vocabulary maintenance.** Each new tier or concern value requires a two-file
  update (`project.py` + `_FRONTMATTER.md`). Low-frequency but non-zero cost.
- **Projection regeneration discipline.** Contributors who author ADRs must run
  `python3 tools/adr-projections/project.py` to update the committed projections. CI will
  not catch a stale projection (it only validates frontmatter, not projection freshness).
- **Frontmatter on every ADR.** Authoring an ADR now has a required additional step. The
  template enforces this, and CI validates it, but it is additional cognitive overhead.
- **Custom tool, not an ecosystem standard.** The projection tool is purpose-built; there is
  no upstream community maintaining it.

### Trust impact / Security and privacy

No security or privacy impact. The ADR portfolio system manages documentation metadata only.
There are no secrets, no PII, and no cryptographic operations involved.

---

## Compatibility plan

This ADR formalizes a pattern that was already shipped. All 65 ADRs in `docs/adrs/` at the
time of this writing carry frontmatter applied via the bulk migration (PRs #481/#483/#485).
The projection tool is live and in use. The quarterly snapshot for 2026-Q2 exists.

ADRs 0066 onward (not yet authored as of 2026-05-02) must carry frontmatter from the moment
of initial draft. The `_template.md` enforces this by including the frontmatter block at the
top of the template.

No packages are affected. This ADR is `tier: tooling` — it governs documentation tooling
only.

---

## Implementation checklist

These items were completed prior to this ADR being authored. The checklist serves as a
post-hoc verification that the full pattern was implemented:

- [x] `docs/adrs/_FRONTMATTER.md` — schema spec authored (PR #481).
- [x] `docs/adrs/_template.md` — updated to include frontmatter block as new convention
  (PR #481).
- [x] `tools/adr-projections/project.py` — projection tool MVP with `--check-only` flag
  (PR #481); `consumed_by` derivation added (PR #484).
- [x] `tools/adr-projections/README.md` — tool documentation (PR #481).
- [x] `tools/adr-projections/bulk_apply_frontmatter.py` — one-time migration script
  (Stage 4, PR #483).
- [x] 4-ADR pilot frontmatter applied (ADRs 0001, 0028, 0049, 0062) — PR #481.
- [x] 56-ADR bulk frontmatter apply — PR #483.
- [x] Concern-tag refinement pass — PR #485.
- [x] `docs/architecture/snapshot-2026-Q2.md` — first quarterly snapshot (PR #487).
- [x] `composes`/`extends` backfill across 61 ADRs — PR #490 (open at time of authoring).
- [x] `docs/adrs/README.md` updated to reference INDEX/STATUS/GRAPH — PR #483.
- [ ] CI workflow `adr-validation.yml` — in progress (worktree `sunfish-adr-ci-wt` at
  time of authoring; pending PR).
- [ ] ADR 0071 (this document) accepted — CO sign-off required.

---

## Open questions

1. **Projection freshness in CI.** The current CI gate validates frontmatter (via
   `--check-only`) but does not verify that committed `INDEX.md`/`STATUS.md`/`GRAPH.md` are
   up to date with the current frontmatter. A future enhancement could run the projection
   tool without `--check-only` and diff against the committed output, failing if stale. This
   is deferred — the current pattern (contributor runs tool locally; CI only validates) is
   sufficient while the team is small.

2. **Concern vocabulary expansion.** The current 20-item `concern` vocabulary was designed
   in the 2026-05-01 sprint. As new domains (e.g., AI inference, hardware interfaces, formal
   verification) enter the portfolio, new concern tags may be needed. Expansion requires a
   small PR touching `project.py` + `_FRONTMATTER.md`. No action now; flag when the first
   uncovered concern is encountered.

3. **Amendment versioning.** Amendments are tracked as strings in the `amendments` array
   (e.g., `["A1", "A2.1"]`). The current schema does not capture the date or status of each
   amendment. A future frontmatter amendment (pun intended) could add per-amendment metadata.
   Deferred until there is a concrete use case (e.g., an amendment that is itself superseded).

---

## Revisit triggers

- **ADR count exceeds 200.** At 200+ ADRs, the Python tool's linear scan performance and the
  snapshot's hand-curation effort should be re-evaluated. Consider a proper ADR database or
  static-site generator.
- **CI tool chain changes.** If Python 3 is removed from the CI environment or if the
  repository migrates to a non-GitHub CI platform, re-evaluate the validator.
- **Team grows beyond 10 contributors.** The current "contributor runs projection tool
  locally" workflow breaks down with more contributors. At that point, enforcing projection
  freshness in CI (open question #1) becomes load-bearing.
- **First multilingual ADR requirement.** If ADRs need to be authored in multiple languages,
  the frontmatter-as-YAML pattern may need a language tag.

---

## References

### Portfolio system files

- [`docs/adrs/_FRONTMATTER.md`](./_FRONTMATTER.md) — frontmatter schema specification (v1).
- [`docs/adrs/_template.md`](./_template.md) — ADR template; carries frontmatter block.
- [`tools/adr-projections/project.py`](../../tools/adr-projections/project.py) — projection
  tool (pure stdlib Python 3).
- [`tools/adr-projections/README.md`](../../tools/adr-projections/README.md) — tool docs.
- [`tools/adr-projections/bulk_apply_frontmatter.py`](../../tools/adr-projections/bulk_apply_frontmatter.py)
  — one-time migration script (kept as reference).
- [`docs/architecture/snapshot-2026-Q2.md`](../architecture/snapshot-2026-Q2.md) — first
  quarterly snapshot.

### PRs that shipped this pattern

- **PR #481** — portfolio foundation: `_FRONTMATTER.md` schema + projection tool MVP +
  4-ADR pilot + `_template.md` update.
- **PR #483** — Stage 4: bulk frontmatter apply across 56 ADRs (61 total covered).
- **PR #484** — `consumed_by` auto-derivation added to `project.py` (open at time of
  authoring).
- **PR #485** — concern-tag refinement pass based on ADR body analysis.
- **PR #487** — first quarterly snapshot (`docs/architecture/snapshot-2026-Q2.md`),
  Stage 5 of ADR portfolio foundation; also surfaced the `tooling` tier gap that prompted
  this ADR.
- **PR #490** — `composes`/`extends` cross-reference backfill across 61 ADRs (open at time
  of authoring).

### Predecessor ADRs (governance and process tier)

- [ADR 0018](./0018-governance-and-license-posture.md) — Governance Model and License
  Posture; establishes BDFL + MIT + ICM stack as the governance frame that this ADR's tooling
  supports.
- [ADR 0037](./0037-ci-platform-decision.md) — CI Platform Decision; the CI environment in
  which `--check-only` runs.
- [ADR 0038](./0038-branch-protection-via-rulesets.md) — Branch Protection via Rulesets;
  the enforcement mechanism that ensures the CI gate cannot be bypassed.
- [ADR 0042](./0042-subagent-driven-development-for-high-velocity.md) — Subagent-Driven
  Development; the development model that motivated the high ADR velocity and thus the
  discoverability problem this ADR solves.
- [ADR 0069](./0069-adr-authoring-discipline.md) — ADR Authoring Discipline
  (pre-merge council + §A0 + three-direction verification); the companion discipline ADR
  that governs how individual ADRs are authored and reviewed. ADR 0071 governs the
  portfolio system; ADR 0069 governs individual ADR authoring.
- [ADR 0070](./0070-multi-session-naval-org-structure.md) — Multi-Session Naval-Org
  Structure; documents the multi-session coordination model that drives concurrent ADR
  authoring and makes the portfolio system's machine-readable metadata especially valuable.

### Foundational paper

- `_shared/product/local-node-architecture-paper.md` — *Inverting the SaaS Paradigm*,
  v10.0 April 2026. Layer 4 in the four-layer model. §20.7 covers accelerator zones
  referenced in the snapshot.

---

## §A0 Pre-acceptance audit

### AHA pass

Three alternatives were considered (renumber by topic, wiki replacement, journal+projections).
Renumbering was evaluated first (obvious; rejected quickly for cross-reference breakage).
Wiki was the second obvious approach (familiar tooling; rejected for audit-trail loss).
The journal+projections approach survives both challenges and maps cleanly to event-sourcing
patterns already used in the Sunfish kernel (ADR 0003 event-bus, ADR 0049 audit substrate).
No simpler alternative was identified. *(Anti-pattern #10: checked.)*

### FAILED conditions / kill triggers

This decision should be reversed or abandoned if:

1. The Python 3 projection tool proves impossible to maintain in CI (e.g., CI environment
   drops Python 3 without a migration path, and no stdlib-only alternative can be found
   within one sprint).
2. The frontmatter overhead measurably slows ADR authoring to the point where ADR adoption
   declines (observable signal: ADR rate drops below 1/week for 4+ consecutive weeks after
   a direct attribution to the frontmatter requirement, not other causes).
3. A maintained, well-supported OSS ADR portfolio tool emerges that provides a superset of
   this system's capabilities with lower maintenance overhead. *(Anti-pattern #11: checked.)*

### Rollback strategy

The frontmatter block is additive; removing it from ADRs requires a simple script that strips
the `---\n...\n---\n` block from each file. The projection tool can be deleted. The journal
(ADR bodies) is unaffected. Rollback is a single PR removing `tools/adr-projections/`,
`docs/adrs/_FRONTMATTER.md`, `docs/adrs/STATUS.md`, `docs/adrs/INDEX.md`,
`docs/adrs/GRAPH.md`, and the frontmatter blocks from all ADRs. The ADRs themselves remain
intact. *(Anti-pattern #4: checked.)*

### Confidence level

**HIGH.** The system described here has already been built and is in production use across 65+
ADRs with `--check-only` passing. This ADR formalizes an observed working pattern, not a
speculative design. The main risk is vocabulary maintenance overhead, which is low-frequency.

### Cited-symbol verification

This ADR is `tier: tooling` — it documents tooling files and file paths, not Sunfish C#
symbols. There are no `Sunfish.*` namespace references to verify. The file paths cited
(`docs/adrs/_FRONTMATTER.md`, `tools/adr-projections/project.py`, etc.) have been verified
to exist in the worktree at `/tmp/sunfish-adr-0071-wt/`.

Cross-ADR claims verified:
- ADR 0018 exists at `docs/adrs/0018-governance-and-license-posture.md` — confirmed.
- ADR 0037 exists at `docs/adrs/0037-ci-platform-decision.md` — confirmed.
- ADR 0038 exists at `docs/adrs/0038-branch-protection-via-rulesets.md` — confirmed.
- ADR 0042 exists at `docs/adrs/0042-subagent-driven-development-for-high-velocity.md` —
  confirmed.
- ADRs 0069 and 0070 are in open PRs #488 and #489 respectively; they will exist in main
  before or concurrent with this ADR merging. Marked explicitly as "open PR" above.
*(Anti-pattern #21: checked.)*

### Anti-pattern scan

- AP-1 (unvalidated assumptions): The ~65% structural-citation failure rate cited in Context
  is derived from the 2026-04-29 cohort observation documented in the quarterly snapshot (PR
  #487) and in memory file `project_adr_portfolio_foundation_pattern.md` ("pre-merge council
  canonical; cohort batting average 19-of-19 substrate amendments needed council fixes"). The
  exact percentage is approximate. Marked as a directional claim, not a precise measurement.
- AP-3 (vague success criteria): FAILED conditions are explicit.
- AP-4 (no rollback): Rollback strategy present.
- AP-9 (skipping Stage 0): Three alternatives evaluated.
- AP-11 (zombie project): Revisit triggers named.
- AP-12 (timeline fantasy): No timeline claims; system is already built.
- AP-21 (assumed symbols): No C# symbols cited; file paths verified.
*(Anti-pattern #13: confidence is evidence-backed — system is in production.)*

### Revisit triggers

Named in the Revisit triggers section above: ADR count >200, CI tool chain changes, team
>10 contributors, multilingual ADR requirement.
*(Anti-pattern #11 second half: checked.)*

### Cold Start Test

A fresh contributor reading this ADR can:
1. Find the frontmatter schema at `docs/adrs/_FRONTMATTER.md`.
2. Find the projection tool at `tools/adr-projections/project.py`.
3. Run `python3 tools/adr-projections/project.py --check-only` to validate.
4. Run `python3 tools/adr-projections/project.py` to regenerate projections.
5. Understand the four-layer model from the Decision section.
6. Find the quarterly snapshot at `docs/architecture/snapshot-2026-Q2.md`.

No author clarification required. *(Stage 2 Check 5: passed.)*

### Sources cited

- The ~65% structural-citation failure rate: derived from the 2026-04-29 cohort observation.
  The `project_adr_portfolio_foundation_pattern.md` memory states "pre-merge council canonical
  (cohort batting average 19-of-19 substrate amendments needed council fixes)"; the quarterly
  snapshot Stage 5 review (PR #487) used this to identify the gap. Approximate, not
  a precise audit measurement.
- "Tooling tier gap" identified in Stage 5 snapshot review: PR #487 is the source;
  snapshot-2026-Q2.md is the artifact.
- Four-layer event-sourcing model: CO architectural framing from 2026-05-01, captured in
  memory file `project_adr_portfolio_foundation_pattern.md`.
*(Anti-pattern #21 part 2: checked.)*
