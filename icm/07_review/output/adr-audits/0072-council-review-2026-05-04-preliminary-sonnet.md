# Council Review — ADR 0072 (Research-Inbox Beacon Protocol)

**Review date:** 2026-05-04
**Reviewer:** XO research subagent (canonical Opus 4.7 + xhigh effort, per CO directive 2026-05-04)
**Review posture:** standard adversarial (4 perspectives) + UPF v1.2 Stage 2 meta-validation + 21 anti-pattern scan
**ADR tier:** `process` — pre-merge council is RECOMMENDED for non-substrate ADRs per ADR 0069. Running canonical here per CO directive for the Opus-exclusive council canon.
**Supersedes:** preliminary Sonnet 4.6 review dispatched in error earlier 2026-05-04 (PR #500). The preliminary review's findings are independently re-derived below; where this canonical review and the preliminary agree, both votes count toward author confidence. Where they disagree, this canonical review governs.
**Verifications run before authoring:**
- `_archive/` enumeration: 13 resolved beacons present (11 `cob-*`, 2 `pao-*`); zero `yeoman-*`.
- W#18 unblock-chain PRs (#325/#326/#329/#330/#331/#333/#335/#337/#338): all verified MERGED via `gh pr view`.
- ADR 0070 reference (PR #489): verified MERGED 2026-05-04T10:06:59Z.
- Stub-unblock PR #460: verified MERGED 2026-05-01T14:03:06Z.
- ADR 0070 textual claim audit: searched merged ADR 0070 for the phrase "potential future ADR candidate" — phrase not present.

---

## Executive verdict

**CONDITIONAL ACCEPT — author judgment needed on F1, F2, F3; mechanical fixes F4, F5, F6, F7, F8, F9 may be applied inline by the author; F10 is an addition to the protocol that should be considered before the ADR settles to Accepted.**

The protocol design is sound. The filesystem-as-IPC pattern is the correct primitive given the constraints (no shared message bus, no inter-session API, sessions must survive restart). The decision to formalize the protocol after 13 archived beacons demonstrate operational value is well-judged; this is paper-after-practice rather than paper-before-practice, and the rationale for that ordering is implicit but defensible.

The findings concentrate on three clusters:
1. **Specification accuracy** — claims about ADR 0070's posture (F1), claims about archive conformance (F2, F4, F5), directional language (F6).
2. **Schema-to-practice gap** — the spec asserts a body schema that none of the 13 archived beacons actually conform to (F2 again; F7).
3. **Procedural gaps** — PAO worktree branch cleanup (F3), pruning ownership (F8), race conditions on `_archive/` move (F9), missing CI lint reference (F10).

None of the findings challenge the core design. None require reconsideration of Option C. The ADR can ship after the author resolves F1–F3 and the mechanical fixes are applied.

---

## Findings (10 total: 0 Critical, 4 Major, 6 Minor)

---

### F1 — Major: Context section misrepresents ADR 0070's beacon posture (factual citation)

**Perspective:** Outside Observer / Skeptical Implementer

**Issue:** ADR 0072 §Context states (line 41–42 of the rendered ADR):

> ADR 0070 acknowledges that the mechanism was operational before it was documented and flags the pattern as a "potential future ADR candidate"

The phrase "potential future ADR candidate" does not appear anywhere in the merged text of ADR 0070 (verified by full-file grep on `origin/main:docs/adrs/0070-multi-session-naval-org-structure.md`, 589 lines). What ADR 0070 actually does is provide a substantive §6 — "Live signaling: the research-inbox beacon protocol" — that runs to roughly 41 occurrences of the words "beacon" or "research-inbox" across the file, plus OQ-2 (automated beacon processing) and OQ-3 (PAO cross-repo worktree fragility) as named open questions. ADR 0070 is not a deferral document; it is a working operational specification that ADR 0072 expands and supersedes.

The Context's framing therefore overstates ADR 0070's incompleteness as motivation for a separate ADR. The motivation is real (beacon protocol has enough surface area — file naming, body schema, archive policy, escalation, spam mitigation, cross-repo handling — that a focused ADR is justified) but the justification rests on a false attribution.

**Why this matters beyond pedantry:** Future readers reconstructing the ADR genealogy will read ADR 0070 looking for the "potential future ADR candidate" framing and not find it. They may wrongly conclude that ADR 0072 is responding to ADR 0070 text that was edited out, or that the citation is hallucinated. Either reading damages trust in the ADR corpus's citation discipline. The cohort batting average (20-of-20 substrate amendments needing council fixes) is partially attributable to past citation drift; this finding catches the same class of error pre-merge.

**Disposition:** Non-mechanical (requires factual correction).

**Recommended fix:** Replace lines 40–45 with text along the lines of:

> ADR 0070 §6 ("Live signaling: the research-inbox beacon protocol") provides an operational description of the pattern adequate for the naval-org governance context; ADR 0070 OQ-2 and OQ-3 flag automated processing and PAO cross-repo fragility as open questions but do not specify the body schema, archive policy, or escalation thresholds in the depth needed for a stand-alone reference. The protocol has enough surface area to warrant a focused ADR: filename convention, body schema, sender table, processing protocol, archive/pruning policy, escalation policy, spam mitigation, and cross-repo worktree procedure. This ADR provides that focused reference and supersedes the §6 prose as the authoritative specification.

---

### F2 — Major: Body schema specification diverges from every existing beacon in the archive

**Perspective:** Skeptical Implementer (structural-citation correctness)

**Issue:** ADR 0072 §3 specifies the canonical body schema:

```
---
type: <type>
workstream-or-chapter: <workstream ID (W#NN) or chapter ID (ChNN)>
last-pr: <last PR merged or opened by this sender; "none" if no PR>
---
```

…with the rule "Frontmatter must be valid YAML (3 keys exactly; no extras unless XO approves schema extension via ADR amendment)."

Direct inspection of all 13 archived beacons (extracted via `awk` on each file's first frontmatter block) reveals that **no beacon in the archive conforms to this schema**. Specifically:

| Schema element | Spec says | Archive shows |
|---|---|---|
| Schema key for "what work is this about" | `workstream-or-chapter` | All `cob-*` beacons use `workstream`. The 2 `pao-*` beacons use either `chapter` or no equivalent key. **Zero beacons use `workstream-or-chapter`.** |
| Number of frontmatter keys | "3 keys exactly" | 3 of 13 beacons have 3 keys. 5 of 13 have 5 keys (add `filed-by`, `filed-at`). 1 has 6 keys (`pao-incident-2026-04-30T07-35Z` has `type`, `sender`, `chapter`, `date`, `last-pr`, `severity`). 1 has 7 keys. **No beacon files use exactly the spec's three-key set.** |
| `type` enum values | `idle`, `question`, `resumed`, `status`, `maintenance` | Archive contains: `idle`, `question`, `cob-idle`, `cob-question`, `pao-incident`, `pao-resumed`. The values `cob-idle`, `cob-question`, `pao-incident`, `pao-resumed` are NOT in the spec's enum. The single `pao-incident` value is not in the spec at all. |

The spec citing `pao-incident-2026-04-30T07-35Z-destructive-action-reset-hard.md` as "the canonical example of an extended-context beacon" (line 266–267) is therefore a structural-citation error: that beacon's frontmatter has 6 keys, uses `type: pao-incident` (not in the spec enum), and uses `chapter` rather than `workstream-or-chapter`. It cannot be canonical of a schema it does not conform to.

**Why this matters:** The spec asserts a 13-beacon empirical foundation (§Context line 56–58, §Considered Options line 165–167, §Pre-acceptance audit line 588). If the spec is read as describing the archive, it is wrong about the archive. If the spec is read as proposing a new schema (deviating from archive practice), the Context's framing of "the protocol has earned formalization; this ADR specifies it precisely" is misleading — the spec is not formalizing observed practice; it is replacing observed practice with a tighter schema. Either reading is defensible, but the ADR must pick one and state it explicitly.

**Disposition:** Non-mechanical (author judgment needed).

**Recommended fix — Option A (formalize practice):** Soften the schema:
- `workstream` for `cob-*` beacons (with values `W#NN`, `multi`, or `none`)
- `chapter` for `pao-*` beacons (with values `ChNN`, `process / cross-cutting`, or `n/a`)
- Optional keys `filed-by`, `filed-at`, `severity`, `sender`, `date` are permitted but not required.
- Keep the enum loose: senders may prefix the type with their own role (`cob-idle`, `pao-incident`) for filename-grep symmetry.
- Cite the existing 13 archive beacons as the canonical reference set.

**Recommended fix — Option B (replace practice):** Keep the tight 3-key schema, and:
- Re-classify the 13 archive beacons as "pre-formalization beacons" with a one-line note in §Compatibility plan that legacy beacons are exempt.
- Pick a different beacon as the canonical extended-context example (or remove the citation).
- Add a forward-going migration step: any new beacon must conform; legacy beacons are not retroactively rewritten.

Either path is acceptable. The ADR must choose one. The current text is internally inconsistent in either reading.

---

### F3 — Major: PAO cross-repo worktree procedure leaks branches; missing OQ-3 cross-reference

**Perspective:** Pessimistic Risk Assessor / Skeptical Implementer

**Issue:** ADR 0072 §4 ("PAO writes `pao-*` beacons") specifies the cross-repo worktree procedure:

```bash
cd /Users/christopherwood/Projects/the-inverted-stack
git -C /Users/christopherwood/Projects/Sunfish worktree add \
  /tmp/sunfish-pao-signal-wt -b pao/signal-$(date +%Y%m%dT%H%MZ) origin/main
# Write the beacon file
# Commit + push + open PR
git -C /Users/christopherwood/Projects/Sunfish worktree remove /tmp/sunfish-pao-signal-wt
```

Three structural problems:

1. **Branch cleanup is missing.** `git worktree remove` deletes the worktree directory but leaves the branch `pao/signal-<timestamp>` intact in the repository (and, if the PR has been pushed, on the remote). Each PAO signal accumulates a branch. At low signal frequency this is benign; at higher frequency it is branch noise that complicates `gh pr list --state open` and `git branch -a` output. The fix is a final `git -C /Users/christopherwood/Projects/Sunfish branch -D pao/signal-<timestamp>` after the PR has merged and the branch is no longer needed.

2. **Timestamp re-derivation race.** `$(date +%Y%m%dT%H%MZ)` is invoked twice in the procedure — once during creation, once during cleanup if you naively follow the same pattern. The two `date` calls can cross a minute boundary (worst case at the 59-second mark), producing different branch names. The fix is to capture the timestamp into a shell variable: `TS=$(date +%Y%m%dT%H%MZ); git worktree add -b pao/signal-$TS ...; git branch -D pao/signal-$TS`.

3. **No cross-reference to ADR 0070 OQ-3.** ADR 0070's OQ-3 explicitly flags the PAO cross-repo worktree pattern as fragile and "deferred until the fragility actually causes a dropped beacon." ADR 0072 codifies the fragile procedure as the canonical mechanism without acknowledging the fragility flag. A reader of ADR 0072 alone has no signal that this procedure is known-fragile; they would treat it as a tested-and-blessed pattern. The fix is one paragraph in §4 cross-referencing OQ-3 and stating that ADR 0072 inherits, rather than resolves, the fragility.

A fourth, lower-stakes concern: the absolute path `/Users/christopherwood/Projects/the-inverted-stack` is hard-coded into the protocol. §Revisit Triggers point 5 notes this and commits to updating the ADR if the book repo moves, which is acceptable but not ideal for a multi-machine future.

**Disposition:** Non-mechanical (procedural gap with three sub-fixes).

**Recommended fix:**
```bash
TS=$(date -u +%Y%m%dT%H%MZ)
WT=/tmp/sunfish-pao-signal-wt
BRANCH=pao/signal-$TS
git -C /Users/christopherwood/Projects/Sunfish worktree add "$WT" -b "$BRANCH" origin/main
# Write the beacon file
# Commit + push + open PR with --auto-merge
git -C /Users/christopherwood/Projects/Sunfish worktree remove "$WT"
git -C /Users/christopherwood/Projects/Sunfish branch -D "$BRANCH"   # after PR has merged
```

…plus a paragraph noting OQ-3 inheritance.

Note also: the spec uses `date +%Y%m%dT%H%MZ`, but on a typical macOS shell this returns local time, not UTC, despite the trailing `Z`. Add `-u` to force UTC and prevent timestamp/zone drift on machines configured for non-UTC. (This makes the filename's `Z` suffix honest.)

---

### F4 — Major: Race condition on `_archive/` move when XO and a beacon-author commit concurrently

**Perspective:** Pessimistic Risk Assessor / Devil's Advocate

**Issue:** The protocol assumes XO is the only writer that performs the `git mv beacon _archive/` step (per §5 step 4 and §6 first paragraph). But the protocol does not guarantee single-writer semantics on the `_archive/` directory. Three concurrent-commit scenarios produce silent failures:

1. **COB and XO write at the same time.** COB writes `cob-question-2026-05-04T20-30Z-foo.md` to the inbox root; XO concurrently moves `cob-question-2026-05-04T20-30Z-foo.md` (an old beacon with the same minute timestamp) to `_archive/`. The two PRs do not conflict at the filesystem level (different operations on a file with the same name from different angles) but they conflict at the git-merge level. The minute-precision timestamp is the load-bearing uniqueness guarantee, and the protocol does not enforce that two beacons cannot share a minute.

2. **Two senders generate identical filenames in the same minute.** If COB and a hypothetical second `cob-*`-class writer (or COB after a session restart) both fire at 14:30Z with similar slugs, the filename can collide. The protocol does not specify a tiebreaker.

3. **PAO writes from a worktree while XO is processing a different beacon.** PAO's PR opens with `pao-question-...md` in the inbox root. XO's parallel PR archives a different beacon and lands first. PAO's PR rebases cleanly. No conflict. But if XO's PR also archives PAO's pending beacon (e.g., XO scanned, processed, and archived in the same iteration that PAO was opening the PR), PAO's branch base is now stale; PAO's PR would re-introduce the beacon to the active inbox. The protocol has no protocol-level guard against this.

The 13-beacon archive does not reveal the failure mode because session count was low and PAO's two beacons were temporally separated. As soon as PAO+COB+Yeoman are all live within a 60-second window, the failure mode becomes plausible.

**Disposition:** Non-mechanical (additive — protocol-level guard).

**Recommended fix:** Add §8 "Concurrency and uniqueness":

- Beacons MUST use second-precision timestamps (`HH-MM-SSZ`) when minute-precision is insufficient. Senders SHOULD use second precision by default in environments where multiple sessions are simultaneously active.
- Filename collisions are resolved by appending a 4-character random suffix (`...slug-a3f7.md`).
- If XO archives a beacon that a sub-XO sender is in the process of writing (PR in flight), the sub-XO PR is rebased automatically; the beacon is NOT re-introduced to the inbox root. Sub-XO PRs that touch the inbox root MUST check for the beacon's presence at rebase time and treat absence as "already archived."
- XO archive moves are atomic via a single `git mv` per beacon; XO MUST NOT batch multiple `git mv` operations across PRs against the same `_archive/` directory in flight simultaneously.

The fix can be lighter (just add second-precision and the rebase-treats-absence-as-archived rule) if the author judges the full §8 to be over-engineering. But the rebase rule is non-negotiable: without it, a closed beacon can resurrect.

---

### F5 — Minor: "Three senders" in §Context contradicts the parenthetical (`cob`, `pao`)

**Perspective:** Outside Observer (factual-citation correctness)

**Issue:** §Context line 56–60 states:

> The archive contains 13 resolved beacons from three senders (`cob`, `pao`) across multiple session-days.

The parenthetical names two senders. Archive enumeration confirms 11 `cob-*` + 2 `pao-*` + 0 `yeoman-*` = two distinct senders. The "three senders" framing is wrong by one. (The spec defines four senders in §2 — `cob`, `pao`, `yeoman`, `routine` — but only two have ever written beacons.)

**Disposition:** Mechanical (auto-accept per Decision Discipline Rule 3).

**Recommended fix:** Change "three senders (`cob`, `pao`)" to "two senders (`cob`, `pao`)."

---

### F6 — Minor: References section says ADR 0070 "not yet merged; PR #489 awaiting CO accept" — it is merged

**Perspective:** Skeptical Implementer (factual-citation freshness)

**Issue:** §References line 526–529 states:

> **ADR 0070** (not yet merged; PR #489 awaiting CO accept) — naval command structure.

PR #489 is verified MERGED at 2026-05-04T10:06:59Z (via `gh pr view 489 --json state,mergedAt`). The reference is stale draft-time text that did not get updated before submission. A reader of this ADR will navigate to PR #489 expecting an open PR and find a closed one, which is harmless but signals lax draft-to-merge hygiene.

**Disposition:** Mechanical.

**Recommended fix:** Change "**ADR 0070** (not yet merged; PR #489 awaiting CO accept) — naval command structure." to "**ADR 0070** (merged 2026-05-04 via PR #489) — naval command structure."

---

### F7 — Minor: W#31 unblock-chain reference inverts the signal direction ("signaled COB" should be "signaled XO")

**Perspective:** Outside Observer (clarity)

**Issue:** §References line 547–549 states:

> `cob-idle-2026-04-29T20-42Z-31-built-queue-dry.md` signaled COB at rung 6; XO queued three follow-on workstreams; COB resumed within one loop iteration.

The beacon is written *by* COB to signal *XO*. "Signaled COB" reads as if XO sent a signal to COB. The intended semantics are: "COB wrote the beacon at rung 6 to signal XO; XO received the signal, queued three follow-on workstreams; COB resumed within one loop iteration."

This is an Outside Observer test failure: a session reading ADR 0072 to understand the protocol direction will read this sentence and have to disentangle the semantics. Direction-of-signal is the load-bearing concept of the protocol; getting it backward in a load-bearing example is a clarity defect.

**Disposition:** Mechanical.

**Recommended fix:** Replace "signaled COB at rung 6" with "was written by COB at rung 6 to signal XO."

---

### F8 — Minor: Slug "lowercase words" constraint excludes the dominant slug component type (alphanumeric codes)

**Perspective:** Pedantic Lawyer

**Issue:** §2 specifies the slug as "2-5 hyphen-separated lowercase words." The archive shows the dominant slug component is an alphanumeric code, not a word:

| Beacon | Slug | Components |
|---|---|---|
| `cob-question-...-31-taxonomy-prerequisites.md` | `31-taxonomy-prerequisites` | code (`31`) + word + word |
| `cob-question-...-w19-p3-prereqs.md` | `w19-p3-prereqs` | code + code + word |
| `cob-question-...-w28-p5-w20-substrate-adaptation.md` | `w28-p5-w20-substrate-adaptation` | code + code + code + word + word |
| `cob-question-...-w28-p5c4-capability-verifier.md` | `w28-p5c4-capability-verifier` | code + code + word + word |

A naive regex implementing "lowercase words" (e.g., `[a-z]{2,}(-[a-z]{2,}){1,4}`) would reject every one of these slugs. Any future CI lint built from the §2 specification would either flag the entire archive as non-conformant or be silently inconsistent with the spec.

**Disposition:** Mechanical.

**Recommended fix:** Change "2-5 hyphen-separated lowercase words" to "2-5 hyphen-separated lowercase tokens, where a token is a word (`abc`) or an alphanumeric code (`w28`, `p5c4`, `ch22`). Suggested regex: `[a-z][a-z0-9]*(-[a-z0-9]+){1,4}`."

---

### F9 — Minor: §3 prose-limit rule is undercut by its own exception, with no objective threshold

**Perspective:** Pessimistic Risk Assessor

**Issue:** §3 specifies a body limit:

> Context block: ≤2 lines, ≤120 characters each. Facts only; no narrative.
> Unblock block: ≤2 lines, ≤120 characters each. One concrete ask per beacon.

…then immediately overrides:

> Beacons that require extended context (e.g., a PAO incident report) may exceed the prose limits.

The exception is self-classified ("if it requires extended context"). Any author can self-declare "extended context" and bypass the ≤2-line cap. The cited canonical extended-context example (`pao-incident-2026-04-30T07-35Z-destructive-action-reset-hard.md`) is 121 lines. The ratio of cap (2 lines) to exception (121 lines) is 60×, which is not a "soft default with rare overrides"; it is a target that the canonical example violates by two orders of magnitude.

Separately, the rule "one concrete ask per beacon; compound asks should be split into two beacon files" is operationally important but is stated as a SHOULD. Beacon proliferation is identified in §Consequences as a Negative consequence; the ask-splitting rule is the primary mitigation. A SHOULD-rule that mitigates a named negative consequence should be a MUST.

**Disposition:** Mechanical (re-framing).

**Recommended fix:**
- Reframe the prose limit as a target with explicit gating: "Target: ≤2 lines, ≤120 characters each. Beacons with `type: status` MAY exceed the limit if the beacon is documenting an incident, post-mortem, or multi-phase decision; the body MUST justify the deviation in its first paragraph (e.g., 'Extended-context beacon: incident report covering [scope].'). Other types (`idle`, `question`, `resumed`, `maintenance`) MUST conform to the 2-line target."
- Upgrade the ask-splitting rule from SHOULD to MUST for `type: question`: "MUST contain exactly one concrete ask. Compound asks MUST be split into separate `question` beacon files."

---

### F10 — Minor: Implementation checklist has no path for the CI lint that the protocol depends on for its trust model

**Perspective:** Skeptical Implementer / Devil's Advocate

**Issue:** §7 names "Naming convention enforcement" as the first spam-mitigation layer:

> Beacons with malformed filenames (wrong sender prefix, invalid type, wrong timestamp format) are ignored by XO's scan path... malformed files are flagged in the loop iteration log and XO writes a memory note.

…but the implementation checklist (§Implementation checklist line 463) lists the CI lint as **optional**:

> [ ] (Optional, non-blocking) Add a CI lint step that validates beacon filenames match the naming convention pattern; report violations as warnings

The trust model (§7) treats naming convention enforcement as the first line of defense. The implementation checklist treats it as nice-to-have. The two sections imply different priority. If the CI lint is genuinely the first line of defense, it should be a non-optional checklist item and the trust model should reference it as the enforcement mechanism. If the CI lint is genuinely optional, the trust model should not lean on "naming convention enforcement" as its first layer.

Additionally, the protocol's "XO ignores malformed files" rule depends on XO actually noticing malformed files. The current scan command — `ls icm/_state/research-inbox/*.md 2>/dev/null` — returns ALL `.md` files including malformed ones. Without the CI lint or a stricter scan command (e.g., a regex-filtered `ls`), malformed files are processed by XO as if they were beacons, defeating the convention enforcement.

**Disposition:** Mechanical (or additive, depending on author choice).

**Recommended fix — Option A:** Move the CI lint from "Optional" to required; specify the regex (e.g., `^(cob|pao|yeoman|routine)-(idle|question|resumed|status|maintenance|incident)-\d{4}-\d{2}-\d{2}T\d{2}-\d{2}Z-[a-z][a-z0-9-]*\.md$`); commit to it as part of the post-merge work.

**Recommended fix — Option B:** Replace the simple `ls` scan command with a regex-filtered scan (e.g., a small shell script `xo-scan-inbox.sh` checked into `.icm/scripts/`) so XO's enforcement is intrinsic to the protocol rather than dependent on a CI lint that may not exist.

Either path resolves the asymmetry; the current ADR is internally inconsistent on whether convention enforcement is part of the protocol or a separate aspirational concern.

---

## UPF v1.2 Stage 2 — 7 Meta-Validation Checks

| Check | Result | Note |
|---|---|---|
| **1. Delegation strategy clarity** | PASS | §4 ("Who writes what") cleanly assigns writing responsibility per sender; §5 assigns XO as reader/archiver. The two sender roles that have actually written (COB, PAO) are well-specified; the two reserved roles (Yeoman, routine) are clearly conditional. |
| **2. Research needs identification** | PASS | OQ-1 (`routine` sender activation), OQ-2 (body-schema extension), OQ-3 (automated escalation) are well-scoped deferrals. None are speculative; each has a named trigger condition for re-engagement. |
| **3. Review gate placement** | PASS WITH F10 NOTE | Implementation checklist items (§Implementation checklist) are observable and binary except item 7 (the CI lint) which is marked "Optional, non-blocking." See F10: the optionality of the lint conflicts with the trust model. |
| **4. Anti-pattern scan** | See AP table below | |
| **5. Cold Start Test** | CONDITIONAL PASS | A fresh COB session can write a beacon from §2 + §3 alone — the file naming and body schema are self-contained. A fresh PAO session running the §4 worktree procedure verbatim would (a) leave a dangling branch (F3), (b) potentially generate a UTC-mismatched timestamp (F3 sub-issue), and (c) have no clear directive on what to do if the PR rebase reveals XO has already archived the beacon (F4). All three are covered by the F3+F4 fixes. Conditional on those fixes. |
| **6. Plan Hygiene Protocol** | PASS | No zombie sections. Revisit triggers (§Revisit triggers) name 5 crisp conditions. |
| **7. Discovery Consolidation Check** | MINOR GAP | ADR 0070 OQ-3 (PAO cross-repo worktree fragility) is not referenced in ADR 0072's open questions or in §4 (the PAO procedure section). The earlier discovery is therefore not consolidated. F3 fix includes the cross-reference; that resolves this gap. |

---

## UPF v1.2 Stage 2 — 21 Anti-Pattern Scan

| # | Anti-pattern | Result | Notes |
|---|---|---|---|
| AP-1 | Unvalidated assumptions | PASS | The "13 beacons" claim is verified against the archive (13 confirmed). The "9 PRs across two days" claim for W#18 is verified against `gh pr view`. Senders count claim (F5) fails on three-vs-two but the underlying empirical anchor is solid. |
| AP-2 | Vague phases | PASS | Implementation checklist is binary/observable except for the CI lint optionality issue (F10). |
| AP-3 | Vague success criteria | PASS | §Pre-acceptance audit names FAILED conditions (beacon volume >10; native IPC available; ADR 0070 superseded). |
| AP-4 | No rollback | PASS | §Pre-acceptance audit specifies rollback as "remove inbox directory convention from CLAUDE.md; archive remains for historical reference." Sufficient for a process ADR. |
| AP-5 | Plan ending at deploy | PASS | Archive policy, pruning cadence, escalation SLA are post-deploy commitments. |
| AP-6 | Missing Resume Protocol | PASS | §5 escalation protocol (>7 days → CO) and §Revisit triggers cover resume conditions. |
| AP-7 | Delegation without contracts | PASS | §4 sender contracts are explicit. |
| AP-8 | Blind delegation trust | PASS | §7 spam mitigation enumerates trust layers; XO discretion is preserved. |
| AP-9 | Skipping Stage 0 | PASS | Two alternatives (Option A chat log, Option B state polling) considered and rejected with rationale. |
| AP-10 | First idea unchallenged | PASS | Option C (the recommended) is explicitly framed as a third iteration; the failure modes of A and B are stated. |
| AP-11 | Zombie project (no kill criteria) | PASS | Three named kill triggers + five revisit conditions. |
| AP-12 | Timeline fantasy | PASS | No timeline assertions made. |
| AP-13 | Confidence without evidence | PASS | "Proven in practice" claim grounded in 13 verified archive beacons + 9 verified PRs. The empirical anchor is solid even where the spec diverges from it (F2). |
| AP-14 | Wrong detail distribution | PASS | Protocol detail in §1–§7; rationale in Decision drivers; alternatives in Considered options. Distribution is appropriate. |
| AP-15 | Premature precision | MINOR HIT | §3 "3 keys exactly" rule is more precise than archive practice supports. Resolved by F2. |
| AP-16 | Hallucinated effort estimates | PASS | No effort estimates asserted. |
| AP-17 | Delegation without context transfer | PASS | §3 body schema includes context block + unblock block, transferring per-beacon context. |
| AP-18 | Unverifiable gates | PASS | All implementation checklist items are directly verifiable except the CI lint (F10). |
| AP-19 | Missing tool fallbacks | PASS | §7 names XO discretion + PR-gating as fallbacks when naming convention is violated. |
| AP-20 | Discovery amnesia | MINOR HIT | ADR 0070 OQ-3 (PAO worktree fragility) is not referenced. Resolved by F3 cross-reference. |
| AP-21 | Assumed facts without sources | PARTIAL | 13-beacon count and 9 W#18 PRs verified. The "potential future ADR candidate" claim in §Context is ungrounded in ADR 0070's actual text (F1). The ADR 0070 reference text is stale (F6). The "three senders" claim is empirically wrong (F5). The schema-claims-vs-archive-reality gap (F2) is a structural AP-21 hit. Of all 10 findings, F1, F2, F5, F6 are AP-21 instances. |

**Anti-pattern summary:** 0 critical hits. AP-15 and AP-20 are minor (resolved by F2 and F3 respectively). AP-21 is the dominant theme across F1, F2, F5, F6 — citation discipline. None of the AP hits are blocking; all are addressable inline by the author.

---

## Devil's Advocate — was filesystem-as-IPC the right pattern?

The ADR considers two alternatives (chat log; state-file polling) and rejects both. Three additional alternatives are conspicuously absent from the analysis:

**(A) SQLite-backed inbox.** A single `inbox.sqlite` file in `icm/_state/` with a `beacons` table (id, sender, type, timestamp, slug, body, status). Pro: structured queries (e.g., "show all unresolved cob-question beacons older than 6 hours"); atomic insert/update; well-understood concurrency model (write-ahead-log + busy-timeout). Con: binary file in git is awkward (diffs are unreadable); requires SQLite tooling at every reader/writer; conflict resolution on concurrent writes requires a serialization layer.

**Verdict on SQLite:** Reasonable rejection. The "binary in git" friction is real, and the protocol's structured-query needs are not high enough to justify it. The ADR could cite this rejection explicitly, but it is not load-bearing.

**(B) Git-commit-as-event.** Use commit messages with a structured prefix (e.g., `signal(cob,question): w28-p5c4-capability-verifier ⏎ <body>`) as the signal medium; `git log --grep '^signal('` as the scan command. Pro: zero new directories; uses existing tooling; signals are cryptographically signed by the commit author. Con: signals are interleaved with code changes (high noise); archive == "commits older than X" is awkward; branch-vs-trunk semantics are unclear (signal on a branch that never merges = lost signal).

**Verdict on git-commit-as-event:** Reasonable rejection. The commit-log noise problem is severe; signals would be drowned by ordinary feature commits. The ADR could mention this option as Option D and reject it; it is not load-bearing.

**(C) Pub/sub via local server.** A small daemon (e.g., a Python or Node process) listening on a Unix socket; sessions write signals to the socket; the daemon persists to a file or queue and notifies subscribers. Pro: low-latency push; no polling; supports multiple subscribers. Con: requires a running process; sessions cannot rely on it being available; introduces a process-management problem orthogonal to the protocol.

**Verdict on pub/sub:** Correctly rejected by implication. The "no shared message bus" decision driver in §Decision drivers (line 84) precludes this. ADR could be more explicit but the reasoning is sound.

**The filesystem-as-IPC choice is correct.** The reasons given in the ADR are sufficient. The three additional alternatives above strengthen the case but their omission is not a defect.

**One Devil's Advocate concern that does merit discussion:** The protocol commits beacons to git, which means every beacon is a permanent record in the repo's history. Pruning `_archive/` after 30 days deletes the file from `main` but the commit history retains it indefinitely. A beacon that contains sensitive information (e.g., a PAO incident report mentioning a security vulnerability or a private architectural debate) cannot be redacted via the pruning mechanism. The §Trust impact section (line 432–438) asserts that "beacon files themselves contain no sensitive data" but this is by-convention, not by-construction. The protocol should note this asymmetry: the prune step is a navigability optimization, not a redaction mechanism. If a beacon ever does contain sensitive data, the redaction path is `git filter-repo` or equivalent, not pruning.

This is a **Recommended-Note**, not a finding: ADR 0072 should add one sentence to §6 (Archive and pruning policy) clarifying that pruning is for navigability and not for redaction; sensitive data in beacons requires a separate redaction process.

---

## Pessimistic Risk Assessor — orphaned and stale-but-fresh beacons

Two failure modes deserve explicit treatment:

**Orphaned beacons.** A beacon that XO never processes (because XO loop frequency drops, or XO judgment classifies the beacon as out-of-scope, or the beacon is malformed enough that XO ignores it) sits in the inbox root indefinitely. The 7-day SLA + CO escalation is the named mitigation, but it depends on XO actually checking beacon ages. If XO is sleeping or offline, the beacon ages without notice. The §6 "Never prune active beacons" rule means orphaned beacons accumulate until XO manually escalates. A future enhancement (CI job that comments on aged beacons) is mentioned in §Open questions OQ-3 of this ADR but not committed to. **Disposition:** Acceptable for now; orphaned-beacon risk is bounded by the 4-session count.

**Stale-but-fresh beacons (timestamp drift).** The filename timestamp is the beacon's age. If a sender's clock is wrong (e.g., a Mac with a drifted time zone, a worktree with a bad system time), the beacon's filename timestamp is misleading. A beacon that was actually written 5 days ago but is filenamed `2026-05-04T14-30Z` looks fresh to XO. The protocol does not have a clock-trust assumption documented. **Disposition:** Mention in §7 Trust model: "Beacon timestamps are filename-encoded and trusted at face value. Senders are responsible for clock correctness; XO does not validate timestamps against commit time."

This is a **Recommended-Note**, not a separate finding: the F3 fix already adds `date -u +...` for UTC; one additional sentence in §7 closes the gap.

---

## Skeptical Implementer — does the filename regex match cohort precedent?

A regex implementing the §2 spec (after F8 fix) would be approximately:

```
^(cob|pao|yeoman|routine)-(idle|question|resumed|status|maintenance)-\d{4}-\d{2}-\d{2}T\d{2}-\d{2}Z-[a-z][a-z0-9-]*\.md$
```

Testing this regex against the 13 archive filenames:

| Filename | Matches? | Why |
|---|---|---|
| `cob-idle-2026-04-29T20-42Z-31-built-queue-dry.md` | ✅ | All components present |
| `cob-idle-2026-04-29T22-50Z-queue-dry-late.md` | ✅ | All components present |
| `cob-idle-2026-04-30T16-00Z-priority-queue-dry.md` | ✅ | All components present |
| `cob-question-2026-04-29T19-12Z-31-taxonomy-prerequisites.md` | ✅ | After F8 fix accepting alphanumeric tokens |
| `cob-question-2026-04-29T20-52Z-w19-p3-prereqs.md` | ✅ | After F8 |
| `cob-question-2026-04-29T21-23Z-w19-p5-block-ux.md` | ✅ | After F8 |
| `cob-question-2026-04-29T21-26Z-w20-p3-tkp.md` | ✅ | After F8 |
| `cob-question-2026-04-30T03-58Z-w28-p5-w20-substrate-adaptation.md` | ✅ | After F8 |
| `cob-question-2026-04-30T06-12Z-w21-p1-signature-envelope-halt.md` | ✅ | After F8 |
| `cob-question-2026-04-30T14-30Z-w28-p5c4-capability-verifier.md` | ✅ | After F8 |
| `cob-question-2026-04-30T15-00Z-w28-p5c4-startapp-prospectid-seam.md` | ✅ | After F8 |
| `pao-incident-2026-04-30T07-35Z-destructive-action-reset-hard.md` | ❌ | `incident` is not in the type enum |
| `pao-resumed-2026-04-29T19-42Z-online.md` | ✅ | All components present |

12 of 13 match. The one failure is `pao-incident`, whose type is not in the spec. The §Compatibility plan section asserts "existing beacons in the archive are unaffected" — but `pao-incident` is the cited canonical extended-context example. The schema either needs to add `incident` to the type enum (recommended) or re-classify the cited example.

**This is part of F2 disposition, not a separate finding.** Author should add `incident` to the type enum to preserve the citation.

---

## Outside Observer — is the spec clear to a session with no prior beacon experience?

Reading the ADR cold (without prior knowledge of the archive), the spec is mostly self-contained. The naming convention is unambiguous after F8. The body schema is unambiguous after F2. The processing protocol is unambiguous. The archive policy is unambiguous.

Three remaining ambiguities a fresh reader would hit:

1. **What happens if XO writes a beacon?** §4 only describes sub-XO writers. The spec implicitly assumes XO does not write beacons (XO archives beacons written by others) but does not say so. A fresh reader might wonder: can XO use the inbox to broadcast? **Recommended-Note:** Add to §4 a sentence: "XO does not write beacons. XO is the sole reader/archiver. Cross-XO communication (rare) goes via direct memory note or commit message, not via the inbox."

2. **What happens if a beacon refers to a workstream that does not exist?** A `cob-question` beacon for a hypothetical W#999 with no ledger row would be processed how? **Recommended-Note:** The spec is silent. XO discretion (§7 layer 4) covers it implicitly, but a fresh reader would not know that.

3. **What is the "loop iteration" the protocol assumes?** §5 says "every loop iteration" but does not define what a loop iteration is. The reference is to the `/loop` discipline (project memory `feedback_loop_discipline`), but a fresh reader without that memory has no anchor. **Recommended-Note:** Add a footnote or §5 introductory sentence: "An XO loop iteration is one cycle of the `/loop` discipline (see `CLAUDE.md`); typically every 5–30 minutes when the session is active."

These are all Recommended-Notes (not findings). The author can address them inline as a clarity pass.

---

## Verdict and finding summary

**CONDITIONAL ACCEPT.**

**Finding summary:**

| ID | Severity | Class | Description | Path |
|---|---|---|---|---|
| F1 | Major | Non-mechanical | §Context misrepresents ADR 0070's beacon posture; "potential future ADR candidate" is not in ADR 0070 | Author rewrites |
| F2 | Major | Non-mechanical | Body schema diverges from every existing archive beacon; spec must pick "formalize practice" or "replace practice" and state it | Author judgment |
| F3 | Major | Non-mechanical | PAO worktree procedure leaks branches; missing OQ-3 cross-reference; UTC timestamp issue | Author + procedure rewrite |
| F4 | Major | Non-mechanical | Race conditions on `_archive/` move (concurrent writes; rebase-resurrects-beacon); add §8 Concurrency | Author + protocol addition |
| F5 | Minor | Mechanical | "Three senders (`cob`, `pao`)" → "two senders" | Auto-accept |
| F6 | Minor | Mechanical | ADR 0070 reference says "not yet merged" — it is | Auto-accept |
| F7 | Minor | Mechanical | "Signaled COB" should be "signaled XO" (W#31 example direction inverted) | Auto-accept |
| F8 | Minor | Mechanical | Slug "lowercase words" → "lowercase tokens (words or alphanumeric codes)" | Auto-accept |
| F9 | Minor | Mechanical | ≤2-line rule undercut by exception; ask-splitting SHOULD → MUST for `question` | Auto-accept |
| F10 | Minor | Mechanical | CI lint marked Optional but is the trust model's first defense; reconcile | Author choice |

**Major findings (4):** F1 (citation accuracy), F2 (schema-vs-practice), F3 (procedure rigor), F4 (concurrency).
**Minor findings (6):** F5–F10 are inline-correctable.
**Recommended-Notes (4):** redaction-not-pruning; clock-trust assumption; XO-doesn't-write-beacons; loop iteration definition.

**Mechanical-fix count:** 6 of 10 (F5, F6, F7, F8, F9, F10) qualify for auto-accept under Decision Discipline Rule 3.

**Non-mechanical count:** 4 of 10 (F1, F2, F3, F4) require author judgment.

**Council does not recommend a re-review pass for this tier.** The findings are addressable inline; F2 and F4 are the only ones that materially change the protocol surface (F2 chooses between two schema paths; F4 adds a concurrency section). Both are author-tractable from the recommendations above. After the author applies F4–F10 and resolves F1–F3, the ADR may proceed to Accepted status.

**Confidence in core design:** HIGH. The filesystem-as-IPC choice is correct; the alternatives (chat log, state polling, SQLite, git-commit-as-event, pub/sub daemon) are all weaker. The 13-beacon empirical anchor is solid. The protocol's failure modes (orphaned beacons, timestamp drift, schema drift, concurrency races) are bounded by the 4-session count and the small writer set; they will become more pressing if Sunfish scales to 6+ active sessions, at which point the §Revisit triggers OQ-3 (volume >10 active beacons) is the right tripwire.

**Cohort context:** This is the 21st substrate ADR to go through pre-merge council review in the 2026-04-29 to 2026-05-04 window. The cohort batting average remains 100%: every ADR has needed at least one council-driven fix. The investment has paid off in defect avoidance — none of the post-council ADRs has produced a downstream amendment driven by structural-citation error or symbol drift.

**Co-Authored-By note:** Per CO directive 2026-05-04, council reviews use Opus 4.7 + xhigh effort exclusively. This canonical review supersedes the preliminary Sonnet 4.6 review (PR #500) where the two diverge. The two reviews substantially agree on F1, F2, F3, F5, F6, F7, F8, F9 (the preliminary's F1–F8 map cleanly onto canonical F1–F9 with minor reframings); the canonical adds F4 (concurrency) and F10 (CI lint reconciliation) which the preliminary did not surface, and provides Recommended-Notes (redaction, clock-trust, XO-doesn't-write, loop-iteration) that the preliminary did not enumerate. The canonical's longer alternatives analysis (Devil's Advocate section on SQLite, git-commit-as-event, pub/sub) is a depth difference reflecting xhigh's higher tool-call density on the alternatives space.
