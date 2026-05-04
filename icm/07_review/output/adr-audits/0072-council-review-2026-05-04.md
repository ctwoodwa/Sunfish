# Council Review — ADR 0072 (Research-Inbox Beacon Protocol)

**Review date:** 2026-05-04
**Reviewer:** XO research subagent (pre-merge canonical per ADR 0069; process-tier ADR)
**Review posture:** standard adversarial (4 perspectives) + UPF v1.2 Stage 2 meta-validation + 21 anti-pattern scan
**ADR tier:** `process` — per ADR 0069 §Decision, pre-merge council is RECOMMENDED (not required)
for non-substrate ADRs. Running as `canonical` per CO direction for this review cycle.
**Archive verified:** 13 resolved beacons confirmed in
`icm/_state/research-inbox/_archive/`; all 9 W#18 unblock-chain PRs (#325/#326/#329/#330/
#331/#333/#335/#337/#338) verified as merged.

---

## Findings (8 total: 0 Critical, 3 Major, 5 Minor)

---

### F1 — Major: Context section misrepresents ADR 0070's handling of the beacon protocol

**Perspective:** Outside Observer / Skeptical Implementer (factual-citation correctness)

**Issue:** ADR 0072's Context section states that ADR 0070 "flags the pattern as a
'potential future ADR candidate'" — implying ADR 0070 defers the beacon specification
to a future document. This is not accurate. ADR 0070 (merged PR #489) contains a
comprehensive §6 — "Live signaling: the research-inbox beacon protocol" — spanning
approximately 25 lines with the scan command, sender table, archive policy, and 7-day
SLA. ADR 0070 also addresses the protocol in OQ-2 (automated beacon processing) and
OQ-3 (PAO cross-repo worktree fragility). The text "potential future ADR candidate" does
not appear anywhere in ADR 0070's merged text.

The actual relationship is: ADR 0070's §6 provides a working operational description
adequate for the naval-org governance context; ADR 0072 expands and formalizes that
description into a dedicated specification. The motivation for a separate ADR is real
(sufficient surface area: file naming, body schema, archive policy, escalation thresholds,
spam mitigation, cross-repo handling) but the Context section overstates ADR 0070's
deferral posture to justify that motivation.

**Disposition:** Non-mechanical (requires author correction of a factual claim).
Recommendation: Replace the "flags the pattern as a 'potential future ADR candidate'"
sentence with an accurate summary: "ADR 0070 §6 provides an operational description of
the pattern; this ADR provides the full specification — filename convention, body schema,
sender table, processing protocol, and archive/escalation policy — to supersede the prose
as the authoritative reference."

---

### F2 — Major: Body schema specification conflicts with the canonical example it cites

**Perspective:** Skeptical Implementer (structural-citation correctness)

**Issue:** ADR 0072 §3 states two rules that conflict with the beacon it identifies as the
canonical exemplar:

1. **Rule:** "Frontmatter must be valid YAML (3 keys exactly; no extras unless XO approves
   schema extension via ADR amendment)."
   **Reality:** `pao-incident-2026-04-30T07-35Z-destructive-action-reset-hard.md` — cited
   in §3 as "the canonical example of an extended-context beacon" — contains **6
   frontmatter keys**: `type`, `sender`, `chapter`, `date`, `last-pr`, `severity`. This
   exceeds the 3-key limit and includes keys not in the specified schema (`sender`,
   `chapter`, `date`, `severity`). The "canonical example" violates the rule it is meant
   to illustrate.

2. **Schema key mismatch:** The spec defines the three required keys as `type`,
   `workstream-or-chapter`, and `last-pr`. Inspection of the 13 archive beacons shows
   that all COB beacons use `workstream` (not `workstream-or-chapter`) as the key name.
   `pao-incident` uses `chapter` (not `workstream-or-chapter`). No beacon in the archive
   uses the `workstream-or-chapter` key name as specified. The spec documents a convention
   that no existing beacon follows.

Additionally, two later-era beacons (`cob-idle-2026-04-30T16-00Z-priority-queue-dry.md`,
`cob-question-2026-04-30T03-58Z-w28-p5-w20-substrate-adaptation.md`) have 5-key frontmatter
including `filed-by` and `filed-at` keys absent from the spec.

**Disposition:** Non-mechanical. Author must decide: (a) adopt `workstream-or-chapter` as the
canonical key and flag existing beacons as pre-formalization variants, or (b) adopt
`workstream` for COB beacons + `chapter` for PAO beacons as two legal variants and update the
schema table accordingly. The "3 keys exactly" rule must either be enforced (with the
canonical-example beacon re-classified as spec-non-conformant pre-formalization artifact) or
softened to "3 base keys minimum; additional keys require XO approval." Whichever path is
chosen, the canonical-example citation must point to a beacon that actually conforms.

---

### F3 — Major: PAO cross-repo worktree procedure omits branch cleanup; creates dangling branches

**Perspective:** Skeptical Implementer / Pessimistic Risk Assessor

**Issue:** ADR 0072 §4 specifies the PAO cross-repo worktree procedure. The final step is:

```bash
git -C /Users/christopherwood/Projects/Sunfish worktree remove /tmp/sunfish-pao-signal-wt
```

`git worktree remove` removes the linked worktree directory but does **not** delete the
tracking branch (`pao/signal-<timestamp>`). Every PAO signal execution leaves an orphaned
remote-tracking branch. Over time (at current ADR's estimated "one to several PAO beacons
per session-month") this produces accumulating branch noise in the Sunfish remote. The
missing step is:

```bash
git -C /Users/christopherwood/Projects/Sunfish branch -d pao/signal-$(date +%Y%m%dT%H%MZ)
```

Note: the `date` invocation in the cleanup must match the `date` used when the worktree
was created. A safer approach uses an explicit variable rather than re-invoking `date`
(timestamps can cross minute boundaries).

ADR 0070 OQ-3 already flags that the PAO cross-repo worktree pattern is fragile and defers
resolution until a dropped beacon is observed. ADR 0072 codifies the fragile procedure
without improving it and without cross-referencing OQ-3. At minimum, the ADR should note
that branch cleanup is required and that OQ-3 fragility remains open.

**Disposition:** Non-mechanical (procedural gap, not just a wording fix). Recommended fix:
add an explicit branch-deletion step with a variable-based approach to avoid timestamp
re-derivation; add a cross-reference to ADR 0070 OQ-3 as an acknowledged open concern.

---

### F4 — Minor: "Three senders" claim contradicts the explicit parenthetical listing two senders

**Perspective:** Outside Observer (factual-citation correctness)

**Issue:** ADR 0072 Context section states: "13 beacons from **three senders** (`cob`,
`pao`)." The parenthetical names exactly two senders. Inspection of the archive confirms
11 `cob-*` beacons and 2 `pao-*` beacons; no `yeoman-*` beacons are present.
The archive does not demonstrate a three-sender protocol; it demonstrates a two-sender
protocol during the 2026-04-29/30 period.

**Disposition:** Mechanical. Change "three senders (`cob`, `pao`)" to "two senders
(`cob`, `pao`)."

---

### F5 — Minor: References section incorrectly states ADR 0070 "awaiting CO accept" (it is merged)

**Perspective:** Skeptical Implementer (factual-citation correctness)

**Issue:** ADR 0072 References section states: "**ADR 0070** (not yet merged; PR #489
awaiting CO accept) — naval command structure." PR #489 is verified as merged (title:
"docs(adrs): 0070 — Multi-Session Naval-Org Structure (CO/XO/COB/PAO/Yeoman)"; state:
MERGED). ADR 0070 is present in `origin/main:docs/adrs/0070-multi-session-naval-org-structure.md`.

This is a stale draft-time note that was not updated before submission.

**Disposition:** Mechanical. Update the ADR 0070 reference to remove the parenthetical
and cite it as a merged ADR.

---

### F6 — Minor: Directional language in W#31 unblock-chain reference is inverted

**Perspective:** Outside Observer (clarity)

**Issue:** ADR 0072 References §"Unblock chains" states:
"`cob-idle-2026-04-29T20-42Z-31-built-queue-dry.md` **signaled COB** at rung 6."

This is backwards. The beacon is written *by* COB to signal *XO*. The correct
reading is: "COB wrote the beacon at rung 6; XO received the signal and queued
three follow-on workstreams." The phrase "signaled COB" implies XO sent a signal to
COB, which inverts the actual direction of communication and could confuse a session
reading the protocol for the first time (exactly the "Outside Observer" failure mode
ADR 0072 aims to prevent).

**Disposition:** Mechanical. Change "signaled COB at rung 6" to "written by COB at
rung 6 to signal XO."

---

### F7 — Minor: Slug length rule conflicts with archive examples

**Perspective:** Pedantic Lawyer

**Issue:** ADR 0072 §2 specifies slug as "2-5 hyphen-separated lowercase words." The
archive contains `cob-question-2026-04-29T19-12Z-31-taxonomy-prerequisites.md` whose slug
`31-taxonomy-prerequisites` has only 3 hyphen-separated segments, where `31` is a number
not a word. More importantly, the slug constraint of "2-5 words" is applied inconsistently:
`w28-p5c4-capability-verifier` has 3 segments where `w28` and `p5c4` are alphanumeric codes,
not natural-language words. The "lowercase words" framing doesn't cleanly cover
workstream/phase codes (`w28`, `p5c4`, `ch22`), which are the dominant slug component type.

This ambiguity matters for any future CI lint that validates beacon filenames: the regex must
handle alphanumeric codes, not just dictionary words.

**Disposition:** Mechanical. Change "2-5 hyphen-separated lowercase words" to "2-5
hyphen-separated lowercase tokens (words or alphanumeric codes such as `w28`, `p5c4`,
`ch22`)."

---

### F8 — Minor: §3 prose limit rule cannot apply to the entire extended-context class; rule is unenforceable as written

**Perspective:** Pessimistic Risk Assessor

**Issue:** ADR 0072 §3 states the context block is "≤2 lines, ≤120 characters each. Facts
only; no narrative." The rule then immediately creates an exception: "Beacons that require
extended context... may exceed the prose limits." The exception class is defined by
self-assessment ("if it requires extended context") with no objective criterion. Any beacon
author can self-classify as "extended context" and bypass the ≤2-line constraint.

Given that `pao-incident-2026-04-30T07-35Z-destructive-action-reset-hard.md` — a legitimate
extended-context beacon — is ~350 lines long, the ≤2-line rule functions more as a default
target than an enforceable constraint. The spec should clarify this honestly rather than
implying a hard limit that the spec itself overrides on the same page.

Separately, the rule "one concrete ask per beacon; compound asks should be split" is
operationally important but is stated as a should-rule. Given that beacon proliferation is
identified as a Negative consequence, the ask-splitting rule should be stated as a MUST for
the `question` type.

**Disposition:** Mechanical. Reframe the ≤2-line rule as a target: "Target: ≤2 lines,
≤120 characters each; extended-context beacons (incidents, multi-phase blocks) may exceed
this with justification in the frontmatter type." Upgrade the ask-splitting rule from
"should be split" to "MUST be split" for `type: question`.

---

## UPF v1.2 Stage 2 — 7 meta-validation checks

| Check | Result | Note |
|---|---|---|
| 1. Delegation strategy clarity | PASS | §4 Who writes what clearly assigns writing responsibility per sender; §5 Processing protocol assigns XO as reader/archiver. No ambiguity. |
| 2. Research needs identification | PASS | OQ-1 (routine sender activation) and OQ-3 (automated escalation) correctly defer empirical needs. |
| 3. Review gate placement | PASS | Implementation checklist uses observable/binary items (directory exists, INDEX updated). No vague gates. |
| 4. Anti-pattern scan | See below | |
| 5. Cold Start Test | CONDITIONAL PASS | §1–§7 is self-contained for a COB beacon. PAO beacon requires the worktree procedure in §4, which has the branch-cleanup gap (F3). A fresh PAO session following §4 would leave orphaned branches. Conditional on F3 resolution. |
| 6. Plan Hygiene Protocol | PASS | No zombie sections; revisit triggers are crisp (5 named conditions). |
| 7. Discovery Consolidation Check | MINOR GAP | ADR 0070 OQ-3 (PAO cross-repo worktree fragility) is not referenced in ADR 0072's open questions or revisit triggers; the prior discovery is therefore not consolidated. |

---

## UPF v1.2 Stage 2 — 21 Anti-Pattern Scan

| AP | Check | Result |
|---|---|---|
| AP-1 (Unvalidated assumptions) | "13 beacons" claim, PR citations | PASS — 13 beacons confirmed in `_archive/`; all 9 W#18 chain PRs verified merged |
| AP-2 (Vague phases) | Implementation checklist | PASS — 7 checklist items are binary and observable |
| AP-3 (Vague success criteria) | FAILED conditions | PASS — 3 kill triggers named (beacon volume >10; native IPC available; ADR 0070 superseded) |
| AP-4 (No rollback) | §"Pre-acceptance audit" | PASS — rollback = revert CLAUDE.md to prose-only guidance; no code changes to undo |
| AP-5 (Plan ending at deploy) | §Consequences | PASS — archive policy, pruning cadence, and escalation SLA extend well past "deploy" |
| AP-6 (Missing Resume Protocol) | §5 XO processing | PASS — escalation protocol for >7 day beacons is defined |
| AP-7 (Delegation without contracts) | §4 Who writes what | PASS — sender contracts are explicit |
| AP-8 (Blind delegation trust) | §7 Trust model | PASS — spam mitigation layers are enumerated; XO discretion is explicitly preserved |
| AP-9 (Skipping Stage 0) | Considered options | PASS — 2 alternatives considered and rejected with documented rationale |
| AP-10 (First idea unchallenged) | Option A/B/C analysis | PASS — Option C is not the first idea; chat-log failure was the starting observation |
| AP-11 (Zombie project) | Kill triggers | PASS — 3 functional kill triggers + 5 revisit conditions |
| AP-12 (Timeline fantasy) | No timelines asserted | PASS |
| AP-13 (Confidence without evidence) | "Proven in practice" claim | PASS — grounded in 13 verified beacons, 9 verified PRs |
| AP-14 (Wrong detail distribution) | §1–§7 specification depth | PASS — protocol detail is in §1–§7; background is in Context |
| AP-15 (Premature precision) | Schema specification | MINOR — §3 "3 keys exactly" rule is prematurely precise given archived beacon divergence (F2); ADR specifies a tighter schema than practice has established |
| AP-16 (Hallucinated effort estimates) | No effort estimates asserted | PASS |
| AP-17 (Delegation without context transfer) | §4 body schema | PASS — context block and unblock block in body schema provide context transfer per beacon |
| AP-18 (Unverifiable gates) | Implementation checklist | PASS — all items are directly verifiable (directory exists checks, INDEX diff) |
| AP-19 (Missing tool fallbacks) | §7 Trust model + spam mitigation | PASS — XO discretion and PR-gating are named fallbacks when naming convention is violated |
| AP-20 (Discovery amnesia) | ADR 0070 relationship | MINOR — OQ-3 from ADR 0070 not referenced; prior discovery partially lost (see Discovery Consolidation check above) |
| AP-21 (Assumed facts without sources) | PR citations, archive counts, ADR 0070 text | PARTIAL — 13 beacon count and 9 W#18 PRs are verified; the "flagged as future ADR candidate" claim in Context is ungrounded in ADR 0070's actual text (F1) |

**Anti-pattern summary:** 0 Critical hits. AP-15 and AP-20 are Minor; AP-21 is partial (F1
resolves it). All other anti-patterns pass.

---

## Verdict

**CONDITIONAL ACCEPT — fix F1, F2, F4, F5, F6 before merge; F3 before PAO beacon
usage; F7, F8 are style-level and may be applied inline.**

**Finding summary:**

| ID | Severity | Class | Description |
|---|---|---|---|
| F1 | Major | Non-mechanical | Context section misrepresents ADR 0070's beacon deferral posture |
| F2 | Major | Non-mechanical | Body schema "3 keys exactly" rule conflicts with archive reality; `workstream-or-chapter` key name diverges from all existing beacons |
| F3 | Major | Non-mechanical | PAO worktree procedure omits branch cleanup; dangling branches accumulate |
| F4 | Minor | Mechanical | "Three senders" with two-sender parenthetical; should be "two senders" |
| F5 | Minor | Mechanical | ADR 0070 reference still says "not yet merged; awaiting CO accept" — it is merged |
| F6 | Minor | Mechanical | W#31 unblock reference says "signaled COB" — direction is inverted; COB signaled XO |
| F7 | Minor | Mechanical | Slug "lowercase words" constraint doesn't cover alphanumeric codes (`w28`, `p5c4`) |
| F8 | Minor | Mechanical | ≤2-line prose rule is undercut by its own exception; ask-splitting SHOULD upgraded to MUST |

**Non-mechanical count:** 3 (F1, F2, F3). All require author judgment or procedural
additions, not copy-edit.

**Mechanical count:** 5 (F4–F8). All are straightforward inline corrections per Decision
Discipline Rule 3 auto-accept.

**Overall confidence:** HIGH that the protocol is sound and has demonstrated operational
value (13 beacons, multi-session unblocks). The findings do not challenge the core design;
they address specification accuracy (F1, F4, F5, F6), schema-to-practice alignment (F2),
and a procedural gap in the cross-repo worktree path (F3). None of the findings require
reconsidering Option C or the filesystem-as-IPC decision.

The ADR may proceed to Accepted status after author review of F1–F3 and application of
F4–F8. Council does not recommend a re-review pass for this tier given the process-tier
classification and the non-code-impact nature of all findings.
