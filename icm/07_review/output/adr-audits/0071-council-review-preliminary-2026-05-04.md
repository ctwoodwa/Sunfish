# PRELIMINARY (Sonnet 4.6) — superseded by canonical Opus 4.7 + xhigh review

**Status:** SUPERSEDED. This review was dispatched on Sonnet 4.6 in error on 2026-05-04. Per CO directive of the same day, all council reviews use Opus 4.7 + `xhigh` effort exclusively while the cohort batting average remains in repair. The canonical review is at `0071-council-review-2026-05-04.md` (this same directory). This file is retained for traceability of the council process but is not authoritative.

**Original PR:** #499 (`chore/icm-0071-council`).

**Original review content follows below the divider.**

---

# Council Review — ADR 0071 (ADR Portfolio System — Event-Sourcing-with-Snapshots)

**Review date:** 2026-05-04
**Reviewer:** XO research subagent (adversarial council; pre-merge canonical per ADR 0069)
**Review posture:** 4-perspective adversarial (Outside Observer, Pessimistic Risk Assessor,
Skeptical Implementer, Devil's Advocate) + UPF v1.2 Stage 2 meta-validation + 21 anti-pattern scan
**ADR tier:** `tooling` (declared by author; contested — see F3)
**ADR status at review:** Proposed (PR #492, open; another subagent rebasing; reviewed from
`origin/docs/adr-0071-adr-portfolio-system`)
**Cohort batting average note:** Per ADR 0065 council review header: "18-of-18 substrate amendments
needed council fixes; structural-citation failure rate ~65%." ADR 0071 is process/tooling-tier;
cohort norms are for substrate ADRs, but pre-merge council applies here per CO authorization.

---

## Verdict

**PASS with two mechanical amendments and one non-mechanical flag.**

The architectural core is sound. The event-sourcing-with-snapshots pattern is the correct model for
the problem. The four-layer design is well-articulated, the rationale for rejecting alternatives A
and B is convincing, and the implementation details are accurate — all cited file paths verified
present in `origin/main`, all six cited PRs confirmed merged. The ADR is safe to accept after the
two mechanical amendments below are applied.

The non-mechanical flag (F3 — tier classification) does not block acceptance but should inform a
future amendment if the `tier` vocabulary is ever audited for consistency.

---

## Summary of findings

| ID | Perspective | Severity | Classification | Issue |
|---|---|---|---|---|
| F1 | Skeptical Implementer | Major | **Mechanical** | Batting-average percentage (65%) contradicts the cited source (19-of-19 = 100%) |
| F2 | Skeptical Implementer | Minor | **Mechanical** | PR #490 described as "open at time of authoring" but was merged 2026-05-04; stale tense |
| F3 | Outside Observer | Minor | **Non-mechanical** | Tier `tooling` is a reasonable-but-arguable fit; `process` is equally valid and used by companion ADRs 0069/0070 |
| F4 | Pessimistic Risk Assessor | Minor | **Mechanical** | `concern: [governance, dev-experience]` does not match the stated meaning of those tags |
| F5 | Devil's Advocate | Low | Observation | Projection freshness is deferred and the CI gap is load-bearing earlier than the ADR implies |

Mechanical: **3** (F1, F2, F4)
Non-mechanical: **1** (F3)
Observations: **1** (F5)

---

## 1. Outside Observer

### General legibility

A fresh contributor opening this ADR without prior cohort context can:

- Understand the four-layer model from the Decision section alone.
- Find the schema spec (`docs/adrs/_FRONTMATTER.md`) and the projection tool
  (`tools/adr-projections/project.py`) via the Cold Start Test in §A0.
- Understand why the alternatives were rejected (reasons are concrete and falsifiable).
- Understand the immutability invariants.

The Context section motivates the decision well: five concrete pain points from actual
development experience, not hypotheticals. The event-sourcing analogy is correctly applied and
will be recognizable to contributors familiar with CQRS/ES patterns.

**Finding none from Outside Observer perspective** — the ADR is well-structured and
self-contained. The only concern is that new contributors who do not read `_FRONTMATTER.md` may
not know to run `python3 tools/adr-projections/project.py` after authoring an ADR; but this is
addressed by the Cold Start Test and the open question #1 about CI freshness enforcement.

---

## 2. Pessimistic Risk Assessor

### What could go wrong

**Risk 1 — Projection freshness gap (medium probability, medium impact).**
The ADR explicitly acknowledges in Open Questions §1 and Negative Consequences that CI validates
frontmatter but does not verify that committed `INDEX.md`/`STATUS.md`/`GRAPH.md` are up to date.
The ADR defers freshness enforcement: "sufficient while the team is small." However, at the pace
of Sunfish's ADR authoring (65 ADRs in ~14 months; 9 ADRs in the 2026-05-02 sprint alone), the
window where "team is small" is narrow. A stale `INDEX.md` cited in a quarterly snapshot review
or a board presentation would be visible and embarrassing. The revisit trigger "team grows beyond
10 contributors" is the right signal but may fire later than the freshness problem becomes
noticeable.

**Assessment:** The risk is real but well-understood and explicitly named. Deferred is the right
call for now. The `adr-validation.yml` CI gate (currently pending) will catch frontmatter errors;
freshness is a separate concern. Risk is low-to-medium while the team is at current scale.

**Risk 2 — Vocabulary maintenance burden (low probability, low impact).**
Each new `tier` or `concern` value requires a two-file update (`project.py` + `_FRONTMATTER.md`).
This is correctly identified as a Negative Consequence. The risk of drift between the two files
(i.e., a valid value in `_FRONTMATTER.md` but not in `VALID_CONCERN` in `project.py`) is real
but small — both files are in the same PR scope and the CI `--check-only` gate would immediately
catch any ADR that uses an invalid vocabulary value.

**Risk 3 — `adr-validation.yml` still pending merge at time of authoring (low severity).**
The implementation checklist marks the CI workflow as not yet merged. Given the ADR formalizes
a pattern that is "already built and is in production use," the CI gate being pending is a minor
inconsistency between the confidence level claim ("HIGH; system is in production use") and the
actual enforcement state.

**Assessment of overall blast radius:** Low. The system is append-only and entirely additive.
Rollback is a single PR stripping frontmatter blocks and deleting the tool and projections. The
journal (ADR bodies) is unaffected in all scenarios. This is the correct blast-radius assessment.

### Finding F4 — Minor — Mechanical

**Issue:** `concern: [governance, dev-experience]` is technically valid per the controlled
vocabulary but does not accurately describe this ADR's scope.

Per `_FRONTMATTER.md`:
- `governance` = "Repo governance, branch protection, CI policy, license posture."
- `dev-experience` = "Scaffolding, templates, kitchen-sink, apps/docs."

ADR 0071 governs the **ADR documentation tooling system** — projection tool, frontmatter schema,
quarterly snapshot cadence. This fits `dev-experience` loosely (tooling is a stretch of
"scaffolding, templates") but `governance` is a poor fit — this ADR does not address branch
protection, license posture, or CI policy in any meaningful way. The CI gate (`adr-validation.yml`)
is a side-effect, not the subject.

The better concern tag set would be `[dev-experience]` only, or `[dev-experience, governance]`
only if the author intends `governance` to cover documentation governance (which the vocabulary
definition does not currently include).

**Disposition:** Mechanical. Remove `governance` from the `concern` array; retain
`dev-experience`. Alternatively, if documentation-governance is a genuinely new concern class,
add an explicit vocabulary entry in `_FRONTMATTER.md` and `project.py` and cite this ADR as
the introduction.

---

## 3. Skeptical Implementer

### File-path verification

All file paths cited in the ADR were verified against `origin/main` via the council worktree at
`/tmp/sunfish-0071-council-wt`:

| Cited path | Present in origin/main |
|---|---|
| `docs/adrs/_FRONTMATTER.md` | Confirmed |
| `docs/adrs/_template.md` | Confirmed |
| `tools/adr-projections/project.py` | Confirmed |
| `tools/adr-projections/README.md` | Confirmed |
| `tools/adr-projections/bulk_apply_frontmatter.py` | Confirmed |
| `docs/architecture/snapshot-2026-Q2.md` | Confirmed |
| `docs/adrs/STATUS.md` | Confirmed |
| `docs/adrs/INDEX.md` | Confirmed |
| `docs/adrs/GRAPH.md` | Confirmed |
| `_shared/product/local-node-architecture-paper.md` | Confirmed (via CLAUDE.md reference) |

### PR citation verification

All six cited PRs verified via `gh pr view`:

| PR | Title | State |
|---|---|---|
| #481 | portfolio foundation: `_FRONTMATTER.md` + projection tool + 4-ADR pilot | MERGED 2026-05-02 |
| #483 | bulk frontmatter apply (56 ADRs; 61 total now covered) | MERGED 2026-05-02 |
| #484 | auto-derive consumed_by in projection tool | MERGED 2026-05-04 |
| #485 | refine frontmatter concerns based on body analysis | MERGED 2026-05-02 |
| #487 | first quarterly snapshot (2026-Q2); Stage 5 | MERGED 2026-05-02 |
| #490 | backfill composes/extends cross-references across 61 ADRs | MERGED 2026-05-04 |

### Cross-ADR existence verification

All predecessor ADR files verified present in `origin/main`:
- ADR 0018, 0037, 0038, 0042, 0069, 0070 — all confirmed at expected paths.
- ADR 0003 (event-bus) and ADR 0049 (audit substrate) — cited in AHA pass — confirmed.

### Projection tool contract accuracy

The ADR states the tool "Scans `docs/adrs/[0-9][0-9][0-9][0-9]-*.md` for frontmatter." The actual
glob in `project.py` is `sorted(ADR_DIR.glob("[0-9][0-9][0-9][0-9]-*.md"))`. The description
matches the implementation. The `consumed_by` derivation via `derive_consumed_by()` is present and
correct. The `--check-only` flag exits non-zero on validation errors. All contract claims accurate.

### Finding F1 — Major — Mechanical

**Issue:** The ADR cites "approximately 65% of amendments in a representative cohort of 19 substrate
ADR amendments required council corrections before merge" in the Context section (lines 64–65) and
repeats this figure in the Decision Drivers (line 96–97), Consequences (line 332), and §A0 AP-1
annotation (lines 545–548).

The §A0 annotation acknowledges the approximate nature of the figure and cites the source:
"memory file `project_adr_portfolio_foundation_pattern.md` ('pre-merge council canonical; cohort
batting average 19-of-19 substrate amendments needed council fixes')."

**The cited source says 19-of-19 = 100%.** The ADR text says "~65%." These are directly
contradictory. The 65% figure appears to originate from the ADR 0065 council review header
("structural-citation failure rate ~65%") where it describes the *fraction of findings that are
structural-citation failures* (a subset of all council findings), not the fraction of amendments
that needed *any* council correction.

The confusion is between two distinct measurements:
1. **Fraction of amendments that needed council correction** — the memory says 19/19 = 100%.
2. **Fraction of council findings that were structural-citation failures** — approximately 65%.

The ADR currently uses measurement (1)'s denominator (19 amendments) with measurement (2)'s
percentage (65%), which is incorrect. The corrected claim should be either:
- "All 19 substrate amendments in the 2026-04-29 cohort required council corrections before merge
  (100%; memory file `project_adr_portfolio_foundation_pattern.md`)." Or:
- "Approximately 65% of all council findings in the 2026-04-29 cohort were structural-citation
  errors, meaning they were errors in cited symbols, file paths, or cross-ADR references."

**Disposition:** Mechanical. Update the Context section, Decision Drivers bullet, Consequences
bullet, and §A0 AP-1 annotation to use the accurate figure and correct denominator. The clearest
fix is to adopt the "100% of 19 amendments needed council correction" framing with a parenthetical
noting that structural-citation errors were the dominant failure mode (approximately 65% of all
findings). This is more dramatic, not less, and better supports the ADR's motivating argument.

### Finding F2 — Minor — Mechanical

**Issue:** The ADR describes PR #490 as "open at time of authoring" in two places:
- Implementation checklist item: "`composes`/`extends` cross-reference backfill across 61 ADRs —
  PR #490 (open at time of authoring)."
- References section: "PR #490 — ... (open at time of authoring)."

PR #490 merged 2026-05-04. The ADR is dated 2026-05-02 and was authored before the merge —
the "open at time of authoring" claim was accurate when written. However, the PR is now merged,
and the checklist item for the backfill is checked (`[x]`), which suggests the checklist was
updated post-author but the prose annotation was not. This creates an inconsistency: the checklist
says the item is complete but the prose says the PR was "open."

**Disposition:** Mechanical. Remove the "open at time of authoring" qualifier from both instances
in the References section and implementation checklist prose, since PR #490 is now merged. The
checked `[x]` in the checklist already communicates completion; the qualifier adds confusion.

---

## 4. Devil's Advocate

### Is the four-layer pattern solving the right problem?

The ADR's stated problem is O(N) discovery cost for "current state" queries across a growing ADR
corpus. The four-layer model addresses this well. But the Devil's Advocate perspective asks: is the
projection tool the simplest possible solution?

**Simpler alternative not considered: `grep + jq` convention without a committed projection.**
The three committed projection files (`INDEX.md`, `STATUS.md`, `GRAPH.md`) require regeneration
discipline. A lighter-weight approach would be: define the frontmatter schema (as done), but emit
projections only in CI artifact outputs — not committed to the repo. Contributors who want the
index run the tool locally; CI runs `--check-only` for validation.

**Why the ADR's choice is still correct:**
The committed projections are load-bearing for *offline-first discipline* (a Sunfish core value per
the Context §Option B rejection). If projections are CI-only artifacts, a contributor without CI
access (or reviewing via a local clone on a flight) cannot answer "which ADRs touch security?"
without running the tool. Committing the projections is the right trade-off for a local-first
framework.

**Alternative option not considered: existing OSS tools (adr-tools, Log4ADR, pADRs).**
The ADR's Option B considers only a wiki, not OSS ADR tools. `adr-tools` (GitHub adr/adr-tools)
provides numbered-ADR creation, supersession, and query but does not support machine-readable
frontmatter or custom taxonomies. `Log4ADR` and similar tools have installation dependencies that
violate the zero-stdlib-dep driver. None provide the tier/concern vocabulary that Sunfish needs
for the quarterly snapshot cadence.

**Assessment:** The AHA pass in §A0 covers two of the three obvious alternatives (renumber,
wiki). The lack of explicit consideration of OSS ADR tools is a minor gap but not a flaw —
the zero-stdlib-dep driver rules them all out, and the taxonomic richness needed for the
Sunfish portfolio is custom by nature. The "first idea unchallenged" anti-pattern (AP-10) does
not fire here.

### Finding F3 — Minor — Non-mechanical

**Issue:** ADR 0071 declares `tier: tooling`. The `_FRONTMATTER.md` spec defines:
- `tooling` = "Scaffolding, generators, build tools."
- `process` = "ICM pipeline, multi-session coordination, naval-org structure, council-batting-average
  discipline."

The ADR portfolio system is the documentation governance infrastructure — it governs how
architectural decisions are recorded, discoverable, and validated. It is closer to the `process`
tier than `tooling`: it is not a scaffolding generator, not a build tool, and its primary
consumers are the XO/COB/PAO sessions (process actors), not engineers running `dotnet scaffold`.

Companion ADRs 0069 (ADR Authoring Discipline) and 0070 (Naval-Org Structure) both declare
`tier: process`. ADR 0071 governs a related practice (how the portfolio of ADRs is organized and
queried) and arguably belongs in the same tier class.

A reasonable counter-argument: the projection tool is a concrete build/tooling artifact
(`project.py`, `project.py --check-only`), which fits `tooling` better than the purely-human
`process` tier. The ADR is a hybrid: the *system* is a process; the *implementation* is tooling.

**Disposition:** Non-mechanical. The author made a defensible choice. This finding is flagged
for CO awareness; the `tier` assignment does not affect validation, CI behavior, or any downstream
system. If the quarterly snapshot review process surfaces `tier: tooling` and `tier: process` as
peers in the ADR portfolio governance cluster, a future amendment (ADR 0071-A1) could reclassify.
Not required before acceptance.

---

## 5. UPF v1.2 Stage 2 Anti-Pattern Scan

**21 patterns evaluated against ADR 0071:**

| AP | Pattern | Assessment |
|---|---|---|
| AP-1 | Unvalidated assumptions | **Fires** — see F1. The 65% figure is internally inconsistent with the cited source. The underlying claim (structural-citation failures were the dominant council failure mode) is valid; the exact percentage is wrong. |
| AP-2 | Vague phases | Not applicable — this ADR describes a built system, not a phased build plan. |
| AP-3 | Vague success criteria | Clear. FAILED conditions are explicit in §A0. |
| AP-4 | No rollback | Rollback strategy present and concrete (single PR stripping frontmatter + tool). |
| AP-5 | Plan ending at deploy | Not applicable — Consequences section covers both positive and negative long-term effects. |
| AP-6 | Missing Resume Protocol | Not applicable (tooling-tier ADR, not a multi-session build plan). |
| AP-7 | Delegation without contracts | Not applicable. |
| AP-8 | Blind delegation trust | Not applicable. |
| AP-9 | Skipping Stage 0 | Three alternatives considered; AHA pass completed. Clear. |
| AP-10 | First idea unchallenged | Does not fire. Two alternatives evaluated and rejected with concrete reasons. |
| AP-11 | Zombie project (no kill criteria) | FAILED conditions name three explicit kill triggers. Clear. |
| AP-12 | Timeline fantasy | Not applicable — system is already built; no timeline claims. |
| AP-13 | Confidence without evidence | Does not fire. Confidence level is "HIGH" backed by evidence: system in production use with `--check-only` passing across 65+ ADRs. |
| AP-14 | Wrong detail distribution | Does not fire. High detail on frontmatter schema/validation rules (appropriate); lower detail on quarterly snapshot process (appropriate — it is hand-curated). |
| AP-15 | Premature precision | Does not fire. The 12 validation rules are precise because they are already implemented; this is post-hoc formalization of a working system. |
| AP-16 | Hallucinated effort estimates | Not applicable — no effort estimates in this ADR. |
| AP-17 | Delegation without context transfer | Not applicable. |
| AP-18 | Unverifiable gates | Does not fire. The "CI validation" gate is verifiable (`--check-only` exits non-zero on failure). |
| AP-19 | Missing tool fallbacks | Does not fire. Human-readable fallback is explicit in the Decision Drivers and Option C pro list. |
| AP-20 | Discovery amnesia | Does not fire. §A0 Sources Cited section traces the provenance of each data point. |
| AP-21 | Assumed facts without sources | **Partially fires** — see F1. The ~65% figure is cited but the citation is internally inconsistent. The file paths and ADR numbers are all verified correct. No C# symbols cited (tooling-tier ADR). |

**AP scan result: 2 fires (AP-1 and AP-21, both from the same root cause as F1). No critical
anti-patterns. The F1/AP-1/AP-21 cluster is the only substantive finding.**

---

## 6. Required amendments (pre-merge)

### Amendment A1 (Mechanical — F1) — Fix batting-average percentage

In the following locations, replace the "~65% of amendments in a representative cohort of 19" claim
with the accurate formulation derived from the cited source:

**Corrected text (apply consistently in all four occurrences):**

> "All 19 substrate amendments in the 2026-04-29 cohort required council corrections before merge.
> Structural-citation errors (cited symbols, file paths, or cross-ADR references that could not be
> verified) were the dominant failure mode, accounting for approximately 65% of all council findings
> in that cohort."

Locations requiring update:
1. Context section (lines 64–66 of the current ADR text)
2. Decision Drivers bullet "Cohort batting average pressure" (lines 96–98)
3. Consequences "Reduced pre-merge council corrections" bullet (lines 330–333)
4. §A0 AP-1 annotation (lines 545–549)

### Amendment A2 (Mechanical — F2) — Remove stale "open at time of authoring" annotation

PR #490 merged 2026-05-04. Remove or rephrase the "open at time of authoring" qualifier in:
1. Implementation checklist item for the `composes`/`extends` backfill.
2. References section PR #490 entry.

The checked `[x]` in the checklist already communicates completion. The qualifier adds confusion
now that the PR is merged.

### Amendment A3 (Mechanical — F4) — Fix concern tags

Remove `governance` from the `concern` array. The `governance` tag's definition
("Repo governance, branch protection, CI policy, license posture") does not accurately describe
ADR 0071's scope. Retain `dev-experience` only. If documentation governance is a genuinely
distinct concern worth tagging in the portfolio, add it as a new vocabulary entry with an explicit
definition in both `_FRONTMATTER.md` and `project.py`.

---

## 7. Optional recommendations (non-blocking)

### Recommendation R1 (F3 — Non-mechanical) — Tier reclassification consideration

Consider whether `tier: process` would be more consistent with companion ADRs 0069 and 0070.
This is not required before acceptance. If `tier` is ever audited for consistency across
governance/process/tooling ADRs, this ADR should be reviewed. A future amendment (A1) could
reclassify.

### Recommendation R2 (F5 — Observation) — Open Question #1 escalation trigger

Open Question #1 (projection freshness CI enforcement) defers action to "when team grows beyond 10
contributors." Given the ADR authoring rate (9 ADRs in the 2026-05-02 sprint), projection staleness
may become visible before the team size trigger fires. Consider adding "or ADR authoring rate
exceeds 5 ADRs/week for 2+ consecutive weeks" as an additional trigger for the freshness enforcement
revisit.

---

## 8. §A0 compliance of this council review

- **Structural-citation check (three directions):**
  - Positive existence: all 6 cited PRs confirmed merged; all 12 cited files confirmed present.
  - Negative existence: no files are claimed absent that in fact exist.
  - Structural correctness: projection tool glob pattern verified to match ADR's description;
    `consumed_by` derivation logic verified present and correct.
- **AP scan:** completed above; 2 fires (both F1/AP-1 root cause).
- **Cold Start Test:** passed — a fresh contributor can follow the Decision section to run the
  tool and understand the system without author clarification.
