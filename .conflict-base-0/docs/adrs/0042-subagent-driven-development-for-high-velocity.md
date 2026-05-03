# ADR 0042 — Subagent-Driven Development for High-Velocity Sessions

**Status:** Accepted (2026-04-26)
**Date:** 2026-04-26
**Resolves:** A development-process choice that delivered the 2026-04-26 session's ~30 PRs in one day. The pattern — controller-orchestrated parallel background subagents, each on its own worktree branch, each completing as a PR with `gh pr merge --auto --squash` — was used continuously throughout the session but never written down as an explicit policy. This ADR backfills the rationale, names the failure modes that surfaced (and the mitigations applied), and establishes when the pattern is and isn't appropriate.

---

## Context

Traditional sequential AI-assisted development looks like: human prompts agent, agent edits files, agent runs tests, agent reports back, human reviews, human prompts the next change. Per-PR wall-clock is dominated by serial dependencies — the agent waits for builds, the human waits for the agent. A productive session might land 3-5 PRs.

The subagent-driven pattern inverts the bottleneck. The controller (the foreground Claude session, plus the human as decision-maker) acts as a dispatcher: independent tasks are broken into briefs, each brief goes to a background subagent operating on its own git worktree, the subagent completes the work and opens a PR with auto-merge enabled. The controller is freed to dispatch the next 5 tasks, review prior PRs, and react to task-completion notifications. Wall-clock per PR drops because the controller is no longer the bottleneck for any individual PR.

The 2026-04-26 session shipped:

- 7 i18n cascades (one per locale family) — PRs #121, #122, #125, #143
- A11y fixes across many components — PRs #114, #117, #123, #127, #134
- CI hardening — PRs #108, #110, #116, #119, #126, #129, #130, #132, #133, #138
- Analyzer cascades — PRs #124, #128
- Documentation, audits, ADRs — PRs #115, #117, #119, #133, #135, #136, #139, #140, #141
- Plus this ADR backfill batch (#0038-0042)

That is ~30 PRs in roughly 8-10 working hours. The same scope sequentially, with the same human bandwidth, would have taken 1-2 weeks.

The pattern carries real costs (named below). This ADR is the explicit acknowledgment of the trade-off and the contract for using the pattern responsibly.

---

## Decision drivers

- **Solo-maintainer capacity is the binding constraint.** Sequential development consumes the maintainer's full attention per PR — review, decision, prompt, wait, repeat. Subagent-driven development decouples dispatch from completion, so per-task attention shrinks to "write a brief" + "review a finished PR."
- **Per-PR autonomy is achievable for many task shapes.** i18n locale cascades, a11y component fixes, CI workflow tweaks, ADR drafting, and documentation refreshes are largely independent. Each can be done by a subagent without coordination with the others.
- **Auto-merge as the completion gate works.** Per the user's `feedback_pr_push_authorization` memory, every change ships as PR + `gh pr merge --auto --squash`; CI is the only gate. This means subagent completion = PR open + auto-merge enabled = lands when CI is green, no further controller action required.
- **Background-task notifications close the dispatch loop.** When a subagent finishes (or fails), the harness emits a notification. The controller doesn't poll; it reacts.
- **Worktrees provide isolation.** Each subagent operates on its own git worktree at `.claude/worktrees/agent-<id>/`. No shared filesystem state; merge conflicts surface only at PR-rebase time, where they're cheap to resolve.
- **Failure modes are named and bounded.** The session surfaced specific failure shapes (husky bootstrap on fresh worktrees; worktree-cleanup races; dedup-blast-radius surprises). Mitigations exist for each (per the buglog and user memory notes). The pattern is productive even with these costs.
- **Pre-release + breaking-changes-approved posture** — high-velocity development is well-matched to a phase where rapid iteration trumps per-change deliberation. Post-v1, the calculus shifts.

---

## Considered options

### Option A — Sequential development (status quo before the pattern)

One agent at a time. Controller prompts, waits, reviews, prompts again.

- **Pro:** Simplest mental model; one thread of attention.
- **Pro:** Per-change deliberation is high; no dispatch overhead.
- **Con:** Throughput cap at ~3-5 PRs/day for non-trivial work. Doesn't scale to the project's current backlog.
- **Con:** The controller's wait-for-build time is wasted bandwidth.
- **Rejected** as the default. Still appropriate for tasks that genuinely require sequential attention (architectural reshape, security review of a single PR, anything where context-handoff cost between subagent and controller exceeds the wall-clock savings).

### Option B — Subagent-driven dispatch with parallel background tasks (this ADR)

Controller decomposes work into 5-8 independent task briefs. Each brief goes to a background subagent on its own worktree. Subagent completes work, opens PR, enables auto-merge. Controller reacts to task-completion notifications and dispatches the next batch.

- **Pro:** ~6-10× throughput vs. sequential for parallelizable task shapes.
- **Pro:** Controller bandwidth shifts from "wait for builds" to "design briefs + review outcomes" — a higher-leverage activity.
- **Pro:** Each subagent's PR is independently reviewable and revertable. Bad PRs don't poison the pipeline.
- **Pro:** Worktree isolation prevents cross-task interference. Conflicts surface at merge time, where the PR-rebase mechanism handles them.
- **Con:** Brief-writing is now a load-bearing skill. A vague brief produces a vague subagent that produces a sloppy PR. The session's `_shared/engineering/subagent-briefs/` template library exists specifically because brief quality compounds.
- **Con:** Blast-radius surprises happen. A subagent given too broad a scope can edit more than expected. Mitigation: strict diff-shape clauses in briefs (e.g., "diff: 5 new ADR files + index updates only").
- **Con:** Worktree-cleanup races and husky-bootstrap failures occur in practice (logged in `.wolf/buglog.json`). Each has a known fix; subagent restart is cheap.
- **Con:** Occasional dedup work — two subagents touching adjacent files produce conflicting PRs that need resolution.
- **Adopted** as the default for parallelizable task shapes during high-velocity sessions.

### Option C — Sequential with one background agent for build-waiting only

Use a background agent purely to absorb build-wait time on the current sequential PR. Don't dispatch true parallel work.

- **Pro:** Captures the easiest throughput win without the brief-writing complexity.
- **Pro:** No worktree management.
- **Con:** Throughput improvement is small (build-wait is maybe 30% of total cycle time).
- **Con:** Doesn't address the controller-bandwidth bottleneck, which is the binding constraint.
- **Defer.** This is a strict subset of Option B; if you'd run Option C you'd run Option B unless you have a specific reason not to.

### Option D — Hand off to a single long-running coding agent (e.g., Aider, Claude Code in autonomous mode)

Give one agent a multi-task list and let it run unattended.

- **Pro:** Lowest controller bandwidth.
- **Pro:** Minimal handoff overhead within one session's context.
- **Con:** Single thread of execution — same throughput as Option A.
- **Con:** Failure modes are session-wide; one bad task can derail the entire run.
- **Con:** Loses the per-task PR isolation that makes recovery cheap.
- **Rejected** for the multi-task case. Right answer for some single-task shapes (long debugging sessions, unattended overnight runs).

### Option E — Multi-instance pair-coding (multiple Claude sessions, each driven by the same human)

Open N Claude sessions in parallel, drive each manually.

- **Pro:** True parallelism without the brief-writing layer.
- **Con:** Human attention doesn't actually parallelize. Each session needs human prompting; throughput is bottlenecked at the human's context-switching cost.
- **Con:** Quickly devolves into one session being primary and the others starving.
- **Rejected.**

---

## Decision

**Adopt Option B: subagent-driven development with parallel background dispatch is the default execution model for parallelizable task shapes during high-velocity sessions. Sequential (Option A) remains the default for tasks that genuinely require sequential attention (architectural reshape, security audit, anything where brief-writing cost exceeds wall-clock savings).**

The decision rests on three premises:

1. **Throughput is the binding constraint at this stage.** The project's backlog (per the cleanup-debt audit, the ICM stage queues, the parity matrix) is large enough that 3-5 PRs/day will not close it in a reasonable window. Subagent-driven dispatch is the only proven way to scale solo-maintainer throughput by an order of magnitude without sacrificing PR quality.
2. **Failure modes are bounded and recoverable.** Every failure mode the session surfaced has a known fix (logged to `.wolf/buglog.json`); none of them caused unrecoverable damage; subagent restart is cheap. The pattern's downside is real but not catastrophic.
3. **Auto-merge + CI gate provides the safety net.** Subagent completion ≠ merge; merge requires CI green. Bad subagent output that breaks tests doesn't land. The risk surface is "merged-but-wrong" not "caused damage on the way."

### Contract for using the pattern

A subagent dispatch is appropriate when ALL of the following hold:

1. **Tasks are genuinely independent.** No shared filesystem state, no shared API contract changes mid-flight, no order dependency.
2. **Briefs include a strict diff-shape clause.** "Diff: N files of type X" or equivalent. Without this, blast radius surprises are routine.
3. **Briefs include a self-cap clause.** Time or work-unit cap (e.g., "self-cap 75 min" or "self-cap 5 PRs"). Without this, a subagent can spiral.
4. **Each subagent produces a single PR.** Multi-PR subagents are an anti-pattern — they accumulate uncommitted state and become hard to recover from.
5. **Auto-merge is the completion path.** The subagent enables `gh pr merge --auto --squash`; the controller does not gate the merge manually.
6. **Each subagent operates on its own worktree.** No shared workspaces; worktrees are at `.claude/worktrees/agent-<id>/`.

A subagent dispatch is INAPPROPRIATE for:

- **Architecture-shaping changes** that need controller-and-human deliberation per design choice. Use the ICM pipeline (sequential).
- **Security-sensitive changes** where per-change human review is the load-bearing safety primitive. Use sequential.
- **Tasks where context-handoff cost between subagent and controller exceeds the wall-clock savings.** A 15-minute brief for a 10-minute task is a loss.
- **Single-task sessions.** Subagent dispatch overhead is amortized across many tasks; one task isn't worth it.

### Failure modes named in the session (and their fixes)

| Failure mode | Symptom | Fix | Reference |
|---|---|---|---|
| Husky bootstrap on fresh worktree | Subagent commits fail because Husky's Node hook isn't installed in the worktree | PR #115 dropped Node-husky bootstrap so fresh worktrees commit cleanly | PR #115; `.wolf/buglog.json` |
| Worktree cleanup race | Cleanup script removes a worktree while a subagent is still active | Check for running agents before cleanup; user memory `feedback_worktree_cleanup_check_running_agents` | User memory note |
| Commitlint type-enum strictness | Subagent uses unapproved commit type (e.g., `security:`) and commit lint fails | Use approved types only — `feat\|fix\|docs\|style\|refactor\|perf\|test\|build\|ci\|chore\|revert`; security work uses `ci(security):` etc. | User memory `project_commitlint_type_enum`; ADR adds clause to brief template |
| Blast-radius surprise | Subagent edits more than the brief intended; PR is too large | Add "Diff: N files of type X" clause to every brief | Standing rule per this ADR |
| Two subagents touching adjacent files | Conflicting PRs that need rebase | Resolve at merge time; cheap when PRs are small | Standard PR-rebase flow |
| Subagent fails silently | No notification; controller doesn't know to dispatch the next task | Subagent must always emit a final report (success or failure); brief template includes "report what was done" line | Brief template convention |

### Brief-writing as a load-bearing skill

The session demonstrated that subagent throughput is bounded by brief quality, not by subagent capability. A well-formed brief includes:

- **Context** — what the change is for, why it matters now.
- **Task** — explicit deliverables, in order.
- **Diff shape** — file count + types ("5 new ADR files + index updates only").
- **Constraints** — self-cap, commit conventions, husky/commitlint specifics, auto-merge target.
- **Evidence/references** — links to PRs, files, prior decisions the subagent should ground its work in.
- **Output expectation** — the brief asks for a final report covering "what was done and key findings."

The `_shared/engineering/subagent-briefs/` directory exists to template briefs by recurring task shape (i18n cascade, a11y fix batch, ADR drafting, etc.). Adding a new brief template is itself a reusable investment.

---

## Consequences

### Positive

- ~30 PRs landed in the 2026-04-26 session vs. an estimated 3-5 with sequential development — a ~6-10× throughput improvement on parallelizable work.
- Controller bandwidth shifts to higher-leverage activities (brief design, PR review, ICM-stage decision). Wall-clock per task drops sharply.
- Each subagent's PR is independently reviewable and revertable — no all-or-nothing batch behavior.
- Auto-merge + CI gate means subagent failures don't land. The risk surface is bounded.
- Failure modes are surfacing in a bounded environment (the buglog accumulates them with fixes), so the next session's pattern usage benefits from this session's learnings.
- The brief-template library compounds — each new brief shape that gets templated lowers the cost of the next dispatch of similar work.
- Subagent worktrees provide a natural sandbox for risky changes; the worst case is "delete the worktree and re-dispatch" rather than "polluted main workspace."

### Negative

- Brief-writing is now load-bearing. A bad brief produces a bad PR, sometimes a bad PR that auto-merges before the controller can intervene. Mitigation: strict diff-shape clauses; PR review habit even when CI is green.
- Worktree count grows during high-velocity sessions; cleanup races (per the user memory note) require explicit "check for running agents" logic before any cleanup.
- Husky/commitlint failures surface only at commit time, after the subagent has done the work. Mitigation: PR #115 reduced this; brief templates include the constraints upfront.
- Dedup work happens occasionally — two subagents touching adjacent files (e.g., the same workflow YAML, the same .resx file) produce PRs that conflict. Resolved at PR-rebase time but adds wall-clock cost.
- Subagent failure visibility depends on notifications; a silent failure (subagent crash without notification) is hard to detect. Mitigation: brief template requires a final report; absent report = treat as failure.
- The throughput advantage assumes parallelizable work. A session whose backlog is mostly architectural-reshape tasks gets no benefit and pays the dispatch overhead.
- Quality of subagent output depends on the subagent's grounding context. Briefs that don't include "read these files first" can produce work that looks plausible but ignores established conventions. Mitigation: brief templates point at the relevant `.wolf/anatomy.md`, ADRs, and code locations.
- Subagent-driven sessions are mentally taxing in a different way than sequential — the controller is constantly context-switching between briefs. Long sessions need hard breaks.

---

## Revisit triggers

This ADR should be re-opened when **any one** of the following occurs:

1. **A subagent failure causes unrecoverable damage** (e.g., merges a PR that breaks `main` in a way `git revert` doesn't fix). Diagnose the brief or pattern flaw; tighten the contract.
2. **The brief-template library grows past ~10 distinct shapes.** At that point a meta-organization (taxonomy, naming convention, discoverability) becomes worth the work.
3. **Second contributor joins.** Subagent dispatch with two humans is a different beast — coordination overhead, brief-ownership, review responsibility need explicit roles. Open a follow-up ADR.
4. **First v1 release ships.** Post-v1, per-PR scrutiny rises and the velocity-vs-deliberation tradeoff shifts. Re-examine which task shapes are still appropriate for parallel dispatch.
5. **A task-completion-notification primitive becomes available** that materially improves the dispatch loop (e.g., richer per-task status streaming). Re-tune the pattern around it.
6. **Subagent harness changes** materially (e.g., new background-process model, different worktree semantics, different auth model for `gh`). Re-validate the contract.

---

## References

- **Session this ADR ratifies:**
  - The 2026-04-26 session full PR list (#108-#144 range; ~30 PRs). See `CHANGELOG.md` entry for that date and PR #139 (`docs(changelog): 2026-04-26 session — 30+ PRs across CI/a11y/i18n/analyzers/security`) for the consolidated list.
- **Failure-mode references:**
  - PR #115 — `chore(husky): drop Node-husky bootstrap so fresh worktrees commit cleanly` — the husky-bootstrap fix.
  - User memory `feedback_worktree_cleanup_check_running_agents` — the worktree-cleanup race; check-before-clean rule.
  - User memory `project_commitlint_type_enum` — commitlint strict type-enum; security work uses `ci(security):` / `chore(security):` / `docs(security):` not `security:`.
- **Brief-template library:**
  - [`_shared/engineering/subagent-briefs/`](../../_shared/engineering/subagent-briefs/) — the canonical brief templates by task shape. Includes the i18n cascade brief from PR #144.
  - [`_shared/engineering/subagent-briefs/i18n-cascade-brief.md`](../../_shared/engineering/subagent-briefs/i18n-cascade-brief.md) — example template the session refined and reused across 7 cascades.
- **Companion ADR:**
  - [ADR 0040](./0040-translation-workflow-ai-first-3-stage-validation.md) — the i18n workflow that makes per-locale parallel cascades possible. Without subagent-driven dispatch, AI-first translation alone wouldn't have produced 16 locales in a day; without the 3-stage gate, the parallel cascades would have produced unreviewed output. The two ADRs are complementary.
- **Process:**
  - User memory `feedback_pr_push_authorization` — the PR + auto-merge default that subagent dispatch depends on as the completion path.
  - [`/icm/CONTEXT.md`](../../icm/CONTEXT.md) — the ICM pipeline that subagent-driven development sits inside (subagents typically operate within stage 06_build of an ICM pipeline).
- **Operational artifacts:**
  - [`.wolf/buglog.json`](../../.wolf/buglog.json) — accumulating record of subagent-pattern failure modes and their fixes.
  - [`.wolf/memory.md`](../../.wolf/memory.md) — session memory including the running list of dispatched agents.
- **Related ADR:**
  - [ADR 0037](./0037-ci-platform-decision.md) — staying on GitHub Actions; auto-merge as the subagent completion gate depends on the GHA + `gh` CLI surface.
