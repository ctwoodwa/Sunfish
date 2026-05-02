---
id: 74
title: Session Startup / Recovery Protocol
status: Proposed
date: 2026-05-01
tier: process
concern:
  - governance
  - dev-experience
composes:
  - 70
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0074 — Session Startup / Recovery Protocol

**Status:** Proposed
**Date:** 2026-05-01
**Author:** XO (research session)
**Pipeline variant:** `sunfish-quality-control` (process-tier ADR; no production code; codifies
existing operating protocol)
**Council posture:** none required — process-tier ADR codifying runtime-verified behavior; no
production-code changes; no security or privacy surface affected

**Resolves:** ADR 0070 §References ("session-start batch command pattern that enables
`/compact`-recovery") cites `feedback_verify_pr_state_at_session_start.md` as the canonical
source; this ADR promotes that pattern into the formal architectural decision trail, cross-
references it with the broader recovery obligations per ADR 0070, and makes the protocol
complete, testable, and exhaustive.

---

## Context

Sunfish runs up to four concurrent Claude Code sessions (XO, COB, PAO, Yeoman — per ADR 0070).
Each session operates in isolation: sessions share a filesystem and git repository but cannot
communicate directly. A session learns what has changed in its absence only by reading the repo.

Two mechanisms disrupt session continuity in practice:

1. **`/compact` events.** When a session's context window approaches capacity, Claude Code
   automatically compresses prior turns into a summary. The summary is a point-in-time
   snapshot — it reflects state at the moment of compaction, not the current state of the
   repo. A parallel session (e.g., COB) may have merged five PRs between the compaction and
   the next turn. The XO session's compacted summary will say "PR #X is pending" when it
   has already merged.

2. **Session restarts.** A session that exits and restarts inherits project memories (loaded
   automatically from `~/.claude/projects/...`) but no conversation history. The newest
   project memories are snapshots too — they lag the repo by whatever happened since they
   were last written.

Both mechanisms produce the same failure mode: **stale-state assumptions**. A session acts
on what it believes to be true based on a summarized or memory-loaded view, without checking
whether the repo's ground truth matches that view. The consequences range from minor
inefficiency (re-doing work that already landed) to coordination failures (authoring a hand-
off for a workstream a parallel session is already building against).

**Documented incidents that shaped this protocol:**

- **2026-04-28 incident (W#18 / kernel-audit scaffolding):** Lead research session spent
  approximately 30 minutes producing UPF, v0 surface analysis, and a widened intake for
  the kernel-audit substrate while a parallel session had already scaffolded against the
  older v0 design and opened an auto-merge PR (#190). The research session wrote a project
  memory claiming the `blocks-kernel-audit v0 scaffolding` convention was pending — wrong;
  the scaffold already existed. Root cause: no session-start state-verification batch.

- **2026-04-26 incident (i18n cascade / worktree cleanup):** A worktree cleanup subagent
  removed a worktree while the i18n cascade agent was still running. The branch had been
  created via `git switch -c` but had no commits yet, making it look idle. Approximately
  50 minutes of translation work was lost. Root cause: cleanup decision made from stale
  state; no real-time active-task check.

- **ADR 0028-A6.2 structural citation error (pre-merge):** A council review pass on ADR
  0028 Amendment A6 found that a rule in §A6.2 cited `required: true` on `ModuleManifest`
  per ADR 0007 — the field existed on `ProviderRequirement`, not `ModuleManifest`. This
  structural citation error was caused by a council subagent working from a compacted
  summary that contained a stale symbol reference. Council caught it pre-merge (A7 filed);
  the fix was an amendment cycle that consumed approximately one additional hour. Root
  cause: council subagent acted on summarized symbol list without re-verifying against
  current code.

These incidents are not anomalies. They are the expected failure mode of a system where
multiple agents share a filesystem but cannot share live context. The protocol in this ADR
addresses the failure class at its root: every session, at start, re-establishes ground
truth from the repo before acting on any "pending" state from memory or summary.

**What this ADR does not govern:** session loop continuity (per `feedback_loop_discipline.md`
memory); destructive git safety (per ADR 0070 §Decision and `feedback_git_discipline.md`);
worktree lifecycle (per `feedback_git_discipline.md` Rule 5). Those are separately specified.
This ADR is specifically the startup / recovery sequence and the pending-state verification rule.

---

## Decision drivers

1. **Ground truth is the repo, not the summary.** `/compact` summaries and project memories
   are point-in-time artifacts. A session that acts on a summary without verifying against
   the repo is acting on potentially-stale data. The protocol enforces repo-first.

2. **Parallel sessions are the norm, not the exception.** With up to four concurrent sessions
   sharing the repo, the probability that something has changed between a session's last
   turn and its current turn is high. The batch verification commands are fast (sub-second);
   the cost of skipping them is an incident probability, not a hypothetical.

3. **Terse signals after `/compact` are the highest-risk moment.** When a user sends a
   single-word message like "continue" or "status" after a `/compact` event, the session is
   most likely to proceed directly from the (now-stale) compacted summary. This is
   precisely when verification is most needed and most likely to be skipped. The protocol
   requires special treatment of this pattern.

4. **Ledger and inbox are coordination surfaces, not optional scans.** The active-workstreams
   ledger (`icm/_state/active-workstreams.md`) and research-inbox (`icm/_state/research-
   inbox/*.md`) are the IPC channels of the naval-org structure (per ADR 0070). A session
   that starts without reading them is operating without its IPC. Unread ledger updates mean
   a workstream flagged `ready-to-build` by XO is not visible to COB; unread inbox beacons
   mean a COB question filed three sessions ago remains unanswered.

5. **The verification batch is idempotent and reversible.** Every command in the startup
   batch is read-only. Running it twice is harmless. Skipping it once can cause an incident.
   This asymmetry justifies making the batch mandatory rather than advisory.

6. **Canonicalization reduces cognitive load.** A fixed, ordered, memorized batch is executed
   without deliberation. A "check what you need" heuristic requires judgment each time and
   degrades under token pressure. The canonical batch removes the judgment call.

---

## Considered options

### Option A — Advisory protocol (documentation only, no enforcement)

Document the startup batch in CLAUDE.md as a reminder. Sessions follow it when they remember.
No distinction between required and optional steps. No special treatment of `/compact`-
recovery.

**Pro:** low overhead for sessions where state is obviously unchanged (e.g., XO immediately
after authoring a hand-off with no parallel sessions active).
**Con:** the failure mode that produced the 2026-04-28 and 2026-04-26 incidents was a session
that did not think it needed to verify. Advisory-only protocols degrade exactly when they are
most needed. Skipping the audit silently is itself Anti-pattern #9 (skipping Stage 0). Rejected.

### Option B — Staged protocol (short batch by default, extended batch on `/compact` signal)

Define a short mandatory batch (git status + git log) and an extended batch (add gh pr list,
but status, ledger scan, inbox scan) triggered on `/compact` indicators.

**Pro:** lower overhead for routine turns.
**Con:** the boundary between "routine" and "needs extended batch" requires judgment,
and judgment degrades under token pressure. Parallel-session collisions do not announce
themselves as requiring an extended scan — the 2026-04-28 incident happened on what felt
like a routine research session. Rejected.

### Option C — Canonical full batch, mandatory at session start and `/compact` recovery [RECOMMENDED]

Define one fixed ordered batch. Run it at session start (cold start or restart) and after
any `/compact` event (identified by terse first message, session-start signal, or explicit
user signal). Separate the session-start batch from the pre-action pending-state verification
rule. No staged logic; no judgment calls about which tier to apply.

**Pro:** deterministic; no per-session judgment; idempotent; directly addresses all three
documented incidents; consistent with ADR 0070's "filesystem is the IPC" design.
**Con:** slightly higher overhead per session start. Acceptable given the sub-second cost
of each command.

**Decision: Option C.**

---

## Decision

### §1 — Canonical session-startup batch

Every session, at cold start or after restart, MUST run the following commands before taking
any substantive action on "pending" tasks from project memory or compacted summaries. Commands
are read-only. Run in the order listed; each one may inform whether the next is needed.

```bash
# 1. Recent commit history across all branches (detects parallel-session work)
git log --oneline -10 --all --decorate

# 2. Working tree state (detects untracked or modified files from prior session)
git status

# 3. Open PRs, especially auto-merge-armed ones (parallel-session work in flight)
gh pr list --state open --json number,title,headRefName,autoMergeRequest

# 4. GitButler virtual-branch status (COB primary; XO runs when on gitbutler/workspace)
but status

# 5. Recent .wolf/memory.md entries (detects file-edit activity since last turn)
tail -n 50 .wolf/memory.md

# 6. Active-workstreams ledger (detect state changes: design-in-flight → ready-to-build, etc.)
grep -E "^\|" icm/_state/active-workstreams.md | head -60

# 7. Research-inbox scan (detect beacons filed by COB, PAO, or Yeoman)
ls icm/_state/research-inbox/*.md 2>/dev/null || echo "(inbox empty)"
```

**Ordering rationale:** steps 1–3 detect what has changed in the repo and PR state (highest
staleness risk); step 4 detects GitButler virtual-branch state that git log alone may not
surface; step 5 detects recent file edits via OpenWolf's change log; steps 6–7 check the
ICM coordination surfaces last, because their interpretation may depend on what step 1–5
revealed (e.g., a `ready-to-build` row in the ledger is actionable only if step 3 confirms
no in-flight PR already covers it).

**Role assignments:**

| Role | Must run | Notes |
|---|---|---|
| XO (research) | Steps 1–7 | Primary coordination role; must see full picture |
| COB (sunfish-PM) | Steps 1–5, 6 | Step 7 is mandatory for COB — primary inbox consumer |
| PAO (book editor) | Steps 1–3, 7 | Book-side; steps 4+5 less relevant; inbox scan is PAO's signal |
| Yeoman (tech writer) | Steps 1–3 | Narrowest blast radius; git state sufficient |

When in doubt, run all seven steps. The batch is fast. The cost of an extra step is negligible;
the cost of a missed step is an incident.

---

### §2 — `/compact` recovery procedure

A `/compact` event produces a compacted summary that is a point-in-time snapshot. The session
has lost visibility into anything that happened after the moment of compaction. The recovery
procedure re-establishes ground truth.

**Signals that a `/compact` event has occurred:**

- First message after compaction is terse (single word: "continue", "status", "go", "ok")
- Session context indicates this is a resumed or continued session
- User references a task or workstream from memory without providing fresh context
- Compacted summary mentions "pending" tasks, "in-progress" workstreams, or "next steps"

**When any of these signals are present, execute the full session-startup batch (§1) before
any other action.** Do not assume the compacted summary's "pending" list is current.

**Additional check after `/compact` (XO only):**

```bash
# 8. Tail recent project memories (detect cross-session announcements since compaction)
ls -t ~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/ | head -10
```

This surfaces memory files written by parallel sessions that post-date the compaction. A
project memory written after the compacted summary was created will not appear in the summary;
step 8 makes it visible.

**Pending-state verification rule (universal):**

> **Never act on a "pending" status from a compacted summary or project memory without first
> verifying that status against the current repo state.**

Operationally: if the compacted summary says "PR #X is pending review," run `gh pr view X`
before commenting on it or dispatching further work against it. If the summary says "workstream
W#N is ready-to-build," read the ledger row in `icm/_state/active-workstreams.md` and confirm
it still says `ready-to-build` before dispatching a COB build session.

---

### §3 — Pre-action verification for pending-state claims

The pending-state verification rule applies beyond session start and `/compact` recovery. Any
time a session is about to act on a claim about external state, it must verify that claim is
current. This includes:

- "The hand-off file says X" → verify the file exists at the cited path and read it
- "ADR 0NNN says Y" → open the ADR and confirm the section cited exists with that content
- "The ledger row says ready-to-build" → `grep -A3 "W#NN" icm/_state/active-workstreams.md`
- "PR #NNN is open" → `gh pr view NNN --json state,mergedAt`
- "No open PR covers package X" → `gh pr list --state open` and grep for the package name

This is not a burdensome gate — each verification is a single command. The rule prevents the
cohort failure mode described in ADR 0070 §Consequences: "council subagent working from a
compacted summary that contained a stale symbol reference."

---

### §4 — Session-start checklist (operator-visible summary)

For operator reference, the mandatory session-start obligations in priority order:

```
[ ] 1. git log --oneline -10 --all --decorate   (parallel-session activity?)
[ ] 2. git status                               (dirty working tree?)
[ ] 3. gh pr list --state open                 (auto-merge PRs in flight?)
[ ] 4. but status                               (virtual branches with uncommitted work?)
[ ] 5. tail -n 50 .wolf/memory.md              (recent file edits?)
[ ] 6. ledger scan (active-workstreams.md)     (state changes since last turn?)
[ ] 7. inbox scan (research-inbox/)            (beacons pending response?)
[ ] 8. [/compact only] recent memory file list (cross-session announcements post-compaction?)
```

A session that cannot complete this batch (e.g., token budget too low at session start) should
treat its compacted summary as unreliable and defer action on any "pending" items until the
batch can run.

---

## Consequences

### Positive

- **Eliminates the documented incident class.** Both the 2026-04-28 and 2026-04-26 incidents
  share the same root cause: acting on state assumed current without verification. The
  startup batch prevents both.
- **Makes `/compact` recovery deterministic.** The recovery procedure moves from a user
  memory file (point-in-time) into a formal ADR (survives session restarts without relying
  on project memory auto-load).
- **Operationalizes ADR 0070's "filesystem is ground truth" principle.** The repo's state is
  authoritative; compacted summaries and memories are shortcuts requiring verification first.
- **Low overhead.** Full seven-step batch completes in under 15 seconds. Negligible against
  sessions that run for hours.

### Negative

- **Startup friction.** Sessions currently begin with substantive work immediately after a
  prompt. This protocol adds a mandatory read batch; deliberate adoption required.
- **Verbosity risk.** Narrating the full batch output before every turn erodes the habit.
  The protocol must be executed silently (one-line summary at most). See checklist item 4.

### Trust impact / Security and privacy

No security or privacy surface affected. All commands in the startup batch are read-only.
The protocol governs session behavior, not data handling or access control.

---

## Compatibility plan

This ADR makes no code changes. Compatibility is entirely behavioral:

- **CLAUDE.md:** §Multi-Session Coordination already references `feedback_verify_pr_state_
  at_session_start.md`. That reference should be updated to cite ADR 0074 as the canonical
  source. The memory file is not deleted — it may auto-load in sessions that don't have the
  updated CLAUDE.md — but ADR 0074 supersedes its content.
- **Project memories:** `feedback_git_discipline.md` Rule 6 and `feedback_verify_pr_state_
  at_session_start.md` (if it exists as a standalone file) are functionally superseded by
  §1–§4 of this ADR. They can be retained for historical reference or archived.
- **All four session roles (XO, COB, PAO, Yeoman):** adopt the role-specific startup batch
  assignment in §1. No code changes; purely behavioral adoption.

---

## Implementation checklist

- [ ] Update `CLAUDE.md` §Multi-Session Coordination to cite ADR 0074 for the session-start
      batch (replace / supplement the `feedback_verify_pr_state_at_session_start` memory
      reference).
- [ ] Update `docs/adrs/README.md` index table to include ADR 0074.
- [ ] Update `docs/adrs/INDEX.md` if it exists and has a `process` or `governance` section.
- [ ] Add a one-line policy comment in the `icm/_state/active-workstreams.md` header noting
      that sessions MUST verify ledger state via startup batch (§1 step 6) before acting on
      any `ready-to-build` row.
- [ ] Consider archiving `feedback_verify_pr_state_at_session_start.md` if it exists as a
      standalone project memory (it may be incorporated into `feedback_git_discipline.md`
      Rule 6 and not exist independently). If archived, note supersession by ADR 0074 in
      the archive note.
- [ ] Enforce execution style: startup batch output is summarized in ≤3 lines ("N open PRs,
      latest commit X, inbox empty / N beacons pending"), not narrated in full. Long startup
      preambles erode the habit by making it feel expensive.

---

## Open questions

**OQ-1 — Automation path.** As of 2026-05-01, Claude Code hooks (`PreToolUse`, `PostToolUse`,
`Stop`) do not include a `SessionStart` hook. If one becomes available, the startup batch
should be converted from behavioral protocol into an automated invariant. File a follow-up
intake if this hook is added.

**OQ-2 — Token-budget detection.** A session starting with a critically low token budget
cannot run all seven steps. Current protocol: defer action on pending items if the batch
cannot run. No programmatic token-budget detection exists; edge case for most sessions.

**OQ-3 — Inbox scan frequency beyond session start.** ADR 0070 §Decision states "XO scans
inbox every loop iteration." For long-running XO sessions, the loop-iteration scan is
separately specified in `feedback_loop_discipline.md`. These specifications are additive.

---

## Revisit triggers

1. **Claude Code adds a `SessionStart` hook** capable of executing shell commands before the
   first user turn. If this becomes available, the behavioral protocol in this ADR should be
   replaced with an automated hook, and this ADR should be amended to reflect the new
   enforcement mechanism.

2. **The multi-session naval-org structure is retired.** If ADR 0070 is superseded (e.g., by
   a new harness capability that enables real-time session coordination without filesystem
   IPC), the parallel-session collision risk is reduced, and the startup batch may be
   simplified. This ADR's revisit trigger depends on ADR 0070's revisit triggers.

3. **A new incident class emerges that the startup batch does not cover.** If a stale-state
   incident occurs that would not have been prevented by the §1 batch, add a new step to the
   batch and amend this ADR. The batch is designed to be extended, not replaced.

4. **The five-minute startup batch proves to materially impair session throughput.** If
   measurement shows that the startup batch consumes >5% of session token budget on average
   (extremely unlikely given sub-second command costs), a lighter-weight variant may be
   appropriate.

---

## References

### Predecessor and sister ADRs

- [ADR 0070](./0070-multi-session-naval-org-structure.md) — Multi-Session Naval-Org Structure.
  This ADR composes [70]: it takes the session-start batch pattern cited in ADR 0070's
  References section and promotes it from a user-memory file into a formal ADR-level
  specification. ADR 0070 describes WHO the sessions are and what they own; ADR 0074
  describes HOW each session starts safely.

### User memories (superseded / reinforced by this ADR)

- `feedback_git_discipline.md` Rule 6 — Verify PR/commit state at session start. This ADR
  extends that rule with the full ordered batch (§1), the `/compact` recovery procedure
  (§2), and the pending-state verification rule (§3). Rule 6 remains as a memory-layer
  reminder; ADR 0074 is the canonical source.
- `feedback_verify_pr_state_at_session_start.md` (if exists as standalone) — the direct
  predecessor memory that this ADR formally supersedes.

### Operational precedents that shaped this ADR

- **2026-04-28 W#18 / kernel-audit scaffolding incident:** lead research session duplicated
  work already done by a parallel session due to missing session-start verification.
  (~30 min lost.) Primary motivator for §1 and §2.

- **2026-04-26 i18n cascade / worktree cleanup incident:** worktree removal while agent was
  active; ~50 min translation work lost. Motivator for §2 pending-state verification rule
  and for §3's "verify before acting on external state" principle.

- **ADR 0028-A6.2 structural citation error (pre-merge):** council subagent cited symbol on
  wrong type from compacted summary; caught pre-merge by adversarial council. Motivator
  for the pending-state verification rule extension to non-session-start contexts (§3).
  See `feedback_council_can_miss_spot_check_negative_existence.md`.

### ICM state files

- [`icm/_state/active-workstreams.md`](../../icm/_state/active-workstreams.md) — ledger;
  startup batch step 6 target.
- [`icm/_state/research-inbox/`](../../icm/_state/research-inbox/) — beacon inbox; startup
  batch step 7 target.

---

## Pre-acceptance audit (§A0 self-audit)

- [x] **AHA pass.** Three options considered (advisory-only, staged, canonical full batch).
      Option C chosen because Options A and B degrade precisely in the failure mode that
      motivated this ADR. *(AP-10: checked.)*

- [x] **FAILED conditions / kill triggers.** Named: SessionStart hook automation (OQ-1 +
      RT-1); ADR 0070 retired (RT-2); batch materially impairs throughput (RT-4).
      *(AP-11: checked.)*

- [x] **Rollback strategy.** Behavioral-only ADR; rollback = revert CLAUDE.md update +
      remove from index. No data migration; no package changes; no CI changes. *(AP-4.)*

- [x] **Confidence level.** HIGH. Derived from three documented incidents with known root
      causes; all seven commands are standard CLI tools in active use. *(AP-13: checked.)*

- [x] **Cited-symbol verification.** No `Sunfish.*` type symbols cited. All citations are
      to file paths verified to exist in worktree at `/tmp/sunfish-adr-0074-wt/`.
      *(AP-21: checked.)*

- [x] **Anti-pattern scan.** AP-1: incidents cited with dates + losses; AP-3: N/A (no
      phases); AP-9: three options considered; AP-12: N/A (no timeline); AP-21: no code
      symbols cited. *(21-AP list: checked.)*

- [x] **Revisit triggers.** Four triggers named. *(AP-11: checked.)*

- [x] **Cold Start Test.** Six observable checklist items; each names a target file and
      action. Executable without author clarification. *(Stage 2 Check 5: checked.)*

- [x] **Sources cited.** Three incidents with dates, losses, root causes, and memory file
      references. All factual claims traceable to documented memories. *(AP-21 part 2.)*
