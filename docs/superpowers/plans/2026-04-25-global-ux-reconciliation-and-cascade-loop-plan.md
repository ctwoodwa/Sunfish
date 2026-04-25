# Global-UX Reconciliation + Cascade Loop Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` for fan-out execution and `superpowers:dispatching-parallel-agents` to drive each wave's parallel batch. The driver-loop iterations should be paced via `ScheduleWakeup` (skill: `loop`). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reconcile the local-vs-PR-#66 i18n duplication, complete the Plan 2 Tasks 3.4-3.6 cascade across the remaining ~17 user-facing packages, refresh the wave tracker to truth, and gate-check Plans 3 / 4 / 4B status so Plan 5 (CI Gates, Wk 6) has a known entry condition — executed as a five-wave loop with parallel subagent fan-out per wave.

**Architecture:** A single tracker file (`waves/global-ux/reconciliation-loop-tracker.md`) holds wave state. A loop driver re-enters every iteration, reads the tracker, dispatches the wave's fan-out subagents in parallel, gates each subagent's output through a reviewer subagent, and either advances or halts on red. Five waves: (0) reconciliation + PR push, (1) status truth four-way fan-out, (2) cascade five-cluster fan-out, (3) per-cluster reviewer gate, (4) Plan 5 entry-conditions sequential. Loop terminates on Wave 4 PASS or any wave RED.

**Tech stack:** .NET 11 preview, SmartFormat.NET 3.6.1, `Microsoft.Extensions.Localization`, custom MSBuild XLIFF task (already landed Plan 2 Task 1.x), Roslyn analyzer SDK (`SUNFISH_I18N_001` already cascaded), `gh` CLI for PR-with-auto-merge, `superpowers:subagent-driven-development` skill for fan-out, `superpowers:dispatching-parallel-agents` skill for batching, `superpowers:requesting-code-review` skill for gates.

---

## Context & Why

After fetch+merge of `origin/main` (2026-04-25), local `main` is 10 ahead containing Plan 2 Task 4.2 / 4.3 / 4.4 work that overlaps PR #66's "3 bundles + analyzer gate" cascade. The merge auto-resolved with no conflicts but file paths overlap — both branches scaffolded `SharedResource.{cs,resx,ar-SA.resx}` in foundation, anchor, and bridge. Plan 2 Tasks 3.4-3.6 (the ~17 remaining user-facing packages) are unstarted. The wave tracker (`waves/global-ux/status.md`) was last updated end of Week 1 and falsely says "Tasks 1.1-1.3 in flight" while commits show Wk 4 polish landed. Plans 3 / 4 / 4B status is invisible from this branch. Plan 5 (Wk 6 CI Gates exit gate) needs a known input from Plans 2/3/4/4B before it can dispatch.

This plan converts the prior turn's "next steps" list into a parallel-execution loop driven by Max Pro's token budget — fan out wherever tasks are independent, gate sequentially where ordering matters, log every dispatch and review for cold-resume.

---

## Success Criteria

### PASSED — proceed to Plan 5 entry

- Wave 0: Local-vs-PR-#66 reconciliation memo published; any duplicate code consolidated; merge commit (or replacement) shipped via PR-with-auto-merge.
- Wave 1: Plans 2 / 3 / 4 / 4B current status documented in `waves/global-ux/status.md` with commit-level evidence per task.
- Wave 2: Every package in the user-facing inventory (target: 14 blocks-* + 1 ui-core + 2 adapters + 3 apps + 0-5 foundation-extensions per gap analysis = ~20-25 packages) has `Resources/Localization/SharedResource.resx` with at least one localized entry, `ISunfishLocalizer<T>` DI registration, and one pilot translator-commented string.
- Wave 3: Per-cluster reviewer subagent reports green on (a) spec compliance against Plan 2 Tasks 3.5/3.6, (b) code quality against `_shared/engineering/coding-standards.md`, (c) no `SUNFISH_I18N_001` analyzer warnings outside the documented 5% noise floor.
- Wave 4: `waves/global-ux/week-3-cascade-coverage-report.md` exists; `waves/global-ux/status.md` reflects Plan 5 entry conditions met or named blockers; one feature branch per merged PR; `main` clean; no direct-push violations.

### FAILED — triggers a re-plan, not project abort

- Wave 0 reveals semantic divergence (not just duplication) between local and PR #66 code paths — escalate, do not auto-merge; produce diff memo and request user decision before any rewrite.
- Wave 2 cluster cannot complete because a package family has no clean string-formatting seam — document deferral with named follow-up plan, do not synthesize fake strings.
- Wave 3 reviewer-agent fails ≥2 clusters in a row — halt loop, surface to user; likely indicates the cascade pattern needs revision before remaining clusters re-dispatch.
- Plan 3 or Plan 4 status check (Wave 1) reveals zero progress and the plan is owned by a stalled subagent or off-branch worktree — flag as blocker for Plan 5 timeline; do not unblock unilaterally.

### Kill trigger (14-day timeout)

If this plan has not landed all PASSED criteria by **2026-05-09** (14 days from start), escalate to user for scope cut: named options are (a) defer Wave 2 cascade to blocks-* only, leave foundation-extensions and federation as out-of-scope; (b) declare Wave 1 sufficient and exit before Wave 2 if PR #66 already covered the priority surface; (c) merge directly to main with `git push` (waiving PR-with-auto-merge for this wave only) if the bottleneck is review latency rather than work latency.

---

## Assumptions & Validation

| Assumption | VALIDATE BY | IMPACT IF WRONG |
|---|---|---|
| Local commits and PR #66 produce identical `SharedResource.{cs,resx,ar-SA.resx}` content for foundation/anchor/bridge | Wave 0 Step 0.1 — `git diff origin/main~7..origin/main` against current `HEAD` for those nine files | Reconciliation becomes a merge-conflict-resolution exercise, not a dedup; +1-2 days slip |
| `git push` is blocked by user policy but `gh pr create --base main --head <feature>` then `gh pr merge --auto --squash` is permitted | Wave 0 Step 0.4 — verify `git push origin <feature-branch>` succeeds (not `main`); see memory `feedback_pr_push_authorization` | If `gh pr merge --auto` is also gated, fall back to manual approval cadence; loop pauses on each PR |
| 14 `blocks-*` packages share a common DI-registration pattern that one subagent template can apply across them | Wave 2 Step 2.0 — read `packages/blocks-tasks/` and `packages/blocks-accounting/` Program/Startup files; if patterns diverge, split clusters by pattern not alphabet | Cluster size grows to 1-2 packages, fan-out reduces, total time +2x |
| `ISunfishLocalizer<T>` and `SunfishLocalizer<T>` from `packages/foundation/Localization/` are stable contracts that downstream packages can take a project reference on without circular deps | Wave 2 Step 2.0 — check `packages/blocks-tasks/*.csproj` for existing `<ProjectReference Include="...foundation..." />` | If foundation isn't referenced, each cluster needs `.csproj` edits which expand task scope |
| Plans 3 / 4 status is recoverable from `git log --all` + worktree inventory + reading their plan files for embedded status notes | Wave 1 Step 1.1 — run `git log --all --oneline | grep -E "Plan [34]"` and `git worktree list` | If status only lives in lost subagent transcripts, mark Plan 3/4 as "unknown" and propose a fresh status-discovery wave |
| `ScheduleWakeup` 1200-1800s pacing is enough for parallel subagent batches to complete between iterations | Wave 0 Step 0.5 — measure first batch completion against scheduled wake | If subagents take >30 min, switch to event-driven re-entry (subagent completion notification) instead of timed wake |
| The `superpowers:dispatching-parallel-agents` skill can fan out 5+ subagents in a single message and the harness will run them concurrently | Wave 2 Step 2.1 — first batch dispatches 5; observe runtime | If serialized, throughput drops 5x; cluster size shrinks or batches go async via `run_in_background` |

---

## File Structure

```
waves/global-ux/
  reconciliation-loop-tracker.md         ← Single source of loop state; loop driver reads/writes this
  reconciliation-pr66-diff-memo.md       ← Wave 0 output; reconciliation finding
  status.md                               ← Refreshed in Wave 1; existed already, edit not create
  week-3-cascade-coverage-report.md      ← Wave 4 output; per Plan 2 Task 3.6
  week-3-plan-2-status.md                ← Wave 1 output (one of four)
  week-3-plan-3-status.md                ← Wave 1 output
  week-3-plan-4-status.md                ← Wave 1 output
  week-3-plan-4b-status.md               ← Wave 1 output

packages/<each-block>/Resources/Localization/SharedResource.resx          ← Wave 2 output × N
packages/<each-block>/Resources/Localization/SharedResource.ar-SA.resx    ← Wave 2 output × N
packages/<each-block>/Localization/SharedResource.cs                      ← Wave 2 output × N
packages/<each-block>/<EntryPoint>.cs                                     ← Wave 2: DI-registration edit

docs/superpowers/plans/
  2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md   ← This plan
```

---

## Tracker File Specification

The tracker is the loop's working memory. Every iteration starts by reading it; every iteration ends by writing it. Schema:

```markdown
# Global-UX Reconciliation Loop — Tracker

**Plan:** docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md
**Started:** YYYY-MM-DDTHH:MM:SSZ
**Last iteration:** YYYY-MM-DDTHH:MM:SSZ
**Iteration count:** N
**Current wave:** 0 | 1 | 2 | 3 | 4 | DONE | RED
**Halt reason:** (only if RED) string

## Wave 0 — Reconciliation
- [ ] 0.1 diff local vs PR #66 nine files
- [ ] 0.2 author reconciliation memo
- [ ] 0.3 dedup or consolidate code
- [ ] 0.4 feature-branch + PR + auto-merge

## Wave 1 — Status truth (parallel)
- [ ] 1.A subagent: Plan 2 status report
- [ ] 1.B subagent: Plan 3 status report
- [ ] 1.C subagent: Plan 4 status report
- [ ] 1.D subagent: Plan 4B status report
- [ ] 1.E driver: merge reports into waves/global-ux/status.md

## Wave 2 — Cascade fan-out (parallel)
- [ ] 2.0 driver: pattern-discovery on 2 reference packages; freeze cluster boundaries
- [ ] 2.A cluster: blocks-finance-ish (accounting, tax-reporting, rent-collection, subscriptions)
- [ ] 2.B cluster: blocks-ops (assets, inspections, maintenance, scheduling)
- [ ] 2.C cluster: blocks-crm-ish (businesscases, forms, leases, tenant-admin, workflow, tasks)
- [ ] 2.D cluster: ui-and-adapters (ui-core, ui-adapters-blazor, ui-adapters-react)
- [ ] 2.E cluster: accelerators-and-apps (apps/kitchen-sink — bridge/anchor done)

## Wave 3 — Quality gate (parallel)
- [ ] 3.A reviewer: cluster A
- [ ] 3.B reviewer: cluster B
- [ ] 3.C reviewer: cluster C
- [ ] 3.D reviewer: cluster D
- [ ] 3.E reviewer: cluster E

## Wave 4 — Plan 5 entry conditions
- [ ] 4.1 driver: author week-3-cascade-coverage-report.md
- [ ] 4.2 driver: refresh status.md with Plan 5 entry verdict
- [ ] 4.3 driver: open Plan 5 if entry verdict GREEN

## Iteration log
| # | Wave | Started | Ended | Outcome | Notes |
|---|---|---|---|---|---|
```

The driver writes one row to "Iteration log" per wake-up. Each entry must be unambiguous so a fresh driver instance can resume cold.

---

## Loop Driver Instructions

The driver is invoked via `/loop <<autonomous-loop-dynamic>>` with `ScheduleWakeup` self-pacing. On each wake:

1. **Read tracker.** If `Current wave == DONE` or `RED`, exit loop (no `ScheduleWakeup`).
2. **Run wave gate-check.** Each wave has explicit pre-conditions defined in its task block. If unmet, write tracker entry "blocked-on: <X>" and wake again in 1800s.
3. **Dispatch wave's fan-out subagents in parallel.** Use `superpowers:dispatching-parallel-agents` skill — single message with multiple `Agent` tool calls. Set `run_in_background: false` if all subagents finish within ~3 min; `true` otherwise (then re-enter on notification, not timer).
4. **Collect subagent reports.** Each fan-out task produces a single markdown file path; the driver reads each.
5. **Dispatch reviewer subagent(s).** Per the skill `superpowers:requesting-code-review`. Reviewer reads the produced files and the relevant section of this plan; writes verdict GREEN | YELLOW | RED.
6. **Update tracker.** Mark each step ✓ or ✗; advance wave if all-GREEN; halt with `Halt reason` if any RED that isn't auto-recoverable.
7. **`ScheduleWakeup`.** If wave advanced or work remains, schedule wake in 1200-1800s with `<<autonomous-loop-dynamic>>` sentinel. If wave is awaiting external review (PR), wake in 1800s and re-check.

The driver MUST NOT skip the reviewer step. The driver MUST NOT mark a step ✓ until evidence (file path, commit SHA, gh PR URL) is recorded in tracker.

---

## Wave 0 — Reconciliation

**Pre-condition:** None (this is the entry wave).

**Why:** PR #66 (`08d5110e`) and local commits `93c53ba2` + `0485abc5` + `d987042d` + `d4dc625e` all wrote files at overlapping paths. Merge auto-resolved without conflict, which means either (a) the contents matched exactly, or (b) ort strategy chose one side. Either way, we don't know which content is canonical, and the analyzer / locale-completeness tool may or may not align. Must verify before further cascade.

### Task 0.1: Diff local vs PR #66 on the nine overlap files

**Files:**
- Read-only: `packages/foundation/Localization/SharedResource.cs`, `packages/foundation/Resources/Localization/SharedResource.resx`, `packages/foundation/Resources/Localization/SharedResource.ar-SA.resx`, mirrors under `accelerators/anchor/` and `accelerators/bridge/Sunfish.Bridge/`

- [ ] **Step 1:** Identify PR #66 base SHA: `git log --merges --oneline | head -5` to confirm `e9effd9a` is the merge; `git log e9effd9a^1..e9effd9a^2 --oneline -- packages/ accelerators/ tooling/` to list incoming PR-#66-side commits.

- [ ] **Step 2:** Identify local-side overlap commits: `git log e9effd9a^2..e9effd9a^1 --oneline -- packages/foundation/ accelerators/` should return `93c53ba2`, `0485abc5`, `d987042d`, `a540410e`, `0f08444b`, `d4dc625e`.

- [ ] **Step 3:** For each of the nine files, run `git show e9effd9a:<path>` (post-merge) and compare against `git show 08d5110e:<path>` (PR #66 tip) and `git show 0485abc5:<path>` (local tip). Use Bash with `diff <(git show ...) <(git show ...)` to produce three-way diff.

- [ ] **Step 4:** Record findings in tracker: which side won the merge, content equivalence (byte-identical / semantically-equivalent / divergent), per-file.

### Task 0.2: Author reconciliation memo

**Files:**
- Create: `waves/global-ux/reconciliation-pr66-diff-memo.md`

- [ ] **Step 1:** Write memo with sections: (a) overlap surface (file list + commits), (b) three-way diff results from Task 0.1, (c) classification per file: NO-OP-DUP / NEEDS-CONSOLIDATION / DIVERGENT, (d) recommended action per classification, (e) downstream-cascade implications (e.g., if local's analyzer config differs from PR #66's).

- [ ] **Step 2:** Reference `waves/global-ux/status.md` and Plan 2 Tasks 4.2/4.3/4.4 explicitly so the memo is grep-discoverable.

- [ ] **Step 3:** Commit:
```bash
git add waves/global-ux/reconciliation-pr66-diff-memo.md
git commit -m "docs(global-ux): reconcile local Plan 2 Task 4.x with PR #66 cascade"
```

### Task 0.3: Consolidate or dedup as memo prescribes

**Files:**
- Modify (only if memo prescribes): the nine overlap files

- [ ] **Step 1:** If memo classified all NO-OP-DUP, skip to Task 0.4 — no edits.
- [ ] **Step 2:** If memo classified any NEEDS-CONSOLIDATION, perform the consolidation per memo. Path-scoped `git add` only the files named in memo.
- [ ] **Step 3:** If memo classified any DIVERGENT, halt with `Halt reason: divergent-pr66` — do not auto-resolve; this requires user judgment.
- [ ] **Step 4:** Build smoke: `dotnet build packages/foundation/Sunfish.Foundation.csproj` — must succeed.
- [ ] **Step 5:** Commit (only if Step 2 ran):
```bash
git add <files-from-memo>
git commit -m "fix(global-ux): consolidate i18n cascade after PR #66 merge"
```

### Task 0.4: Ship as feature branch + PR-with-auto-merge

**Files:**
- (none new)

- [ ] **Step 1:** Create branch from current HEAD:
```bash
git switch -c global-ux/wave-0-reconciliation
```
- [ ] **Step 2:** Push branch (`main` push is blocked — branch push must succeed):
```bash
git push -u origin global-ux/wave-0-reconciliation
```
- [ ] **Step 3:** Open PR with auto-merge on green CI:
```bash
gh pr create --base main --head global-ux/wave-0-reconciliation \
  --title "global-ux: Wave 0 reconciliation — Plan 2 Task 4.x vs PR #66" \
  --body "$(cat <<'EOF'
## Summary
- Reconciliation memo for the local-vs-PR-#66 i18n cascade overlap (nine files, three packages)
- Consolidation edits if memo prescribed any
- Catches main back up to local Plan 2 Wk-4 polish work that landed pre-merge

## Wave 0 of looping plan
See [docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md](../blob/main/docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md).

## Test plan
- [x] dotnet build packages/foundation passes
- [x] No new analyzer warnings outside documented noise
- [x] waves/global-ux/reconciliation-pr66-diff-memo.md present and links to commits

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
gh pr merge --auto --squash
```
- [ ] **Step 4:** Record PR URL in tracker. Do not switch back to `main` until PR is merged (CI gate).
- [ ] **Step 5:** Wave 0 advancement gate: tracker shows PR merged on origin (`gh pr view <#> --json mergedAt -q .mergedAt`) is non-null. Re-fetch and re-set local `main`:
```bash
git switch main && git pull --ff-only origin main
```

**Wave 0 exit:** Tracker rows 0.1-0.4 all ✓; PR merged; local `main` synced. Advance to Wave 1.

---

## Wave 1 — Status truth (4-way fan-out)

**Pre-condition:** Wave 0 exit gate passed.

**Why:** `waves/global-ux/status.md` is one day stale and falsely reports Plan 2 progress. Plans 3 / 4 / 4B status is invisible from this branch. Need ground truth before any further dispatch.

### Task 1.0: Driver — dispatch four parallel status-discovery subagents

**Files:**
- (subagent dispatch only; no driver-side edits)

- [ ] **Step 1:** In a single message, dispatch four subagents using `Agent` tool, all in parallel (no `run_in_background`; expect ~3 min each):

  - **Agent 1.A — Plan 2 status:** subagent_type=`general-purpose`. Brief: "Read `docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md`. For each task (Tasks 1.1 through 4.5), classify status as DONE / IN-PROGRESS / NOT-STARTED with commit-SHA evidence from `git log --oneline --all --since=2026-04-23 -- <files referenced in task>`. Output to `waves/global-ux/week-3-plan-2-status.md`. Do not infer status — require commit evidence or explicit absence. Under 800 words."

  - **Agent 1.B — Plan 3 status:** Same brief structure, plan file `2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md`, output `week-3-plan-3-status.md`.

  - **Agent 1.C — Plan 4 status:** Same, plan file `2026-04-24-global-first-ux-phase-1-weeks-2-4-a11y-foundation-plan.md`, output `week-3-plan-4-status.md`.

  - **Agent 1.D — Plan 4B status:** Same, plan file `2026-04-24-global-first-ux-phase-1-weeks-3-6-ui-sensory-cascade-plan.md`, output `week-3-plan-4b-status.md`.

- [ ] **Step 2:** Wait for all four agents to return. Each returns a single markdown file path.

- [ ] **Step 3:** Read each output file. Verify each contains a status table and at least one git-SHA citation per task.

### Task 1.E: Driver — merge four reports into status.md

**Files:**
- Modify: `waves/global-ux/status.md`

- [ ] **Step 1:** Replace the `## Completed this week`, `## In progress`, and `## Blocked` sections of `status.md` with synthesized content from the four sub-reports. Preserve `## Plans authored` table; update each row's "Status" column.

- [ ] **Step 2:** Add a new section `## Wave 0 reconciliation outcome` referencing the Wave 0 PR and memo.

- [ ] **Step 3:** Update header dates: `**Updated:**` to today; `**Current phase:**` to "Phase 1 Week 3 cascade execution (in flight)"; `**Current focus:**` to "Plan 2 Tasks 3.4-3.6 cluster cascade".

- [ ] **Step 4:** Commit on a new feature branch + PR:
```bash
git switch -c global-ux/wave-1-status-truth
git add waves/global-ux/status.md waves/global-ux/week-3-plan-*.md
git commit -m "docs(global-ux): refresh status — Plans 2/3/4/4B truth + Wave 0 outcome"
git push -u origin global-ux/wave-1-status-truth
gh pr create --base main --head global-ux/wave-1-status-truth \
  --title "global-ux: Wave 1 status refresh" \
  --body "Synthesized truth from four parallel plan-status subagents. See plan: docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md"
gh pr merge --auto --squash
```

- [ ] **Step 5:** Wait for merge. Sync `main`. Advance.

**Wave 1 exit:** Four sub-reports published; `status.md` reflects current truth; PR merged.

---

## Wave 2 — Cascade fan-out (5-cluster parallel)

**Pre-condition:** Wave 1 exit gate passed; `status.md` shows Plan 2 Tasks 3.4-3.6 as the next priority.

**Why:** Plan 2 Tasks 3.4-3.6 cascade across ~17 remaining user-facing packages. Foundation, anchor, and bridge already have bundles (Plan 2 Task 4.x + PR #66). The remaining surface is 14 blocks-* + ui-core + 2 adapters + kitchen-sink. Fan out in 5 clusters.

### Task 2.0: Driver — pattern discovery + cluster freeze

**Files:**
- Read-only: `packages/blocks-tasks/`, `packages/blocks-accounting/`, `packages/foundation/Localization/SunfishLocalizer.cs`, `packages/ui-core/`

- [ ] **Step 1:** Inspect two reference packages (`blocks-tasks`, `blocks-accounting`). Identify (a) entry-point file (Program.cs / ServiceCollectionExtensions.cs / Module.cs), (b) existing `.csproj` foundation reference, (c) existing string-formatting seam.

- [ ] **Step 2:** Choose a per-package template. Required outputs per package: `Resources/Localization/SharedResource.resx` (en-US neutral), `Resources/Localization/SharedResource.ar-SA.resx` (eight entries; mirror foundation count), `Localization/SharedResource.cs` (marker class), DI registration edit (one-line `services.AddLocalization()` + `services.AddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>))` if missing).

- [ ] **Step 3:** Confirm cluster boundaries by reading two packages from each proposed cluster. If any cluster has divergent patterns (different DI surface), split it. Document final cluster set in tracker.

- [ ] **Step 4:** Author per-subagent brief template (one source of truth for all clusters; only the package list changes per cluster). Save to `waves/global-ux/wave-2-subagent-brief.md` for grep-discovery.

- [ ] **Step 5:** Commit the brief:
```bash
git switch -c global-ux/wave-2-cluster-cascade
git add waves/global-ux/wave-2-subagent-brief.md
git commit -m "docs(global-ux): Wave 2 cluster cascade subagent brief"
```

### Task 2.A-2.E: Five-cluster parallel dispatch

**Files:**
- Created/modified by subagents per their cluster's package list

- [ ] **Step 1:** Dispatch five subagents in a single message (parallel `Agent` tool calls, all `subagent_type: general-purpose`, no `run_in_background` initially):

  - **Cluster A (blocks-finance-ish):** packages/blocks-accounting, blocks-tax-reporting, blocks-rent-collection, blocks-subscriptions
  - **Cluster B (blocks-ops):** packages/blocks-assets, blocks-inspections, blocks-maintenance, blocks-scheduling
  - **Cluster C (blocks-crm-ish):** packages/blocks-businesscases, blocks-forms, blocks-leases, blocks-tenant-admin, blocks-workflow, blocks-tasks
  - **Cluster D (ui-and-adapters):** packages/ui-core (if .NET; otherwise N/A), packages/ui-adapters-blazor, packages/ui-adapters-react (TypeScript — different cascade pattern; if so, document and skip until separate plan)
  - **Cluster E (accelerators-and-apps):** apps/kitchen-sink (foundation/anchor/bridge already done)

  Each brief: "Read `waves/global-ux/wave-2-subagent-brief.md` and `docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md` Task 2.0. For each package in cluster X = [list], create `Resources/Localization/SharedResource.resx`, `Resources/Localization/SharedResource.ar-SA.resx` (mirror foundation's eight entries), `Localization/SharedResource.cs` (marker class). Edit the package's entry-point file to register `ISunfishLocalizer<T>`. Use one path-scoped `git add packages/<cluster-roots>/` per cluster. Make ONE commit for the cluster: `feat(i18n): cluster-X cascade — Plan 2 Task 3.5`. Run `dotnet build` on each modified package; must succeed. Output a per-cluster report `waves/global-ux/wave-2-cluster-<X>-report.md` with: package list, file paths created, commit SHA, build evidence, any deferrals (with reason). Under 1500 words. DO NOT push to remote — driver opens the PR after Wave 3 review."

- [ ] **Step 2:** Wait for all five reports. If any subagent fails to build, mark that cluster RED in tracker; do not auto-retry.

- [ ] **Step 3:** Verify each report references its commit SHA and the SHA exists locally: `git cat-file -e <sha>` for each.

**Wave 2 exit:** Five cluster reports landed; five commits exist on `global-ux/wave-2-cluster-cascade` branch; all builds green; tracker advances to Wave 3.

---

## Wave 3 — Quality gate (5 parallel reviewers)

**Pre-condition:** Wave 2 exit gate passed.

**Why:** Per Plan 2 Task 3.5 Step 4: "Reviewer agent (spec-compliance + code-quality, serial) gates each cluster commit before the next cluster dispatches." We're running clusters in parallel for speed, so reviewers fan out in parallel too — but the gate is still per-cluster.

### Task 3.A-3.E: Per-cluster reviewer subagents

**Files:**
- Read-only by reviewers
- Output: `waves/global-ux/wave-3-cluster-<X>-review.md` × 5

- [ ] **Step 1:** Dispatch five reviewer subagents in parallel (single message). Each:
  - **subagent_type:** `superpowers:code-reviewer`
  - **Brief:** "Review the changes in commit <SHA-from-cluster-X>. Read this plan (Task 2.0 template) and `_shared/engineering/coding-standards.md` and `docs/diagnostic-codes.md` for `SUNFISH_I18N_001`. Verify: (a) every package in cluster has the three files (`.resx`, `.ar-SA.resx`, `.cs`), (b) DI registration is present and idempotent, (c) ar-SA bundle has eight entries matching foundation's bundle keys, (d) no `SUNFISH_I18N_001` warnings on build, (e) one path-scoped commit per cluster (not file-scattered). Verdict GREEN | YELLOW | RED with per-issue line citations. Output to `waves/global-ux/wave-3-cluster-<X>-review.md`. Under 800 words."

- [ ] **Step 2:** Wait for all five reviews. Read verdicts.

- [ ] **Step 3:** Decision matrix:
  - All GREEN → advance to Task 3.F.
  - Any YELLOW → driver attempts auto-fix per reviewer's named issues only if scoped to ≤5 lines per package; else escalate to user.
  - Any RED → halt loop with `Halt reason: wave-3-red-cluster-<X>`.
  - ≥2 RED → halt loop with `Halt reason: wave-3-systemic-red`; cascade pattern likely broken; do not retry.

### Task 3.F: Driver — open Wave-2 PR after green gate

**Files:**
- (none; PR creation only)

- [ ] **Step 1:** Push branch:
```bash
git push -u origin global-ux/wave-2-cluster-cascade
```
- [ ] **Step 2:** Open PR + auto-merge:
```bash
gh pr create --base main --head global-ux/wave-2-cluster-cascade \
  --title "global-ux: Wave 2 cascade — Plan 2 Task 3.5 across 5 clusters" \
  --body "5 clusters, 14-17 packages, 5 path-scoped commits. Reviewed by 5 parallel reviewer subagents (Wave 3). Per-cluster reports in waves/global-ux/wave-2-cluster-*-report.md and reviews in waves/global-ux/wave-3-cluster-*-review.md. Plan: docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md"
gh pr merge --auto --squash
```
- [ ] **Step 3:** Record PR URL in tracker. Wait for merge before advancing.

**Wave 3 exit:** Five reviews GREEN; PR merged; local `main` synced.

---

## Wave 4 — Plan 5 entry conditions

**Pre-condition:** Wave 3 exit gate passed.

**Why:** Plan 2 Task 3.6 (cascade coverage report) and Task 4.5 (integration report + go/no-go for Plan 5) are the remaining Plan 2 deliverables. With Wave 2 cascade done, these become writable.

### Task 4.1: Author cascade coverage report

**Files:**
- Create: `waves/global-ux/week-3-cascade-coverage-report.md`

- [ ] **Step 1:** Synthesize from the five Wave-2 cluster reports + Wave-3 reviews. Sections: (a) packages covered (table: package, cluster, commit SHA, ar-SA entry count), (b) packages deferred (with reason — typically TypeScript adapters out of cascade pattern), (c) binary gates per Plan 2 Task 3.6: `grep -r 'AddLocalization()' packages/ apps/ accelerators/` ≥ 3 (already met — foundation+bridge+anchor); every `packages/blocks-*` has `Resources/Localization/SharedResource.resx`.

- [ ] **Step 2:** Run the gate commands and paste output verbatim.

- [ ] **Step 3:** Verdict block: PASS / PASS-WITH-DEFERRALS / FAIL.

### Task 4.2: Refresh status.md with Plan 5 entry verdict

**Files:**
- Modify: `waves/global-ux/status.md`

- [ ] **Step 1:** Update `## Plans authored` Plan 2 row Status column: `Wk 2-4 complete (cascade landed Wave 2; reports in week-3-cascade-coverage-report.md)`.

- [ ] **Step 2:** Add `## Plan 5 entry verdict` section. Reference Plans 3 / 4 / 4B status from Wave 1 reports. If any of those are not GREEN, mark Plan 5 as BLOCKED-ON-<plan-N> with named blockers; if all green, mark Plan 5 as READY-TO-DISPATCH.

### Task 4.3: Ship Wave 4 PR + close out

**Files:**
- (commit + PR only)

- [ ] **Step 1:**
```bash
git switch -c global-ux/wave-4-plan-5-entry
git add waves/global-ux/week-3-cascade-coverage-report.md waves/global-ux/status.md waves/global-ux/reconciliation-loop-tracker.md
git commit -m "docs(global-ux): Plan 2 close-out + Plan 5 entry verdict"
git push -u origin global-ux/wave-4-plan-5-entry
gh pr create --base main --head global-ux/wave-4-plan-5-entry \
  --title "global-ux: Wave 4 — Plan 2 close-out" \
  --body "Cascade coverage report + Plan 5 entry verdict. Plan: docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md"
gh pr merge --auto --squash
```

- [ ] **Step 2:** Mark tracker `Current wave: DONE`. Do not `ScheduleWakeup` again.

- [ ] **Step 3:** Final report to user: tracker DONE; four PRs merged; Plan 5 entry status (READY-TO-DISPATCH or BLOCKED-ON-X). Loop terminates.

**Wave 4 exit:** Tracker DONE; loop terminates cleanly.

---

## Verification

### Automated (driver runs each iteration)

- `dotnet build` on touched packages (Wave 2 subagent self-checks; Wave 3 reviewer re-verifies)
- `grep -r 'AddLocalization()' packages/ apps/ accelerators/` ≥ 3 (Plan 2 Task 3.6 gate; Wave 4)
- `find packages/blocks-* -path '*/Resources/Localization/SharedResource.resx' | wc -l` == 14 (Wave 4 gate)
- `gh pr view <#> --json mergedAt -q .mergedAt` non-null (each wave's PR closure)
- `git log main --oneline | head -10` shows expected commit cadence

### Manual (user reviews at wave gates)

- After Wave 0 PR opens: user reviews reconciliation memo classification before merge
- After Wave 1 PR opens: user spot-checks status.md against actual recent work
- After Wave 3 reviews land: user spot-checks one of the five cluster commits (random sample)
- After Wave 4 verdict: user decides whether to dispatch Plan 5 immediately or pause

### Ongoing observability

- Tracker iteration log appended every wake-up — readable at any time
- Each subagent report committed before driver advances — `git log waves/global-ux/` shows progress
- Each PR is a CI gate; failure = merge blocked = loop halts

---

## Conditional sections

### Rollback Strategy

- **Per wave:** each wave ships in its own PR. Rollback = `gh pr revert <#>` for the affected wave. Loop driver re-reads tracker on next wake; if Wave-N is reverted, tracker rolls back to Wave-(N-1).
- **Loop runaway:** if driver gets stuck in a wake/halt cycle ≥3 iterations on same wave, user can write `Current wave: HALT-USER` in tracker; driver respects and exits.
- **Subagent budget runaway:** if MCP / Claude Code quota hits per memory `feedback_sleep_on_quota_exhaustion` or `feedback_sleep_on_claude_code_token_exhaustion`, driver halts in-flight new dispatches, lets in-flight subagents finish, `ScheduleWakeup` 3600s past reset.

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Wave 2 cluster D (TS adapters) has no .NET cascade pattern | High | Medium | Wave 2 Task 2.0 detects pattern divergence; cluster D documents deferral, doesn't fake cascade |
| Reviewer subagents disagree on cascade quality | Medium | Medium | Decision matrix in Wave 3 Step 3 named explicitly; ≥2 RED halts loop, single RED escalates per cluster |
| PR auto-merge wedges on flaky CI | Medium | Low | gh pr merge --auto retries on next push; loop wakes 1800s and re-checks |
| Plan 3 / 4 / 4B status check finds zero progress | Medium | High to Plan 5 timeline | Wave 1 surfaces this immediately; Wave 4 verdict marks Plan 5 BLOCKED-ON-X with named owner; not this loop's job to start them |
| Subagent invents a non-existent file or commit SHA | Low | High | Wave 2 Step 3 verifies SHAs locally; Wave 3 reviewer reads files and cross-checks; tracker requires SHA evidence |
| Loop iterations consume token budget faster than work completes | Low (Max Pro) | Low | `ScheduleWakeup` reasoning explicitly considers cache TTL; default 1200-1800s avoids 5-min cache-miss cliff |

### Dependencies & Blockers

- **External:** `gh` CLI authenticated; `dotnet` SDK 11 preview present; Weblate stack out-of-scope (Plan 3).
- **Internal:** Wave 1 depends on Wave 0; Wave 2 depends on Wave 1; Wave 3 depends on Wave 2 commits; Wave 4 depends on Wave 3 reviews. Strict ordering.
- **Plan 3/4/4B:** This plan does not advance them; only reports their status (Wave 1) and signals their blocking effect on Plan 5 (Wave 4). If user wants Plan 3/4/4B advanced, that's a separate loop.

### Delegation & Team Strategy

| Wave | Driver work | Subagent fan-out | Reviewer fan-out |
|---|---|---|---|
| 0 | All (sequential, manual judgement) | 0 | 0 (user reviews PR) |
| 1 | Synthesis only | 4 parallel | 0 (user reviews PR) |
| 2 | Pattern discovery + brief authoring | 5 parallel | 0 (deferred to Wave 3) |
| 3 | Decision matrix + PR open | 0 | 5 parallel |
| 4 | All (synthesis + PR open) | 0 | 0 (user reviews PR) |

Maximum parallelism: 5 subagents simultaneously (Wave 2 cluster dispatch and Wave 3 reviewer dispatch). Driver-budget cost: ~10 driver iterations × ~2k tokens each + ~14 subagent invocations × ~30-50k tokens each = ~600k-800k tokens total budget for the full plan. Well within Max Pro daily envelope.

### Incremental Delivery

Every wave ships its own PR. Even if loop halts at Wave 2 RED, the value delivered (Wave 0 reconciliation, Wave 1 truth refresh) is already on `main` and useful standalone. No "all-or-nothing" delivery.

### Reference Library

- Plan 2 (the source plan being cascaded): `docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md`
- Plan 2 Task 3.5 cluster definitions (where the original cluster boundaries were proposed): line 277 of Plan 2
- Sunfish coding standards: `_shared/engineering/coding-standards.md`
- Diagnostic codes: `docs/diagnostic-codes.md` (`SUNFISH_I18N_001`)
- Spec §3A (loc-infra requirements): `docs/superpowers/specs/2026-04-24-global-first-ux-design.md` §3A
- ADR 0034 Amendment 1 (Node↔.NET contract bridge — relevant if cluster D pivots to TS): `docs/adrs/0034-...`
- PR-with-auto-merge memory rule: `feedback_pr_push_authorization`
- Pre-release latest-first policy: `project_pre_release_latest_first_policy.md`

### Learning & Knowledge Capture

After Wave 4 close-out, append to `.wolf/cerebrum.md`:
- "Cluster pattern X applied to ~14 packages produces Y per-cluster review noise" — calibration for next cascade
- Any subagent failure modes observed (stuck dispatches, fake SHAs, scope creep) — feeds into next plan's brief tightening

### Replanning Triggers

- Wave 2 cluster pattern divergence forces cluster split → re-plan Wave 2 only
- Wave 3 systemic RED → re-plan Wave 2 entirely (cascade pattern broken)
- Plan 3 / 4 / 4B status reveals deeper Phase-1 schedule risk → escalate; this plan doesn't fix it
- User signals priority shift (e.g., wants compat-package expansion per memory `project_compat_expansion_workstream`) → halt loop with `Halt reason: user-priority-shift`

---

## Cold Start Test

A fresh agent with no prior context can resume this plan by:

1. Reading `waves/global-ux/reconciliation-loop-tracker.md` → finds `Current wave: N` and last iteration log.
2. Reading this plan file → finds Wave-N task block with full pre-conditions, dispatch instructions, and exit gate.
3. Re-fetching git: `git fetch origin && git status` → confirms branch state.
4. Picking up at the first un-checked step in Wave-N's task block.
5. If `Current wave: RED`, reading `Halt reason` and stopping; do not auto-recover.

The driver MUST update tracker after EVERY wake (even if no progress made — log "blocked-on: X" row) so cold-resume is unambiguous.

---

## Self-Review

**Spec coverage:** Five waves cover the five next-steps from the prior turn (reconcile, status truth, cascade, gate, Plan-5-entry). ✓

**Placeholder scan:** Searched for "TBD", "TODO", "implement later" — none. Searched for "appropriate" / "handle edge cases" — none. ✓

**Type consistency:** Tracker schema, file paths, and commit messages cross-referenced; no naming drift between waves. ✓

**Plan-2 traceability:** Wave 2 Task 2.0-E maps to Plan 2 Tasks 3.4-3.5; Wave 3 maps to Plan 2 Task 3.5 Step 4 reviewer gate; Wave 4 Task 4.1-4.2 maps to Plan 2 Tasks 3.6 + 4.5. ✓
