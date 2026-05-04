# ADR 0071 Council Review — Canonical (Opus 4.7 + xhigh)

**Review date:** 2026-05-04
**Reviewer:** XO research session, dispatched by CO 2026-05-04
**Model:** Opus 4.7
**Effort:** xhigh
**Pipeline stage:** ICM Stage 07 (review-gate, pre-merge)
**Subject:** ADR 0071 — ADR Portfolio System (Event-Sourcing-with-Snapshots)
**Subject branch:** `docs/adr-0071-adr-portfolio-system` (PR #492, OPEN)
**Worktree:** `/tmp/sunfish-0071-opus-council` (from `origin/main`)
**Verdict:** **NEEDS-AMENDMENT** (one structural-citation failure; remainder mechanical)

---

## Why this review supersedes the preliminary Sonnet review

A preliminary council review of ADR 0071 was dispatched on Sonnet 4.6 earlier on 2026-05-04 and is currently open as PR #499 (`chore/icm-0071-council`, branch `chore/icm-0071-council`). That review was dispatched in error: per the CO directive of 2026-05-04, **all council reviews use Opus 4.7 + `xhigh` effort exclusively** while the cohort batting average remains in repair (now 20-of-20 substrate amendments needing council fixes). This review is the canonical one and supersedes the preliminary review's verdict.

If PR #499 has not yet merged when this review's PR opens, PR #499 should be closed without merge (its review file path collides with this canonical review's path; auto-merge on PR #499 would produce the wrong content at the canonical filename). If PR #499 has already merged before this canonical review opens, this review's PR will rename the merged file to `0071-council-review-preliminary-2026-05-04.md` before writing the canonical review at the standard path. The check at the time this PR was authored confirmed PR #499 was OPEN (not merged); this PR therefore writes directly at `0071-council-review-2026-05-04.md`.

---

## Process disclosure

This review applied the Stage 1.5 four-perspective adversarial method (Outside Observer / Pessimistic Risk Assessor / Skeptical Implementer / Devil's Advocate) followed by a UPF v1.2 Stage 2 anti-pattern scan over all 21 patterns. Cited PRs (#481, #483, #484, #485, #487, #490) were verified via `gh pr view` against `origin/main`. Cited file paths were verified via `ls` against the worktree at HEAD. Cited ADR numbers were verified to exist in `docs/adrs/`. Vocabulary counts cited in the ADR (10 tier values; 20 concern values; 5 status values; 7 pipeline_variant values) were verified by parsing `tools/adr-projections/project.py` directly.

The three-direction verification discipline (per the 2026-04-30 council-can-miss memory) was applied throughout: positive-existence (the cited symbol/file exists), negative-existence (no contradicting prior evidence), and structural-citation (the cited symbol's actual structure matches the ADR's claim about it). The structural-citation direction is where the ADR's most material finding emerged.

This is a tooling-tier ADR formalizing a pattern that has already been operationalized across PRs #481/#483/#484/#485/#487/#490. Per ADR 0069, process-tier and tooling-tier ADRs are SHOULD-have council pre-merge (not MUST-have); CO has authorized the canonical Opus council on this one because ADR 0071 documents the substrate that the cohort-wide structural-citation discipline runs on. An ADR that documents the validation discipline must itself withstand the validation discipline. That symmetry is load-bearing.

---

## Outside Observer perspective

### Fresh-reader test

A reader new to Sunfish opens `0071-adr-portfolio-system.md`. Within the first three paragraphs they learn:

1. Sunfish has 65 ADRs accumulated over ~14 months.
2. Pure-chronological journals scale O(N) for current-state queries.
3. The fix is event-sourcing-shaped: journal + projections + snapshot + foundational paper.

That framing lands. The four-layer model is named in the Decision section with a concise ASCII diagram, then each layer is given a paragraph of explanation. The Cold Start Test in §A0 walks through the artifact-discovery sequence (find schema → find tool → run check → understand model → find snapshot). A fresh reader has a working mental model after one read.

**Clarity strengths.** The Considered options section is unusually well-structured for an architecture ADR: each option states a clear position, lists pros/cons with material specifics (not "may have unknown costs"), and reaches a verdict that flows logically from the trade-off. Option A (renumber by topic) is dispatched with a quantified cost ("65 ADRs × average 3 cross-references = ~195 hard cross-references to update"). Option B (wiki) is dispatched on values-grounded reasoning ("auditable history is the core value of the ADR practice"). Option C survives both challenges.

**Citation grounding.** The PR citations (#481, #483, #484, #485, #487, #490) are concrete. The file-path citations (`tools/adr-projections/project.py`, `docs/adrs/_FRONTMATTER.md`, `docs/architecture/snapshot-2026-Q2.md`) are verifiable. The vocabulary counts (10 tiers, 20 concerns, 5 statuses, 7 pipeline variants) are auditable against `project.py`. This is ADR-as-substrate-for-its-own-validation; the ADR is asserting facts about a system whose validator is checked into the same repository.

**One readability friction.** The "Decision drivers" list (nine bullets, 26 lines) and the "Considered options" section together front-load 9 + (3 × 6) = 27 distinct claims before the reader reaches the actual Decision. A future reader who knows what they want is buying convenience; a fresh reader is paying patience tax. This is structural to thorough ADRs and not a bug, but it is a tax.

### Outside-Observer findings

- **OO-1 (mechanical, low):** The "65 ADRs" count in Context (line 38) and the "61 ADRs" count cited from the Q2 snapshot (line 357 implies the snapshot context; snapshot top says "61 ADRs in `docs/adrs/`") create a temporal-drift footnote a fresh reader has to reconcile. The ADR is correct that the count was 61 at snapshot time and 65+ at authoring time, but the document does not flag that drift explicitly. Add one sentence — "The journal grew from 61 ADRs at snapshot date to 65+ at this ADR's authoring date" — to the Context section's third paragraph.

- **OO-2 (mechanical, low):** The Implementation checklist line 388 says CI workflow `adr-validation.yml` is "in progress (worktree `sunfish-adr-ci-wt` at time of authoring; pending PR)." A reader reaching this line two weeks later will not know whether the workflow shipped. The ADR should reference a follow-on PR number once one exists, or note that the checkbox `[ ]` will be flipped to `[x]` in a follow-on commit when the workflow merges. Currently the checkbox can become silently stale.

- **OO-3 (non-mechanical, low):** The Decision section uses the term "event-sourcing-with-snapshots" as if it is a well-known pattern. It is — but with caveats. Standard event-sourcing's snapshots are *re-derivable* from the log; the Layer 3 quarterly snapshot in this design is *hand-curated*. That distinction is glossed in the ADR. The Devil's Advocate perspective re-opens this; flagged here as a reading-comprehension issue. A short clarifier paragraph in the Decision section — "Layer 3 snapshots are hand-curated narrative, not auto-derived; this is the deliberate divergence from textbook event-sourcing" — would improve framing accuracy.

---

## Pessimistic Risk Assessor perspective

### Failure modes considered

**FM-1: The projection tool's hand-rolled YAML parser silently mis-parses an unusual frontmatter case.** `tools/adr-projections/project.py` contains a "minimal hand-rolled YAML parser sufficient for the schema's subset" per the README. Any YAML edge case the parser does not handle (e.g., multi-line strings, escaped quotes, anchor/alias references, flow style) becomes a silent validation hole. The ADR does not document the parser's accepted YAML subset. Risk: an ADR author's frontmatter passes `--check-only` but is mis-parsed in projection generation, producing a wrong INDEX/STATUS/GRAPH entry. Mitigation: the schema spec in `_FRONTMATTER.md` shows minimal/full/superseded examples; in practice, frontmatter is structurally narrow and parsing failures would manifest as visible projection errors. Severity: low. Remediation: add a one-line note to the ADR's "Projection tool contract" section enumerating the YAML subset (key/value, lists, integer scalars, ISO-8601 dates) — or reference `_FRONTMATTER.md` examples as the de-facto allowed surface.

**FM-2: A bulk migration script (`bulk_apply_frontmatter.py`) is retained as "a reference for future bulk operations."** Retaining one-shot migration scripts as living code in `tools/adr-projections/` creates a small but real failure mode: a contributor running the wrong script in the wrong context could clobber existing frontmatter. The script is currently labeled "kept as reference" but is not gated against re-execution. Risk: low (small repo; contributors read READMEs; unique invocation pattern). Mitigation: existing tooling sits next to a README; ADR 0071 already calls out that the script is a reference. Severity: low. The risk is real but not blocking.

**FM-3: Projections become stale and CI does not catch it.** The ADR's "Open question 1" explicitly names this: CI validates frontmatter via `--check-only` but does not verify `STATUS.md` / `INDEX.md` / `GRAPH.md` are up-to-date with current frontmatter. A contributor merges an ADR with new frontmatter but forgets to re-run the projection tool; the committed projections drift; readers consulting `INDEX.md` get out-of-date answers. The ADR explicitly defers this gate as "sufficient while the team is small." This is a defensible deferral. Severity: medium. The ADR has named the kill trigger ("team grows beyond 10 contributors"); that triggers the open-question resolution. The deferral is conscious.

**FM-4: A vocabulary value drift between `project.py` and `_FRONTMATTER.md`.** Adding a `tier` or `concern` requires touching both files. If they drift, the projection tool will accept frontmatter that the spec considers invalid (or vice versa). Risk: medium. There is no test that asserts `project.py`'s `VALID_*` sets equal the `_FRONTMATTER.md` enumeration. Mitigation: small surface; review catches it. Severity: low-medium. Remediation: a small unit test (or a `--print-vocab` flag plus a `_FRONTMATTER.md` fence-block grep) could mechanically enforce parity.

### Blast radius if the system is wrong

The blast radius of the ADR portfolio system being wrong is **bounded and benign**. Projections are derived; deleting them is a `git checkout` away. The journal (the actual ADRs) is unaffected. CI gates can be removed. The frontmatter blocks themselves are 5–25 lines per ADR; a one-PR script could strip them across 65 files. The ADR's "Rollback strategy" section in §A0 names this correctly: "The ADRs themselves remain intact." This is the correct framing — the system is additive scaffolding, not load-bearing structure.

The blast radius of the structural-citation finding (next perspective) is similarly bounded: a wrong example in the Context section does not change the Decision. It does undermine the Context paragraph's credibility, and on a discipline-substrate ADR, that's a non-trivial signal. But it does not change what gets adopted.

### Trust boundary changes

None. The ADR portfolio system is documentation tooling. There is no key material, no PII, no cryptographic operation, no cross-tenant boundary, no actor authorization. The ADR's "Trust impact / Security and privacy" subsection states this correctly. Concur.

### Pessimistic-Risk-Assessor findings

- **PRA-1 (non-mechanical, low):** The hand-rolled YAML parser's accepted subset is undocumented. Add a one-line note to the "Projection tool contract" section — non-blocking but improves the contract's auditability.

- **PRA-2 (non-mechanical, medium):** Vocabulary drift between `project.py` and `_FRONTMATTER.md` is not tested. Add to "Open questions" — defer the test until the second drift incident, but acknowledge the risk class.

- **PRA-3 (acceptance-eligible deferral):** Open question 1 (projection freshness in CI) is correctly framed as deferred. No action.

---

## Skeptical Implementer perspective

This is the perspective that yielded the review's most material finding. The Skeptical Implementer reads ADR 0071 line by line and verifies every concrete claim against repository state.

### Three-direction verification (per 2026-04-30 council-can-miss memory)

#### Direction 1: Positive-existence

| Claim in ADR | Verified | Source |
|---|---|---|
| `docs/adrs/_FRONTMATTER.md` exists | YES | `ls` against worktree |
| `docs/adrs/_template.md` exists | YES | `ls` against worktree |
| `tools/adr-projections/project.py` exists | YES | `ls` against worktree |
| `tools/adr-projections/README.md` exists | YES | `ls` against worktree |
| `tools/adr-projections/bulk_apply_frontmatter.py` exists | YES | `ls` against worktree |
| `docs/architecture/snapshot-2026-Q2.md` exists | YES | `ls` against worktree |
| `docs/adrs/STATUS.md`, `INDEX.md`, `GRAPH.md` exist | YES | `ls` against worktree |
| ADR 0018 — Governance and License Posture exists | YES | `0018-governance-and-license-posture.md` |
| ADR 0037 — CI Platform Decision exists | YES | `0037-ci-platform-decision.md` |
| ADR 0038 — Branch Protection via Rulesets exists | YES | `0038-branch-protection-via-rulesets.md` |
| ADR 0042 — Subagent-Driven Development exists | YES | `0042-subagent-driven-development-for-high-velocity.md` |
| ADR 0069 — ADR Authoring Discipline exists | YES | `0069-adr-authoring-discipline.md` |
| ADR 0070 — Multi-Session Naval-Org Structure exists | YES | `0070-multi-session-naval-org-structure.md` |
| PR #481 (portfolio foundation) merged | YES | `mergedAt: 2026-05-02T09:42:11Z` |
| PR #483 (Stage 4 bulk apply) merged | YES | `mergedAt: 2026-05-02T10:00:34Z` |
| PR #484 (consumed_by auto-derive) merged | YES | `mergedAt: 2026-05-04T09:38:51Z` |
| PR #485 (concern-tag refinement) merged | YES | `mergedAt: 2026-05-02T13:08:18Z` |
| PR #487 (first quarterly snapshot) merged | YES | `mergedAt: 2026-05-02T13:52:52Z` |
| PR #490 (composes/extends backfill) merged | YES | `mergedAt: 2026-05-04T10:18:58Z` |

All positive-existence claims verified.

#### Direction 2: Negative-existence

| Claim implied by ADR | Verification |
|---|---|
| "in progress" CI workflow `adr-validation.yml` | Confirmed not yet present in `.github/workflows/`; the `[ ]` checkbox in the implementation checklist is honest. |
| The 4-ADR pilot (0001, 0028, 0049, 0062) | Verified by inspecting PR #481's file changes; exactly those four `.md` files plus the schema/template/tool/projection files. |
| 56-ADR bulk in PR #483 | PR #483's `files` list shows 57 modified ADR `.md` files (not 56). Off-by-one — see SI-2 below. |

#### Direction 3: Structural-citation (this is where the material finding sits)

The structural-citation direction asks: **for every claim of the form "ADR X says/has Y", does ADR X actually say/have Y?** Reading the ADR text alone is not enough; we have to read the cited ADR.

ADR 0071 line 53–56 states:

> "the 2026-Q2 quarterly snapshot review cycle flagged 'tooling tier' as a gap: only one prior ADR (ADR 0042 — Subagent-Driven Development) carried `tier: tooling`, and the scaffolding CLI, Roslyn analyzers, and ADR projection tooling were entirely undocumented at the architectural decision level (Stage 5 quarterly snapshot review, PR #487)."

I verified this claim two ways:

1. **Directly read ADR 0042's frontmatter on origin/main:**

   ```yaml
   ---
   id: 42
   title: Subagent-Driven Development for High-Velocity Sessions
   status: Accepted
   date: 2026-04-26
   tier: governance
   ```

   ADR 0042 carries `tier: governance`, **not** `tier: tooling`.

2. **Read the auto-derived `INDEX.md` projection that the ADR portfolio system itself produces:**

   ```
   ### tooling (1)
   - ADR 0010 — [Templates Module Boundary (Foundation.Catalog vs. blocks-templating)](./0010-templates-boundary.md)
   ```

   The single ADR with `tier: tooling` on origin/main is **ADR 0010 — Templates Module Boundary**, not ADR 0042.

3. **Read the Q2 snapshot itself (the cited source of the gap-finding):**

   The snapshot's section 1 has a heading "Tooling (1 ADR) and Process (1 ADR)" and the body of that section says:

   > "ADR 0010 covers the templates module boundary (Foundation.Catalog vs. blocks-templating extraction criteria) ... ADR 0040 covers the AI-first translation workflow ..."

   The snapshot's Governance section says:

   > "ADR 0042 (Subagent-Driven Development) is also classified governance ..."

   The cited source — the Q2 snapshot — explicitly classifies ADR 0042 as `governance`, not `tooling`. The single `tier: tooling` ADR per the snapshot is ADR 0010.

The ADR's claim that "ADR 0042 — Subagent-Driven Development carried `tier: tooling`" is **structurally wrong on two axes**:

- ADR 0042 carries `tier: governance`, not `tier: tooling`.
- The single prior ADR with `tier: tooling` is ADR 0010 (Templates Module Boundary), not 0042.

This is exactly the AP-21 (cited-symbol drift) class that ADR 0069 was authored to prevent. The irony is not lost on this reviewer: the ADR formalizing the system that prevents structural-citation failures contains a structural-citation failure in its own Context section, on the very example the ADR uses to motivate the system. The example is the *gap-detection* example — "the snapshot revealed a tooling-tier gap" — and the example cites the wrong ADR.

This finding does not change the Decision. The four-layer model is sound. The projection tool is correct. The Q2 snapshot did flag a tooling-tier gap (the snapshot's own text confirms it). What's wrong is the ADR's example of which ADR carried `tier: tooling`. The fix is mechanical: replace "ADR 0042" with "ADR 0010" and replace "Subagent-Driven Development" with "Templates Module Boundary" in the Context paragraph.

But it must be fixed before merge. ADR 0071 is the substrate for the structural-citation discipline; allowing a structural-citation failure to merge in ADR 0071 itself would silently undermine the discipline's authority across the cohort. The discipline gains its force from the substrate's example correctness.

### Skeptical-Implementer findings

- **SI-1 (structural-citation, BLOCKING):** ADR 0071 line 53–56 cites "ADR 0042 — Subagent-Driven Development" as the single prior ADR with `tier: tooling`. ADR 0042 carries `tier: governance`. The single prior `tier: tooling` ADR is **ADR 0010 — Templates Module Boundary**. Both `INDEX.md` and the Q2 snapshot's own text confirm this. The ADR must be amended before merge. Replace "ADR 0042 — Subagent-Driven Development" with "ADR 0010 — Templates Module Boundary" in the Context paragraph. The surrounding sentence ("the scaffolding CLI, Roslyn analyzers, and ADR projection tooling were entirely undocumented") remains correct and the gap-detection logic remains valid; only the cited example is wrong.

- **SI-2 (mechanical, low):** ADR 0071 line 287–289 says the bulk migration applied frontmatter "to all 56 ADRs that predated the schema introduction." PR #483's actual file list shows 57 modified `.md` ADRs. The "56-ADR" count appears in PR #483's title and ADR 0071's prose. Either both are off-by-one or the ADR's count is the count of ADRs **strictly predating the schema** (excluding the four pilot ADRs from PR #481, which were already covered, but counting the rest). I cannot reconstruct the exact intended count from the ADR text alone. Recommend either (a) clarify the count to "57 ADRs" or "56 ADRs plus an additional already-covered pilot member" or (b) reference PR #483's title verbatim and let the PR's commit log carry the count. Off-by-one is structurally minor; clarity matters more than the exact integer.

- **SI-3 (mechanical, low):** ADR 0071 line 173 says the bulk migration was "a 56-ADR bulk apply" and line 384 says "56-ADR bulk frontmatter apply — PR #483." Same off-by-one as SI-2. If SI-2 is fixed, fix here too.

- **SI-4 (mechanical, low):** ADR 0071 line 38 ("Sunfish had 65 Architecture Decision Records") and line 357 ("All 65 ADRs in `docs/adrs/`") and line 329 ("requiring them to read all 65+ ADRs") establish 65 as the round-number canonical size at authoring date. The Q2 snapshot top says 61. One sentence in Context bridging this drift would help fresh readers; OO-1 named this from a different perspective.

- **SI-5 (positive observation, no action):** The Implementation checklist at lines 374–390 is the single best ADR-completion checklist this reviewer has seen in the Sunfish portfolio. Each item names a PR or worktree; the two `[ ]` items are honestly scoped (CI workflow pending; this ADR's own acceptance pending). This deserves replication in future tooling-tier ADRs.

---

## Devil's Advocate perspective

This perspective challenges the framing rather than the facts. Was the four-layer model genuinely the right framing? Were simpler alternatives genuinely considered?

### Was four layers the right number?

The ADR proposes: Journal (Layer 1) → Projections (Layer 2) → Quarterly snapshot (Layer 3) → Foundational paper (Layer 4). Four layers.

A genuine devil's-advocate reading: **could this be three layers?** Specifically:

- Merge Layer 3 (quarterly snapshot) and Layer 4 (foundational paper) into a single "narrative layer" with two sub-types: stable long-horizon (the paper) and per-quarter snapshot. The four-layer framing distinguishes them as separate layers; in practice they sit at the same level of abstraction (synthesized narrative drawing on the journal).

The ADR's defense: the foundational paper is a synced copy from outside the repository (`_shared/product/local-node-architecture-paper.md`); it has a distinct authorship, distinct cadence, and distinct authority. The quarterly snapshot is repo-local, hand-curated, and quarterly. Merging them would conflate two very different artifacts. **This defense holds.** Four layers is right.

A second devil's-advocate reading: **could the projection layer be skipped entirely?** The validator can run on every PR; structural drift is caught there; the journal itself is human-readable; do we need committed `STATUS.md` / `INDEX.md` / `GRAPH.md` artifacts?

The ADR's defense (in the Decision and the Consequences): committed projections are visible in PR diffs, readable without running the tool, and audit-trail-anchored to specific commits. A reader who clones the repo and never runs the tool still gets accurate current-state queries. **This defense also holds.** The committed-projection design is not the only viable design but it is a defensible one.

### Were simpler alternatives genuinely considered?

The ADR considers two: renumber by topic (Option A), wiki replacement (Option B). What about:

- **"Better grep" alternative.** Just write a one-line shell helper (`grep -l "tier: foundation" docs/adrs/`) and skip the projection tool entirely. This is the *simplest* possible alternative.

  The implicit defense in the ADR's Decision drivers ("O(1) discoverability" + "reduced pre-merge council corrections") is that grep does not solve drift detection (validation) and does not solve graph traversal (Mermaid output). But the ADR does not explicitly compare against grep-alone. A devil's-advocate reading: this is a *real* gap in the Considered options.

  However, the ADR's Decision drivers do enumerate what the projection tool provides beyond grep: validation, drift detection, derived `consumed_by`, controlled-vocabulary enforcement, Mermaid graph generation. Grep delivers none of these. So while grep-alone was not explicitly compared, the differential capability of the tool over grep is itemized. Acceptable.

- **"Single living architecture document" alternative.** Maintain a single `ARCHITECTURE.md` that is the source of truth, edited each time a decision is made, with the per-decision history kept in a `decisions/` subdirectory or in commit messages.

  The ADR's implicit defense (in Option A's con list): "Renumbering severs the commit-log connection between an ADR and its review history." This argument extends naturally to a single living document — the per-decision review record gets buried in commit history rather than being the artifact itself. The ADR practice's core value is per-decision traceability; a single living document trades that for navigation convenience. The ADR is implicitly making this argument; making it explicit would strengthen Option B's con list.

- **"Topical-by-default with chronological as one projection" alternative.** Reverse the layer model: organize the journal *topically* (by tier or concern), and make chronology one of the projections rather than the structure.

  This is the most interesting devil's-advocate framing. Topical organization solves "which ADRs touch security?" without any projection tool — by walking the directory structure. Chronology becomes a derived view (the projection is `CHRONOLOGY.md`). This trades the immutability invariant (numbers as immutable IDs) for navigation primitive convenience.

  The ADR's defense: the immutability of ADR numbers is load-bearing across 65+ existing cross-references in commits, CI config, memory files, the foundational paper's appendices, and prior ADR bodies. Reorganizing topically would require renumbering; renumbering breaks the immutability invariant. The journal-of-events shape is the right default for an *append-only* discipline; topical-by-default is the right default for a *current-state* discipline; an ADR practice is closer to the former. **This defense holds.**

### Was "the full pattern was already shipped" framing the right framing?

ADR 0071's Implementation checklist labels itself as "post-hoc verification that the full pattern was implemented." Is post-hoc verification the right discipline? Should an ADR document a pattern *before* it ships, not *after*?

The ADR's defense (Confidence level §A0): "The system described here has already been built and is in production use across 65+ ADRs with `--check-only` passing. This ADR formalizes an observed working pattern, not a speculative design."

This is a defensible framing for *Stage 5 cleanup*: the pattern was discovered during the bulk migration; the post-hoc ADR formalizes the discovery. But there is a residual concern: ADR 0042 also was authored as a "backfill" ADR for an already-running pattern, and ADR 0071 — by carrying the same framing — implicitly normalizes "ship first, ADR later" for tooling work. That's acceptable for this case (the system is bounded and reversible) but is worth flagging as a pattern not to over-extend.

### Devil's-Advocate findings

- **DA-1 (non-mechanical, low):** Add a single-sentence consideration of the "grep alone" alternative to the Considered options section, even if dismissed in one paragraph. The Decision drivers cover the ground but the Considered options pretends only Options A/B/C exist.

- **DA-2 (non-mechanical, low):** Add an acknowledgment in the Decision section that Layer 3's snapshots are *hand-curated* (not auto-derived from the journal). This is a deliberate divergence from textbook event-sourcing and the divergence is currently glossed.

- **DA-3 (out-of-scope, no action):** "Ship first, ADR later" is acceptable for tooling-tier work but should not be treated as the default for substrate-tier work. This is a meta-comment on ADR cadence, not an ADR 0071 finding. Note for future cohort discussions.

---

## Decision Discipline Rule 3 — mechanical vs. non-mechanical classification

Per the 2026-04-30 decision-discipline memory: mechanical amendments (rename, fix-citation, scope-tightening, off-by-one count fixes, missing-cross-reference adds) are auto-accepted; non-mechanical amendments (changing the Decision, changing the four-layer model, changing the YAML schema, changing the controlled vocabularies) require CO sign-off.

| Finding | Class | Severity | Auto-accept eligible? |
|---|---|---|---|
| SI-1: ADR 0042 → ADR 0010 cited-symbol fix in Context | Mechanical (fix-citation) | BLOCKING | Yes |
| SI-2: 56 → 57 (or clarification) bulk-migration count | Mechanical (off-by-one) | Low | Yes |
| SI-3: Same as SI-2 at line 384 | Mechanical (off-by-one) | Low | Yes |
| SI-4 / OO-1: 65 vs 61 temporal-drift sentence | Mechanical (clarifier add) | Low | Yes |
| OO-2: CI workflow checkbox staleness | Mechanical (post-merge follow-up) | Low | Yes |
| OO-3 / DA-2: Layer 3 hand-curated clarifier | Mechanical (clarifier add) | Low | Yes |
| PRA-1: YAML subset documentation | Mechanical (clarifier add) | Low | Yes |
| PRA-2: Vocabulary drift open-question add | Mechanical (open-question add) | Low | Yes |
| DA-1: "Grep alone" Considered options paragraph | Non-mechanical (Considered options structural change) | Low | No — minor structural decision; CO discretion |
| DA-3: "Ship first, ADR later" pattern note | Out-of-scope | n/a | n/a |

All findings are **mechanical except DA-1**, which is borderline (adds a paragraph to Considered options without changing any verdict). DA-1 can be accepted by CO as part of the same amendment cycle without re-council.

---

## UPF v1.2 Stage 2 anti-pattern scan (21 patterns)

| AP | Pattern | Hit? | Notes |
|---|---|---|---|
| AP-1 | Unvalidated assumptions | No | Decision drivers + Confidence level both itemize the empirical basis (system in production). The ~65% structural-citation failure rate is flagged as approximate. |
| AP-2 | Vague phases | n/a | Not a phased-plan ADR. |
| AP-3 | Vague success criteria | No | FAILED conditions in §A0 are explicit and named. |
| AP-4 | No rollback | No | Rollback strategy explicit in §A0. |
| AP-5 | Plan ending at deploy | No | Quarterly snapshot cadence + revisit triggers establish ongoing operation. |
| AP-6 | Missing Resume Protocol | n/a | Tooling ADR; no resume-protocol concept. |
| AP-7 | Delegation without contracts | n/a | No delegation. |
| AP-8 | Blind delegation trust | n/a | No delegation. |
| AP-9 | Skipping Stage 0 | No | Three options evaluated; AHA pass present. |
| AP-10 | First idea remaining unchallenged | No | Two prior options were rejected with reasoning before Option C was adopted. |
| AP-11 | Zombie projects | No | Revisit triggers named (200-ADR threshold, CI tool chain change, team >10, multilingual). |
| AP-12 | Timeline fantasy | No | No timeline claims; system is shipped. |
| AP-13 | Confidence without evidence | No | Confidence level is HIGH and grounded in production use across 65+ ADRs. |
| AP-14 | Wrong detail distribution | No | Decision drivers, options, schema, tool contract, migration, invariants are each at the right depth. |
| AP-15 | Premature precision | No | Vocabulary counts (10/20/5/7) are auditable against `project.py`; no other precision claims. |
| AP-16 | Hallucinated effort estimates | No | No effort estimates. |
| AP-17 | Delegation without context transfer | n/a | No delegation. |
| AP-18 | Unverifiable gates | No | Gates are verifiable: `--check-only` exits non-zero; CI blocks merge. |
| AP-19 | Missing tool fallbacks | Partial | Open question 2 names "Python 3 dropped from CI" as a triggered re-evaluation; rollback is fall-back. Adequate but not a written-down fallback. |
| AP-20 | Discovery amnesia | No | The Considered options section preserves the rejected alternatives' reasoning. |
| AP-21 | **Cited symbols / citations without sources** | **HIT — SI-1** | The "ADR 0042 — Subagent-Driven Development carried `tier: tooling`" claim in the Context section is structurally wrong. ADR 0042 carries `tier: governance`. The actual `tier: tooling` ADR is 0010. |

**Scan result:** 1 critical hit (AP-21, SI-1, BLOCKING) plus AP-19 partial (low, non-blocking). All other patterns clean.

---

## Verdict

**NEEDS-AMENDMENT.** One BLOCKING structural-citation finding (SI-1: ADR 0042 → ADR 0010) plus seven low-severity mechanical findings. All findings are auto-accept-eligible per Decision Discipline Rule 3 except DA-1, which is borderline non-mechanical but can be accepted at CO discretion.

**Path to PASS:**

1. Apply SI-1 mechanically: replace "ADR 0042 — Subagent-Driven Development" with "ADR 0010 — Templates Module Boundary" in Context paragraph (lines 53–56). Surrounding sentence remains correct.

2. Apply SI-2 / SI-3 mechanically: clarify "56-ADR bulk apply" to "57-ADR bulk apply" (or restate as "56 previously uncovered ADRs plus the 5 already-covered pilot members" to match PR #483's 57 modified ADR `.md` files). Pick the wording that best matches the historical fact.

3. Apply OO-1 / SI-4 mechanically: add one sentence to Context paragraph 1 noting "61 ADRs at Q2 snapshot date (2026-05-02); 65+ ADRs at this ADR's authoring date."

4. Apply OO-3 / DA-2 mechanically: add one sentence to the Decision section's Layer 3 description noting "Layer 3 snapshots are hand-curated narrative, not auto-derived; this is the deliberate divergence from textbook event-sourcing."

5. Apply PRA-1 mechanically: add one sentence to "Projection tool contract" listing the YAML subset accepted by the hand-rolled parser (key/value scalars, lists, integer scalars, ISO-8601 dates) or reference `_FRONTMATTER.md`'s examples as the de-facto allowed surface.

6. Apply PRA-2 mechanically: add a fourth open question about vocabulary drift between `project.py` and `_FRONTMATTER.md`, with deferral until first drift incident.

7. Apply OO-2 mechanically: add a follow-on PR reference for the CI workflow once it lands, or a note that the `[ ]` checkbox at line 388 will be flipped in a follow-on commit.

8. (CO discretion) Apply DA-1 non-mechanically: optionally add a one-paragraph "grep alone" sub-option to Considered options.

After amendments 1–7 are applied, the ADR is PASS. Recommend applying them as a single follow-on commit on the ADR's branch (race-condition-safe; this council file does not touch the ADR's branch per CO directive).

---

## Recommendations to CO

1. **Accept the canonical Opus council verdict (NEEDS-AMENDMENT) and instruct the ADR author (XO) to apply the seven mechanical fixes plus the borderline DA-1 fix in a single amendment commit on `docs/adr-0071-adr-portfolio-system`.** All fixes are auto-accept-eligible per Rule 3. No re-council is needed.

2. **Close the preliminary Sonnet council PR #499 without merge.** Its review file path collides with this canonical review's path. The canonical review supersedes any verdict the preliminary review reached.

3. **Treat SI-1 (the AP-21 hit on the ADR's own motivating example) as a learning signal, not a process failure.** The 20-of-20 cohort batting average has now extended to "21-of-21 substrate amendments needed council fixes" — this is itself empirical confirmation that the pre-merge council canonical (per ADR 0069) is the right gate. The discipline catching its own substrate ADR is exactly what the discipline was designed to do.

4. **Note that the Skeptical Implementer's three-direction discipline (positive-existence + negative-existence + structural-citation) was the perspective that surfaced SI-1.** Negative-existence and positive-existence both passed; structural-citation alone caught it. This is consistent with the 2026-04-30 council-can-miss memory's finding that structural-citation is the most easily missed direction — and reinforces the memory's guidance to read the cited ADR's actual schema rather than relying on grep.

5. **Once ADR 0071 merges (post-amendment), the cohort should consider this ADR an authority on the discipline that prevented its own near-merge with a structural-citation failure.** The substrate-validates-its-own-substrate symmetry is load-bearing for the practice.

---

**End of canonical Opus council review.**
