# Plan 5 Task 7 Review — cross-plan health gate

**Code commit:** `84b5f83e` on branch `worktree-agent-a7aef2dc3833dfbd5`
**Report commit:** `45851046` (data-only, not trusted)
**Reviewer:** Senior Code Reviewer agent
**Date:** 2026-04-25

---

## Per-criterion results

### (a) Diff shape — files in commit `84b5f83e`
PASS. `git show --name-only 84b5f83e | sort -u` returns exactly the 5 expected paths and nothing else:

- `.github/workflows/global-ux-gate.yml`
- `tooling/cross-plan-health/README.md`
- `tooling/cross-plan-health/check.mjs`
- `tooling/cross-plan-health/package.json`
- `tooling/cross-plan-health/tests/parser.test.mjs`

### (b) Tests — `tests/parser.test.mjs`
PASS. File contains exactly 3 tests as specified:
1. `parser extracts plan verdicts from markdown table` — asserts `parseStatusTable` extracts 2 rows, plan names, and verdicts.
2. `evaluateHealth flags RED plans` — asserts `healthy === false`, `redPlans === ['Plan 3']`.
3. `evaluateHealth passes when all GREEN/YELLOW` — asserts `healthy === true`.

### (c) `check.mjs` shape
PASS. `parseStatusTable()` is a pure markdown-table parser walking under the `## Plans authored` heading, terminating at the next `##`. `evaluateHealth()` filters for `verdict === 'RED'` and reports them. Status file is read via `readFileSync(path, 'utf8')` — no `child_process`, no `exec`, no shell interpolation. CLI argument is treated only as a filesystem path. Module is dual-purpose: imports are pure (exports), CLI behavior is gated on `process.argv[1] === fileURLToPath(import.meta.url)`, which keeps the test suite from triggering the CLI side effect.

### (d) Test execution
PASS. `node --test tooling/cross-plan-health/tests/parser.test.mjs` from the worktree:
```
✔ parser extracts plan verdicts from markdown table (0.83ms)
✔ evaluateHealth flags RED plans (0.11ms)
✔ evaluateHealth passes when all GREEN/YELLOW (0.07ms)
ℹ tests 3   pass 3   fail 0
```

### (e) Live invocation against current `status.md`
PASS. `node tooling/cross-plan-health/check.mjs waves/global-ux/status.md`:
```
Cross-plan health: RED
  Plan 1: UNKNOWN
  Plan 2: GREEN
  Plan 3: RED
  Plan 4: YELLOW
  Plan 4B: YELLOW
  Plan 5: GREEN
  Plan 6: UNKNOWN

SUNFISH_PLAN_HEALTH: 1 RED plan(s): Plan 3
Surface to human owner; consider re-prioritization gate per Plan 5 spec.
```
Exit code: `1`. Message string matches the brief verbatim. Plan 3 is correctly flagged as the sole RED plan; Plan 4/4B YELLOWs and Plan 1/6 UNKNOWNs do not trigger the gate (per spec — only RED gates).

### (f) Workflow YAML modifications
PASS.

- New job `cross-plan-health` (lines 104-114) — runs `node tooling/cross-plan-health/check.mjs waves/global-ux/status.md`.
- Job-level guard: `if: github.event_name == 'push' || github.event_name == 'schedule'` (line 107) — matches the brief's `if: schedule || push` intent.
- `continue-on-error: true` (line 108) with the explicit informational-baseline rationale comment.
- Schedule trigger added to `on:` block: `cron: '0 12 * * 1'` (lines 16-17) with explanatory comment "Monday noon UTC — weekly cross-plan health drift check (Plan 5 Task 7)."
- Pull-request `paths:` filter additionally extended with `tooling/cross-plan-health/**` and `waves/global-ux/status.md` so PR runs trigger when either the tool or the source-of-truth changes — sensible additive scope.

### (g) 7-day grace deviation — critical evaluation
ACCEPTABLE. The brief acknowledges the deviation. Subagent's reasoning is sound: `waves/global-ux/status.md`'s `## Plans authored` table has columns `Plan | Scope | Weeks | Lines | Status` — there is no per-row last-update timestamp to parse, so a 7-day grace would require either (i) a separate timestamp column that doesn't exist, (ii) git-blame inspection per row (fragile and slow), or (iii) a sidecar JSON ledger (out of scope for v1). The "exit 1 on ANY RED" behavior is **strictly stronger** than the brief's title intent: it surfaces RED faster (immediately) than the proposed grace window, and the human owner can dismiss false-positives in their weekly Monday review. `continue-on-error: true` ensures the stricter posture cannot block CI during the 2-week baseline. The README also documents the verdict-precedence rules transparently, so future v2 work to add a 7-day grace has a clean extension point. No correctness regression; v2 ticket should be filed if Plan-3-style stalls become a recurring pattern that warrants the grace.

### (h) Aggregator `needs:` exclusion
PASS. Line 122: `needs: [css-logical, locale-completeness, a11y-storybook]`. `cross-plan-health` is correctly omitted from the required-aggregator set, consistent with the `continue-on-error: true` informational-baseline posture. (Note: `resx-xss-scan` is also absent from this aggregator on this branch, but that's expected — the agent branched before Plan 5 Task 6 merged, and the rebase/merge will reconcile when the PR opens.)

### (i) Commit message token
PASS. Subject line: `feat(ci): plan-5-task-7 — cross-plan health gate (Wave 1 finding carry-forward)`. Body includes explicit `Token: plan-5-task-7` line.

### (j) Diff-shape (no extras)
PASS. No `.wolf/` updates, no stray docs, no incidental refactors. Five files only — same set as (a). Clean change footprint.

---

## 7-day grace deviation evaluation

**ACCEPTABLE.** The behavioral substitution (immediate-RED-flag rather than 7-day-stall-flag) is strictly more conservative, well-rationalized in the subagent's reasoning, and risk-bounded by `continue-on-error: true` for the 2-week baseline. The `status.md` source-of-truth genuinely lacks the per-row timestamp data needed to implement the grace correctly, and adding such timestamps is itself a separate plan-design change that belongs in a follow-up. README documents the precedence rules transparently so a v2 grace feature has a clean extension surface. No reviewer concern; recommend tracking a v2 follow-up only if Plan-3-style stalls recur often enough that the noise-floor justifies the grace logic.

---

## Final verdict: GREEN
