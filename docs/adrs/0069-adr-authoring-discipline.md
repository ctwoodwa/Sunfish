---
id: 69
title: ADR Authoring Discipline (pre-merge council + §A0 + three-direction)
status: Proposed
date: 2026-05-01
tier: process
pipeline_variant: sunfish-quality-control

concern:
  - governance
  - dev-experience

enables: []

composes: []

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

amendments: []
---
# ADR 0069 — ADR Authoring Discipline (pre-merge council + §A0 + three-direction)

**Status:** Proposed
**Date:** 2026-05-01
**Author:** XO (research session)
**Pipeline variant:** `sunfish-quality-control` (formalizes existing informal discipline; no new substrate)
**Tier:** `process`

---

## Context

Sunfish's ICM pipeline specifies what moves through stages; until now it has said nothing canonical about the quality disciplines an ADR author must apply during drafting and pre-merge review. The gap has been papered over by informal CLAUDE.md notes, session memories, and a growing cohort of post-acceptance amendments that fix structural-citation failures the author's own review missed.

Three disciplines have emerged organically from that cohort and stabilised across ADRs 0062–0065. They are now reliable enough to formalize so they become part of every future substrate ADR rather than being re-derived each session:

**D1 — Pre-merge council canonical for substrate-tier ADRs.**
Stage 1.5 adversarial review (four perspectives + 21-AP scan) runs BEFORE a substrate-tier ADR merges, not as a post-acceptance triage. The primary driver is a cohort batting average of **20-of-20 substrate amendments needing council-sourced fixes**. Running council post-acceptance converts those fixes into ADR amendments (numbered noise, audit-trail churn). Running council pre-merge absorbs them as inline corrections before the ADR is marked Accepted.

**D2 — §A0 self-audit limitation block.**
Every substrate-tier ADR begins with a §A0 section listing every cited symbol, ADR, and package path classified as Existing / Introduced / Removed. §A0 forces enumeration at draft time. It does NOT replace council; the §A0 catch rate across the 2026-04/05 cohort was **0-of-5 structural-citation failures caught** (failures were caught by pre-merge council, not §A0). §A0 is necessary-but-not-sufficient: the act of enumeration is itself a sanity-check, but XO's mental model is precisely what the enumeration mirrors — so the same blind spots propagate.

**D3 — Three-direction structural-citation spot-check.**
Structural-citation failures come in three directions:
1. **Negative-existence** — claiming something doesn't exist when it does (e.g., ADR 0028-A1 council falsely claimed ADR 0061 was vapourware; A3 retracted).
2. **Positive-existence** — claiming something exists when it doesn't (e.g., ADR 0028-A2 cited `Sunfish.Foundation.Canonicalization.JsonCanonical` as "verified existing" when the implementing PR's checklist was unchecked; A4 retracted).
3. **Structural-citation correctness** — a symbol exists but the citation places it at the wrong type, namespace, or layer (e.g., ADR 0028-A6 cited `required: true` on `ModuleManifest` per ADR 0007; the field exists in ADR 0007 but on `ProviderRequirement`, not `ModuleManifest`; council caught this pre-merge in A6, fixing it in A7).

The three-direction discipline applies symmetrically: XO spot-checks its own drafts AND spot-checks council's own claims when council makes negative/positive/structural assertions (council false-claim rate: 2-of-12 council findings in the 2026-04/05 cohort).

The Stage 5 quarterly snapshot (PR #487) flagged the absence of a canonical process-tier ADR covering these disciplines as a gap. This ADR closes it.

---

## Decision drivers

- **Cohort batting average: 20-of-20 substrate amendments needed council-sourced fixes.** (Updated per ADR 0065 context — was 18-of-18 at ADR 0065 draft time.) Zero counter-examples exist in the 2026-04/05 cohort. Statistical support for "pre-merge council canonical" is as strong as the sample size permits.
- **§A0 catch rate: 0-of-5.** ADR 0063's council found 4 structural-citation failures that all passed the §A0 self-audit (F1 `MinimumSpecDimension`, F2 `SyncStateSpec.AcceptableStates` state-names, F3 `NetworkSpec.RequiredTransports` enum values, F5 `packages/foundation-bundles/` vs. `packages/foundation-catalog/`). ADR 0028-A9's council found a 5th (parent-propagated citation of `schemaEpoch` to ADR 0001 instead of paper §7.1). §A0's failure mode: it mirrors XO's draft-time mental model; where the model is wrong, the enumeration is wrong.
- **Post-acceptance amendment cost.** A structural-citation failure that lands in an Accepted ADR produces: (1) a numbered amendment commit, (2) an ADR audit-trail wart, (3) downstream ADRs that may have inherited the bad citation via verbatim copy (the "parent-propagation" failure mode from ADR 0028-A6 → A9). Pre-merge cost: an inline correction. Post-merge cost: amendment + possible cascade.
- **Formalizing informally-stable practice.** Decision Discipline Rule 3 (auto-accept mechanical-fix council amendments) and the §A0 pattern (introduced in ADR 0062-A1.14) have been applied consistently across 0062/0063/0064/0065. Formalizing in an ADR makes the discipline durable across sessions and communicates expectations to future contributors.
- **Process-tier ADRs are subject to the same disciplines they codify.** An ADR whose own claims are unverified would undermine the discipline it mandates. This ADR includes a §A0 and undergoes the pre-acceptance audit from the template.

---

## Considered options

### Option A — Informal conventions (status quo) [REJECTED]

Continue relying on CLAUDE.md notes + session memories to propagate the disciplines. No canonical ADR; no formal checklist enforcement.

- **Pro:** Zero new overhead — authors who already know the conventions are unaffected.
- **Pro:** No friction for process-tier ADRs where structural-citation risk is low.
- **Con:** Conventions decay across sessions. Memory files are point-in-time observations; new sessions start cold.
- **Con:** "Pre-merge council canonical" is only enforced when the author remembers it. The 20-of-20 batting average exists precisely because the current cohort ran council; there is no guarantee a future session will.
- **Con:** New contributors (human or AI) have no single document explaining the expected authoring posture. The disciplines are scattered across memories, CLAUDE.md amendments, and cohort-specific ADR preamble notes.

**Verdict:** Rejected. The gap is real and the cost of a formal ADR is low.

### Option B — CLAUDE.md checklist (extend CLAUDE.md, no ADR) [REJECTED]

Add a "Substrate ADR authoring checklist" section to CLAUDE.md covering all three disciplines.

- **Pro:** Lower ceremony than an ADR; immediately visible to new sessions.
- **Pro:** CLAUDE.md is session-loaded by default; no lookup required.
- **Con:** CLAUDE.md is project-level instruction, not the ADR portfolio's own governance. Governance decisions about the ADR system belong in the ADR system.
- **Con:** CLAUDE.md changes require a PR with a human-instructions tag; discipline updates then require both a CLAUDE.md edit and an ADR amendment — two sources of truth.
- **Con:** No formal change-management. Scope creep, version drift, and conflicting amendments are invisible to the ICM pipeline.

**Verdict:** Rejected in favor of a canonical process-tier ADR, with a short pointer from CLAUDE.md to this ADR.

### Option C — Canonical process-tier ADR (this option) [RECOMMENDED]

Author ADR 0069 (`tier: process`) formalizing D1 + D2 + D3 as the required authoring discipline for substrate-tier ADRs. The ADR template (`docs/adrs/_template.md`) is updated to cite ADR 0069 for §A0 guidance. CLAUDE.md receives a one-line pointer.

- **Pro:** Governance decision lives in the governance system. Future amendments are versioned, reviewable, and ICM-tracked.
- **Pro:** Single source of truth. Sessions look up ADR 0069; CLAUDE.md, the template, and individual ADRs all point here.
- **Pro:** The ADR itself demonstrates the disciplines it codifies (§A0 below; pre-acceptance audit in template section).
- **Con:** Adds 1 ADR to the index. Low cost.
- **Con:** Process-tier ADRs require the same authoring rigor as substrate-tier ADRs, increasing XO effort slightly.

**Verdict:** Recommended.

---

## Decision

**Adopt Option C.** The three disciplines — pre-merge council canonical (D1), §A0 self-audit block (D2), and three-direction structural-citation spot-check (D3) — are required authoring discipline for all new **substrate-tier** ADRs.

Process-tier and governance-tier ADRs SHOULD follow D2 and D3 where citations are present; D1 (pre-merge council) is RECOMMENDED but not required for non-substrate ADRs.

### Discipline 1 — Pre-merge council canonical for substrate-tier ADRs

**Rule:** Every substrate-tier ADR (`tier` ∈ `{foundation, kernel, ui-core, adapter, block, accelerator}`) MUST run a Stage 1.5 adversarial council review *before* the ADR is merged and status is flipped to `Accepted`.

**Council brief shape:** standard four perspectives (Outside Observer, Pessimistic Risk Assessor, Pedantic Lawyer, Skeptical Implementer) plus the 21-AP scan. Dispatch at `high` effort, `Opus 4.7`. Explicitly enumerate structural-citation pressure-test points in the brief (the ADR 0063 council brief did this; the council then caught all 4 structural failures).

**Amendment classification:**

| Amendment type | Action |
|---|---|
| Mechanical fix (rename, correct type signature, add missing cross-reference, fix citation) | Author inline + auto-merge. Rule 3 of Decision Discipline. |
| Scope reframe ("this ADR should also cover X") | Escalate to CO. |
| Architecture reframe (use Option B instead of A) | Escalate to CO. |
| Business-impact change (PCI scope, payment commitment, customer-facing deletion) | Escalate to CO. |

The council's own claims are not trusted blindly. When a council finding asserts:
- A negative-existence claim ("X does not exist") → spot-check before applying (see D3).
- A positive-existence verification ("X is verified per ADR Y") → spot-check the cited ADR's implementation checklist.
- A structural assertion ("field F lives on type T per ADR Y") → read the cited ADR's schema definition.

Council false-claim rate in the 2026-04/05 cohort: **2-of-12** council findings were wrong. Always cheaper to spot-check than to retract.

**PR discipline for substrate-tier ADRs:**

- Disable auto-merge.
- PR description must state: "Awaiting pre-merge council per ADR 0069."
- After council review and all mechanical amendments applied: CO or XO explicitly enables merge (or re-enables auto-merge).

### Discipline 2 — §A0 self-audit limitation block

**Rule:** Every substrate-tier ADR MUST include a §A0 section immediately after the metadata lines and before the `## Context` section. The section enumerates every cited symbol, ADR, and package path, classified as:

- **Existing on `origin/main` (verified `<date>`):** — the cited artifact has been verified to exist at the cited name and location.
- **Introduced by this ADR:** — the type, package, or path does not yet exist; this ADR's implementation checklist creates it.
- **Removed by this ADR:** — the type, package, or path is deprecated by this ADR.

**Section header:** Use `## A0 cited-symbol audit` (matching the existing convention from ADR 0063/0064/0065).

**Limitation acknowledgment:** §A0 MUST end with a standard limitation note:

> *§A0 is necessary but not sufficient. Structural-citation failures in the 2026-04/05 cohort had a 0-of-5 §A0 catch rate; all were caught by pre-merge council. §A0 forces enumeration; it does not substitute for pre-merge council review.*

**When §A0 catches nothing vs. flags something:** Both outcomes are valid. An empty §A0 (all citations Existing; none Introduced or Removed) is a useful signal. A §A0 with `Introduced` entries is required to list those entries in the Implementation checklist.

### Discipline 3 — Three-direction structural-citation spot-check

**Rule:** Before declaring AP-21 ("assumed facts without sources") clean in the pre-acceptance audit, run the following checks depending on claim type:

#### Direction 1 — Negative-existence ("X does not exist")

Before removing a citation or applying a council finding that says X doesn't exist:

```bash
# Check ADR existence
git ls-tree origin/main docs/adrs/ | grep <number>

# Check symbol existence
git grep -n "interface <Name>|class <Name>|record <Name>|enum <Name>" packages/

# Check package path existence
git ls-tree origin/main packages/<name>/
```

#### Direction 2 — Positive-existence ("X exists per ADR Y")

Before asserting a cited symbol is verified:

```bash
# Verify the ADR's implementation checklist is CHECKED (not just that the ADR was authored)
git show origin/main:docs/adrs/<number>-<slug>.md | grep -A 20 "Implementation checklist" | head -25

# Verify the symbol exists in packages
git grep -n "public.*<TypeName>|class <TypeName>|record <TypeName>|interface <TypeName>" packages/

# Verify the package directory exists if a new package was promised
git ls-tree origin/main packages/<promised-package>/
```

#### Direction 3 — Structural-citation correctness ("field F exists on type T per ADR Y")

Before asserting a field-on-type citation is correct:

```bash
# Read the ADR's actual schema — don't trust grep alone
git show origin/main:docs/adrs/<number>-<slug>.md | grep -B 2 -A 10 "<TypeName>"

# If multiple types in the ADR define similar fields, list ALL types defining the field
git show origin/main:docs/adrs/<number>-<slug>.md | grep -B 1 "<fieldName>:"

# Cross-check against code if the type already shipped
git grep -n "class <TypeName>|record <TypeName>" packages/
```

**Parent-propagation corollary:** When a derivative amendment cites the parent amendment's text verbatim, the §A0 self-audit MUST re-verify the parent's own citation chain — not just confirm the parent ADR is Accepted. Verbatim copy propagates the parent's structural failures silently.

**Asymmetric cost of each direction:** Negative-existence failures tend to produce active removals (XO removes a correct citation based on false council signal); they are harder to recover from than positive-existence failures, which typically surface as build failures. Structural-citation failures slip through both §A0 and positive-existence checks because `git grep` finds the field name — just on the wrong type. Reading the ADR's schema definition directly is the only reliable defense.

---

## Scope boundary

These disciplines apply most strongly to **substrate-tier** ADRs — those introducing new types, contracts, or packages across multiple packages. The justification is empirical: 20-of-20 failing council-without-pre-merge is a substrate-tier phenomenon; the cited symbol surface is what creates the risk.

**Process-tier ADRs** (like this one) SHOULD run §A0 and three-direction spot-check where citations are present. Pre-merge council is RECOMMENDED but not required — process-tier changes rarely introduce structural-citation risk unless they cite specific code symbols.

**Governance-tier and tooling-tier ADRs** follow the same guidance as process-tier.

**Amendment ADRs** (e.g., ADR 0028-A7): the parent's §A0 serves as baseline; an amendment §A0 documents its own delta. The parent-propagation corollary of D3 applies.

---

## Consequences

### Positive

- Structural-citation failures — which have appeared in 71% of substrate amendments in the 2026-04/05 cohort (~10-of-14) — are caught pre-merge rather than post-acceptance.
- Post-acceptance amendment volume drops. The 20-of-20 batting average implies near-total absorption if D1 is applied consistently.
- New contributors (human or AI) have a single ADR to read that explains expected authoring posture; they no longer have to reverse-engineer the cohort's accumulated session memories.
- §A0's enumeration habit remains valuable even with 0% standalone catch rate, because it forces the author to name what they're citing — a prerequisite for the spot-check.

### Negative

- Pre-merge council adds 10–30 minutes to the ADR authoring cycle per substrate ADR (council dispatch + amendment application). The cohort data shows this is always cheaper than post-acceptance amendment cycles, which average 15–45 minutes each and produce audit-trail noise.
- Authors (especially AI sessions after `/compact`) may not load this ADR's conventions if CLAUDE.md does not point to it explicitly. The Implementation checklist below includes a CLAUDE.md pointer task.

### Trust impact

Pre-merge council is adversarial by design; it adds a mandatory adversarial perspective before substrate ADRs land. This is consistent with the project's security posture (ADRs that introduce new trust boundaries, key surfaces, or capability scopes are exactly the ones where pre-merge council finds the most critical findings).

---

## Compatibility plan

This is a process-tier ADR. It changes the authoring workflow, not any code or type surface. There is no package migration.

**Backward compatibility:** ADRs 0001–0065 are retroactively in scope for the spirit of D1/D2/D3, but there is no remediation obligation. The disciplines apply going forward; existing ADRs are amended only if a structural-citation failure surfaces through normal workstream progression.

**Template update:** `docs/adrs/_template.md`'s Pre-acceptance audit section already covers cited-symbol verification (added 2026-04-29). The template's §A0 example and D1 council-posture note should be added per the Implementation checklist below. No breaking change to existing ADRs.

---

## Implementation checklist

- [ ] Add a one-line pointer to ADR 0069 in `CLAUDE.md`'s Key files table: `ADR 0069` → "ADR Authoring Discipline (pre-merge council + §A0 + three-direction)".
- [ ] Update `docs/adrs/_template.md`: add §A0 template block immediately after metadata lines (before `## Context`); add D1 pre-merge council note in Pre-acceptance audit section; cite ADR 0069.
- [ ] For each new substrate-tier ADR going forward: include §A0, run D3 spot-checks at draft time, disable auto-merge, dispatch pre-merge council before flipping status to Accepted.
- [ ] Update `icm/_state/active-workstreams.md` or the relevant workstream hand-off if any in-flight substrate ADR has auto-merge currently enabled: disable it; add council-pending note.
- [ ] When dispatching pre-merge council for a substrate ADR, enumerate structural-citation pressure-test points explicitly in the council brief (cite which types, fields, and cross-ADR claims need structural verification — this doubled council's catch rate in the ADR 0063 experience).

---

## Open questions

1. **§A0 format standardization.** The existing ADRs (0062–0065) use slightly different §A0 header styles. Should the template enforce a strict header string (`## A0 cited-symbol audit`)? Low priority; the tooling does not currently parse §A0.
2. **Automated §A0 enforcement.** `tools/adr-projections/project.py` validates frontmatter; it does not parse body sections. A future extension could warn when a substrate-tier ADR is missing §A0. Left to a future `sunfish-quality-control` workstream.
3. **Council dispatch automation.** Currently council dispatch is a manual step. A future `sunfish-quality-control` workstream could add a CI check: substrate ADR PRs with auto-merge enabled trigger a warning if no council file is present in `icm/07_review/`. Out of scope for this ADR.

---

## Revisit triggers

- **Council batting average drops below 70%.** If 3 consecutive substrate ADRs pass pre-merge council without amendments, reconsider whether the council step is proportionate for lower-risk substrate ADRs.
- **§A0 catch rate improves.** If the tooling gains body-parsing capability and §A0 can be automatically cross-checked against `git grep`, the "necessary-but-not-sufficient" framing may need updating.
- **ADR volume exceeds 200.** At scale, per-ADR council dispatch may be cost-prohibitive. Revisit D1 scope (e.g., council only for ADRs introducing new trust boundaries or public API surfaces).
- **Process-tier ADRs begin acquiring structural citations.** If process-tier ADRs start citing specific code symbols frequently, D1 should become required for process-tier as well.

---

## References

### Predecessor and sister ADRs

- [ADR 0062](./0062-mission-space-negotiation-protocol.md) — first ADR to codify pre-merge council posture explicitly; §A0 pattern introduced in A1.14
- [ADR 0063](./0063-mission-space-requirements.md) — first ADR to include §A0 from inception; council found 4 structural failures §A0 missed (F1/F2/F3/F5); the empirical basis for "§A0 is necessary-but-not-sufficient"
- [ADR 0065](./0065-wayfinder-system-and-standing-order-contract.md) — cites 18-of-18 batting average (pre-0069); confirms the pattern is stable

### Process documentation

- [`CLAUDE.md`](../../CLAUDE.md) — Pre-build checklist (sunfish-PM); Multi-session coordination
- [`.claude/rules/universal-planning.md`](../../.claude/rules/universal-planning.md) — Stage 1.5 Autonomous Hardening (six adversarial perspectives); 21 anti-patterns
- [`.claude/rules/effort-policy.md`](../../.claude/rules/effort-policy.md) — Subagent effort policy; council subagent dispatch at `high`/`xhigh`

### Memory files (point-in-time; verify against current repo)

- `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/feedback_council_can_miss_spot_check_negative_existence.md` — three-direction failure modes; council false-claim rate; §A0 catch-rate history; parent-propagation corollary (2026-05-01 update)
- `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/feedback_decision_discipline.md` — Rule 3 (mechanical-fix auto-accept); Rule 6 (cited-symbol verification procedure)

### Tooling

- [`tools/adr-projections/project.py`](../../tools/adr-projections/project.py) — frontmatter validator; relevant for D2 future extension (§A0 auto-detection)
- [`docs/adrs/_template.md`](./template.md) — pre-acceptance audit checklist; cited-symbol verification helper script

---

## A0 cited-symbol audit

Per D2 of this ADR (recursive; meta-§A0):

**Existing on `origin/main` (verified 2026-05-01):**

- `docs/adrs/0062-mission-space-negotiation-protocol.md` — ADR 0062 cited in References and Context (verified via `git ls-tree`)
- `docs/adrs/0063-mission-space-requirements.md` — ADR 0063 cited; source of §A0 catch-rate empirical data (verified)
- `docs/adrs/0065-wayfinder-system-and-standing-order-contract.md` — ADR 0065 cited; 18-of-18 batting-average source (verified)
- `docs/adrs/_template.md` — cited in Implementation checklist; exists (verified)
- `docs/adrs/README.md` — not cited in body; not needed
- `.claude/rules/universal-planning.md` — cited in References (verified)
- `.claude/rules/effort-policy.md` — cited in References (verified)
- `tools/adr-projections/project.py` — cited in Tooling reference (verified)
- `CLAUDE.md` — cited in Implementation checklist + References (verified)

**Introduced by ADR 0069:** None. This ADR introduces no new code types or packages.

**Removed by ADR 0069:** None.

*§A0 is necessary but not sufficient. Structural-citation failures in the 2026-04/05 cohort had a 0-of-5 §A0 catch rate; all were caught by pre-merge council. §A0 forces enumeration; it does not substitute for pre-merge council review.*

---

## Pre-acceptance audit

Per [`docs/adrs/_template.md`](./template.md):

- [x] **AHA pass.** Three options considered (status quo / CLAUDE.md checklist / canonical ADR). Options A and B rejected with documented rationale.
- [x] **FAILED conditions / kill triggers.** Council batting average dropping below 70% named as revisit trigger. §A0 catch rate improving named as revisit trigger.
- [x] **Rollback strategy.** Reverting this ADR to `Withdrawn` and re-editing CLAUDE.md is sufficient — no code surface to unwind.
- [x] **Confidence level.** HIGH. 20-of-20 cohort batting average is unambiguous. §A0 0-of-5 catch rate is unambiguous. Both are empirically grounded in this repo's documented history, not assumption.
- [x] **Cited-symbol verification.** This is a process-tier ADR; it cites ADR numbers and file paths rather than `Sunfish.*` code symbols. All cited ADR numbers and file paths verified via `git ls-tree origin/main` above in §A0. No code symbols to verify.
- [x] **Anti-pattern scan.** AP-1 (unvalidated assumptions): empirical cohort data cited. AP-3 (vague phases): no implementation phases — one-time checklist. AP-9 (skipping Stage 0): §A0 above + three options considered. AP-11 (zombie project): revisit triggers named. AP-12 (timeline fantasy): no timeline dependencies. AP-21 (assumed facts without sources): cited-symbol verification clean; cohort statistics sourced from memory files (cited in References).
- [x] **Revisit triggers.** Named: batting-average drop, §A0 catch-rate improvement, ADR volume > 200, process-tier ADRs acquiring structural citations.
- [x] **Cold Start Test.** A fresh contributor reading this ADR can: identify which ADR tier requires D1/D2/D3; execute §A0 enumeration; run three-direction spot-checks using the shell snippets; disable auto-merge on a PR; write a council brief with structural pressure-test points. The checklist in Implementation is sufficient.
- [x] **Sources cited.** Cohort batting average sourced to ADR 0065 context + memory files. §A0 catch rate sourced to ADR 0063 council findings + memory file. Council false-claim rate sourced to memory file. All sources named.
