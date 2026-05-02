---
adr: "0073"
title: "Stage-06 Hand-off Template Contract"
status: "Proposed"
date: "2026-05-01"
authors: ["XO research session"]
tier: process
concerns: ["governance", "dev-experience"]
pipeline-variant: "sunfish-quality-control"
composes: ["0070"]
---

# ADR 0073 — Stage-06 Hand-off Template Contract

**Status:** Proposed
**Date:** 2026-05-01
**Authors:** XO research session
**Tier:** process
**Concerns:** governance, dev-experience
**Composes:** [ADR 0070]
**Pipeline variant:** `sunfish-quality-control`

---

## Context

Sunfish uses a filesystem-based ICM pipeline (nine stages, `00_intake` through `08_release`) for all significant changes. The **Stage-06 hand-off** is the primary coordination artifact between the XO (research/design) session and the COB (sunfish-PM/build) session. It lives at `icm/_state/handoffs/<workstream>.md` and carries everything COB needs to execute a workstream without real-time communication from XO.

As of Q1–Q2 2026, more than thirty hand-off files have been authored. The corpus demonstrates a clear, load-bearing structure: scope summary, per-phase instructions with binary acceptance gates, halt conditions, and cross-references. However, the structure has been informally converging rather than formally specified — each new hand-off author must infer the convention from prior examples rather than from a declared standard.

This lack of formal specification creates four concrete problems:

1. **Section-name drift.** Early hand-offs write `## Acceptance criteria` at the foot of the file; later ones write `## Acceptance criteria (cumulative across all N phases)` or embed per-phase criteria inline. COB must scan the full file to find gates.
2. **Estimate opacity.** Some hand-offs include a cohort-precedent citation for their estimate ("within ADR 0053 amendment A7's 12–19h estimate"); others state hours without justification. A hand-off that silently overpromises creates schedule debt visible only at PR merge.
3. **Halt-condition omission.** Several early hand-offs omit halt conditions entirely. COB, encountering an unexpected blocker, has no documented signal for whether to proceed, stub, or raise a beacon. The absence of a halt-condition section is itself ambiguous.
4. **Post-author mutation.** XO occasionally revises a hand-off after COB has already started work — correcting a type name, adding a phase, widening scope — without any formal amendment process. COB, mid-build, cannot distinguish stale local assumptions from live XO updates.

The fourth problem is the highest-stakes. A silent in-place edit to a hand-off that COB has already consumed can produce divergent implementations: COB builds to the version it read; XO's post-edit version has silently changed the contract. The only safe mitigation is a formal no-silent-update rule enforced by convention and tracked via an addendum file.

This ADR formalizes the hand-off schema, filename convention, required and optional sections, binary phase-gate protocol, estimate-honesty rules, and the no-silent-update rule. It does **not** change existing hand-offs — the corpus is grandfathered at its current state. Future hand-offs (authored after this ADR is accepted) MUST conform.

---

## Decision drivers

1. **COB executes from the hand-off alone.** There is no synchronous communication channel between XO and COB sessions. The hand-off is the complete specification; any ambiguity in the hand-off becomes a halt or a misimplementation.
2. **Binary gates are the contract.** COB must know, at the end of every phase, whether to proceed or stop. Acceptance criteria expressed as prose paragraphs are not binary. The contract requires an unambiguous PASS/FAIL checkpoint.
3. **Estimate honesty protects velocity.** The MASTER-PLAN velocity baseline depends on hours-per-phase estimates. Estimates that are disconnected from cohort precedent silently corrupt the baseline. Citing a precedent makes the estimate auditable and revisable.
4. **Halt conditions prevent silent divergence.** A COB session that encounters an unexpected blocker and has no halt-condition guidance either halts the entire workstream (conservative; correct but slow) or makes an undocumented design judgment (fast; potentially wrong). Explicit halt conditions remove the guessing game.
5. **Addendum files enforce immutability.** The pattern is already in use (`property-public-listings-stage06-addendum.md`, `adr-0046-a2-encrypted-field-stage06-addendum.md`, `property-messaging-substrate-stage06-addendum.md`, among others). Formalizing it removes the question of when an in-place edit is acceptable.
6. **Grandfathering reduces migration cost.** Retrofitting all existing hand-offs would require council-level review of thirty files. The value of retroactive conformance is low; the cost is high. Grandfathering existing files and applying the standard to new ones preserves momentum.

---

## Considered options

### Option A — Informal convention (status quo)

Continue allowing hand-offs to evolve organically. Trust authors to follow prior examples. Document conventions only in CLAUDE.md prose.

- **Pro:** zero overhead per hand-off; authors have full flexibility.
- **Con:** drift is already visible in the corpus (§Context, problem 1). Will worsen as the workstream count grows beyond fifty.
- **Con:** new XO sessions (and especially sessions that `/compact` prior context) cannot reliably reconstruct the convention from CLAUDE.md prose alone. The hand-off format ends up in scattered memory notes rather than a canonical spec.
- **Verdict:** the status quo created the problem this ADR is solving. Rejecting it.

### Option B — Schema-validation tooling (CI-enforced)

Write a Markdown schema validator that runs in CI and fails PRs that don't include required sections.

- **Pro:** mechanical enforcement; no hand-author judgment needed.
- **Con:** requires maintaining a CI tool that is not part of the production codebase. Adds CI surface without production value.
- **Con:** the ICM pipeline is intended to be human-authored and human-reviewed. Mechanical enforcement of Markdown structure is appropriate only after the structure has stabilized through several release cycles. Premature automation locks in a schema before its edge cases are understood.
- **Verdict:** defer to post-v1. Out of scope for this ADR; noted as a follow-on candidate.

### Option C — Canonical schema ADR + named-section contract + addendum protocol **[RECOMMENDED]**

Define the required and optional sections, the binary-gate protocol, the filename convention, the estimate-honesty rules, and the no-silent-update rule in a single ADR. Apply to new hand-offs only. Enforce via code-review checklist rather than CI tooling.

- **Pro:** zero new CI surface; enforcement is social (review gate) + mechanical (ADR exists and is citable).
- **Pro:** authors get a spec to read, not a corpus to infer from.
- **Pro:** the addendum pattern is already proven by the corpus; formalizing it is low-risk.
- **Verdict:** this is the right approach for a process-tier constraint at this stage of the project.

---

## Decision

**Adopt Option C.** The canonical Stage-06 hand-off format is defined below. All hand-offs authored after this ADR is accepted MUST conform. Existing hand-offs are grandfathered.

---

## The canonical hand-off schema

### Filename convention

```
icm/_state/handoffs/<workstream-slug>-stage06-handoff.md
```

Where `<workstream-slug>` is the kebab-case identifier used in `icm/_state/active-workstreams.md` for the workstream row. Examples: `property-leasing-pipeline`, `foundation-mission-space`, `foundation-wayfinder`.

**Addendum files** use the suffix pattern:

```
icm/_state/handoffs/<workstream-slug>-stage06-addendum.md
icm/_state/handoffs/<workstream-slug>-stage06-addendum-2.md   (if a second addendum is needed)
```

Addendum numbering starts at bare (no suffix for the first addendum) and increments (`-2`, `-3`) if further amendments arrive after COB has consumed the previous addendum.

---

### Required sections (all seven MUST be present)

#### §1 — Header block

A metadata block at the top of the file with the following fields:

```markdown
# <Workstream title> — Stage 06 hand-off

**From:** <authoring session role; e.g., XO research session>
**To:** <receiving session role; e.g., sunfish-PM session>
**Workstream:** #<N> (<descriptive name>)
**Spec:** [ADR XXXX](../../docs/adrs/XXXX-slug.md) (<status>; <PR ref if accepted>)
**Pipeline variant:** `<variant>`
**Estimated effort:** <range in hours> <session type> (e.g., "12–19 hours focused sunfish-PM time")
**Estimate basis:** <cohort-precedent citation or explicit "no precedent — first-of-kind estimate">
**Decomposition:** <N> phases shipping as ~<M> PRs
**Prerequisites:** <list with ✓ for already-met, and linked hand-offs or ADRs for pending>
**Status:** `ready-to-build`
```

The `Status:` field MUST be `ready-to-build` when the hand-off is created. XO MUST NOT create the hand-off file at a prior status and update it in place — the file appears at `ready-to-build` or not at all.

#### §2 — Scope summary

One-to-three paragraphs, or a numbered list, describing what this hand-off builds. Must include a **NOT in scope** sub-section listing at least two items explicitly deferred. The not-in-scope list is the primary tool for controlling scope creep: if COB encounters a tempting extension that is not listed, the default is to write a `cob-question-*` beacon, not to add it.

#### §3 — Phases

One sub-section per phase. Each phase section MUST include:

- **What to build:** file-by-file or interface-by-interface description; sufficient for a cold-start COB session to proceed without reading prior session context.
- **Estimated effort:** hours range for this phase, tied back to the §1 total.
- **Gate:** a single binary PASS/FAIL condition written as one or two sentences in the form *"Gate: \<observable condition\>."* The gate must be mechanically verifiable by COB without judgment calls. Examples of compliant gates:
  - *"Gate: `dotnet build` clean; all existing and new unit tests pass."*
  - *"Gate: 12 `AuditEventType` constants present; factory ships; demographics never appear in any `AuditPayload.Body`."*
  - *"Gate: ledger row #{N} status reads `built`."*
- **PR title:** the exact conventional-commit PR title COB will use for this phase.

The gate is not a narrative description of completeness. If the gate cannot be expressed as a binary check, the phase is too coarsely defined; break it further.

#### §4 — Halt conditions

An explicit enumerated list (minimum two items) of conditions under which COB MUST stop work and write a `cob-question-*` beacon rather than proceeding. Each item identifies:

1. The triggering condition (what COB will observe).
2. The action to take (which beacon type; which workstream to name; what question to ask).

The halt-conditions section exists for COB's protection: a COB session that silently makes a design call on a blocked dependency produces a harder-to-fix state than one that halts and surfaces the question. The absence of a halt-condition section is a hand-off quality defect; XO code review MUST flag it.

Required halt conditions for every hand-off (in addition to workstream-specific ones):

- *"If any prerequisite listed in §1 is not yet `built` (check `icm/_state/active-workstreams.md`) when its phase is needed → halt; write `cob-question-*` naming the unmet prerequisite."*
- *"If the active-workstreams.md row for this workstream does not read `ready-to-build` when COB begins → halt; something has changed; write `cob-question-*` naming the discrepancy."*

#### §5 — Acceptance criteria (cumulative)

A checkbox list covering the deliverables of the entire hand-off. Each item is a sentence in the form *"[ ] \<observable outcome\>."* These are the items COB checks before updating the ledger row to `built`. They are the union of all per-phase gates plus any cross-cutting checks (e.g., build clean, all tests pass, apps/docs page exists).

This section is intentionally redundant with the per-phase gates — it provides a final-pass checklist that does not require re-reading all phase sections. The cross-cutting items (build, tests, docs) MUST appear here even if they also appear in phase gates.

#### §6 — Total decomposition table

A Markdown table summarizing all phases:

```markdown
| Phase | Subject | Hours | PR |
|---|---|---|---|
| 1 | <subject> | <min–max> | `<PR title prefix>` |
| ... | | | |
| **Total** | | **<sum-min–sum-max>h** | **<N> PRs** |
```

The "Total" row hours range is the source of truth for the §1 `Estimated effort` field. If the two diverge (e.g., during revision), the table wins.

#### §7 — References

A linked list of all ADRs, prior hand-offs, council reviews, and intake documents cited in the hand-off body. No orphan references in the body text — every `[ADR XXXX]` reference in the file MUST have a corresponding entry in §7 with the relative path verified at authoring time.

---

### Optional sections (use when applicable)

The following sections appear in many hand-offs but are not universally required. Authors MUST include them when the condition applies; they MAY omit them when the condition does not.

| Section | Include when |
|---|---|
| **Open questions** | There are known-unresolved design questions that are explicitly punted by the authoring ADR, or that XO believes COB may encounter. Note each question's resolution path (e.g., "write `cob-question-*`; XO will respond with addendum"). |
| **Cohort patterns to follow** | There are established patterns in the corpus (existing hand-offs, ADR implementation sequences) that this workstream MUST mirror. Cite the pattern by name and link to the canonical example. Omitting this section when patterns exist is a quality defect — COB must not reverse-engineer patterns from grep results when XO knows the answer. |
| **Migration / breaking change** | The workstream involves a breaking-change migration (positional record → init-only, dropped field, MAJOR version bump). Summarize the migration steps and add a `MIGRATION.md` deliverable to the appropriate phase. |
| **Ledger update phase** | Always present for workstreams that flip a `ready-to-build` row to `built`. May be omitted for handoffs that do not correspond to a named workstream row (e.g., a pure ADR-amendment hand-off). |

---

## Binary phase-gate convention

The gate is the only signal COB uses to decide whether a phase is complete. The convention:

1. **A gate is a `dotnet build`-level claim or an artifact-existence claim.** "Build clean" and "X tests pass" are build-level. "File Y exists at path Z" is artifact-existence. Both are binary. *"Feature is substantially implemented"* is not binary and is not a valid gate.
2. **Gates do not reference effort or time.** A phase is complete when its gate is met, not when its estimated hours have been spent. Estimate overrun is a signal to halt + raise a beacon, not to widen scope to fill time.
3. **A gate failure at any phase stops the chain.** COB does not proceed to the next phase until the current gate passes. If a gate cannot be made to pass without a design decision, that is a halt condition — write the beacon, mark the phase incomplete, and stop.
4. **Addenda may add phases or modify gates, but may not silently replace a gate that COB has already passed.** See the no-silent-update rule below.

---

## Estimate-honesty rules

An estimate is honest if it is **grounded** and **calibrated**:

**Grounded:** the estimate cites a cohort precedent in the form *"within \<ADR\> amendment \<X\>'s \<range\>h estimate"* or *"mirrors W#\<N\>'s Phase \<M\> (\<range\>h)"*. If there is no applicable precedent, the §1 header block MUST note *"no precedent — first-of-kind estimate"* and the estimate range MUST be wide (at minimum ±40%).

**Calibrated:** if XO authors a hand-off where the total-decomposition table range is more than 2× a cited precedent, XO MUST either justify the divergence in the scope summary or run the estimate past a council subagent before publishing. Silent ±2× drift is a hand-off quality defect.

Phase-level estimates are informational; the per-phase gate is the completion signal. But COB uses estimates to decide when to escalate: if a phase is consuming double its estimate without a gate pass, that is a halt signal, not a reason to continue.

---

## No-silent-update rule

**This rule is the highest-stakes convention in this ADR.**

Once a hand-off file is committed to `icm/_state/handoffs/` with status `ready-to-build`, XO MUST NOT modify it in place after COB has consumed it (i.e., after COB has begun any phase of work). All post-consumption changes MUST be delivered as an addendum file:

```
icm/_state/handoffs/<workstream-slug>-stage06-addendum.md
```

The addendum MUST include:

- Which phase(s) are affected.
- What changed (old value → new value).
- Whether COB needs to redo any already-passed phase (if yes, the addendum is a breaking amendment — XO MUST write a `cob-question-*` beacon naming this explicitly).

**Why this matters:** COB sessions cannot diff a hand-off file against an earlier version they read (they may have `/compact`ed). A silent in-place update is indistinguishable from the original. COB has no way to know the contract changed. The result is an implementation built on the old contract with XO operating on the new one — the worst form of coordination failure.

The addendum pattern is already proven by the corpus. Examples grandfathered under this ADR:
- `property-public-listings-stage06-addendum.md` (phase-5b, phase-5c4, phase-5c4-sliceC — three addenda for the same workstream)
- `property-messaging-substrate-stage06-addendum.md`
- `property-ios-field-app-stage06-addendum.md`
- `foundation-taxonomy-phase1-stage06-addendum.md`
- `adr-0046-a2-encrypted-field-stage06-addendum.md`

XO may edit a `ready-to-build` hand-off in place only in the window between commit and COB first-read — that is, before any phase has been started. The practical test: has COB opened a PR for any phase of this workstream? If yes, the in-place-edit window is closed.

---

## Canonical examples (grandfathered under this ADR)

The following hand-offs are designated canonical examples of compliant structure, retroactively. Future hand-offs should use them as shape references:

| Hand-off file | Workstream | Exemplifies |
|---|---|---|
| `property-work-orders-stage06-handoff.md` | W#19 (Work Orders) | Binary phase gates; cohort-precedent estimate; comprehensive halt conditions; cumulative acceptance criteria; all seven required sections present |
| `property-leasing-pipeline-stage06-handoff.md` | W#22 (Leasing Pipeline) | Halt conditions that surface legal-review requirements; FHA-defense structural invariant as a gate; taxonomy charter as a distinct phase |
| `foundation-taxonomy-phase1-stage06-handoff.md` | W#31 (Foundation.Taxonomy) | File-by-file scaffold; prerequisite-audit instruction embedded in §Phases; pattern-reference to prior workstreams |

These three hand-offs demonstrate the required structure with differing workstream shapes — a schema migration, a domain-entity build, and a substrate scaffold. Authors should read all three before authoring a new hand-off for the first time.

---

## Trust impact

The hand-off contract is a trust surface between XO and COB. Violations create coordination debt, not just style debt:

- **A missing halt-condition** means COB makes a design call that may be wrong, producing rework rather than a clean pause.
- **A silent in-place update** means COB implements the wrong contract, producing a PR that XO cannot accept without a design-scope conversation that should have happened before implementation.
- **An ungrounded estimate** means the MASTER-PLAN velocity baseline silently drifts, producing schedule surprises at MVP milestone reviews.

These are not hypothetical risks. All three have occurred in the W#18–W#33 cohort. The no-silent-update rule and halt-condition requirement address the highest-frequency failure modes directly.

**COB trusts the hand-off file as the complete contract.** XO must author to that trust level.

---

## Relationship to the ICM pipeline

This ADR governs the artifact produced at the transition from ICM Stage 05 (implementation-plan) to Stage 06 (build). The hand-off is the deliverable that makes the Stage 05 → Stage 06 transition observable — a Stage 05 output exists, can be reviewed, and can be handed to COB without XO participation.

The ICM pipeline's `icm/_state/active-workstreams.md` ledger tracks the workstream row; the hand-off file tracks the build spec. The two are complementary: the ledger says *whether* work is ready; the hand-off says *what* to build. Updating the ledger row to `ready-to-build` without authoring the hand-off is an incomplete Stage 05 exit — COB will correctly halt at the pre-build checklist step.

Composes [ADR 0070] on ICM pipeline governance. Where ADR 0070 specifies the stage boundaries and exit criteria for the nine-stage pipeline, this ADR specifies the structure of the primary Stage 05 → Stage 06 artifact.

---

## Grandfathering clause

Hand-off files committed to `icm/_state/handoffs/` before this ADR is accepted are grandfathered. They are not required to be retrofitted to this schema. The following constraints apply to grandfathered files:

1. **No in-place schema retrofits.** Do not add required sections to grandfathered files to bring them into nominal compliance — this risks silently invalidating COB's prior reads. If a grandfathered hand-off needs amendment, use the addendum file pattern.
2. **Addendum files for grandfathered hand-offs MUST conform to this ADR's addendum convention** (filename pattern, affected-phase declaration, change description, breaking-amendment beacon rule).
3. **New hand-offs for grandfathered workstreams (e.g., a Phase 2 hand-off for a workstream whose Phase 1 was grandfathered) MUST conform to this ADR.** The grandfathering applies to the specific file, not to the workstream.

---

## §A0 — Self-audit

This ADR is process-tier; it defines a convention rather than a code surface. The self-audit checklist is adapted accordingly:

| Check | Result |
|---|---|
| Does this ADR have a clear decision? | Yes — Option C adopted; canonical schema with seven required sections, addendum protocol, binary gate convention, and estimate-honesty rules. |
| Are the options genuinely distinct? | Yes — Option A (status quo) and Option B (CI tooling) are rejected with clear rationale. |
| Are all cross-references verifiable? | Yes — all cited hand-off filenames verified against `icm/_state/handoffs/` directory listing. ADR 0070 is a forward reference (not yet authored); the composes relationship is correctly provisional. |
| Does the grandfathering clause create ambiguity? | No — grandfathering applies per-file, not per-workstream. New files always conform. |
| Does the no-silent-update rule have a clear scope boundary? | Yes — the practical test (has COB opened a PR?) is deterministic. |
| Is the estimate-honesty rule measurable? | Yes — "no precedent — first-of-kind" declaration is required when no precedent exists; ±2× divergence requires justification or council review. |
| Are the required sections necessary and sufficient? | Reviewed against W#19, W#22, W#31 hand-offs. All seven required sections are present in those three. No required section was invented that those files don't already practice. |
| Does this ADR change existing hand-offs? | No. Grandfathering clause is explicit. |

**Self-audit verdict:** Passes. No blocking findings.

---

## References

- [W#19 — Work Orders hand-off](../../icm/_state/handoffs/property-work-orders-stage06-handoff.md) — canonical example; binary phase gates + cohort-precedent estimate
- [W#22 — Leasing Pipeline hand-off](../../icm/_state/handoffs/property-leasing-pipeline-stage06-handoff.md) — canonical example; legal-review halt condition + FHA-defense gate
- [W#31 — Foundation.Taxonomy hand-off](../../icm/_state/handoffs/foundation-taxonomy-phase1-stage06-handoff.md) — canonical example; file-by-file scaffold + prerequisite-audit instruction
- [ADR 0049](./0049-audit-trail-substrate.md) — audit-trail substrate; cited as a cohort-precedent example in estimate-honesty discussion
- [ICM pipeline overview](../../icm/CONTEXT.md) — nine-stage pipeline; Stage 05 implementation-plan → Stage 06 build transition
- [`icm/_state/active-workstreams.md`](../../icm/_state/active-workstreams.md) — workstream ledger; ledger-row/hand-off-file duality
- [CLAUDE.md pre-build checklist](../../CLAUDE.md) — five-step checklist COB runs before any build; step 3 is "read the hand-off"
- [ADR 0070] — ICM pipeline governance (forward reference; composes relationship noted in frontmatter)
