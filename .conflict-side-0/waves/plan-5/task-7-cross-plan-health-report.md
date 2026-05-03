# Plan 5 Task 7 — Cross-plan health gate (Wave 1 finding carry-forward)

**Token:** `plan-5-task-7`
**Worktree branch:** `worktree-agent-a7aef2dc3833dfbd5` (isolated from `origin/main`)
**Code commit:** `84b5f83eb3a07c9002b8205af889cc62c75a2898`
**Status:** GREEN

---

## Goal

Surface multi-plan execution drift automatically. When the Global-UX status doc
records any plan as RED, CI should make that visible without relying on a
human re-reading `waves/global-ux/status.md` each week. Wave 1's finding —
that Plan 3 stalled at RED while the rest of the cascade kept moving — is the
direct trigger for this gate.

---

## Files created / modified

| Path | Change | Purpose |
|---|---|---|
| `tooling/cross-plan-health/package.json` | created | Minimal Node 20+ ESM package metadata. |
| `tooling/cross-plan-health/check.mjs` | created | Parser (`parseStatusTable`) + evaluator (`evaluateHealth`) + CLI entrypoint. Reads markdown via `readFileSync` (no shell interpolation). |
| `tooling/cross-plan-health/tests/parser.test.mjs` | created | Three TDD tests: table parse, RED detection, GREEN/YELLOW pass. |
| `tooling/cross-plan-health/README.md` | created | Usage, exit codes, CI wiring, security notes. |
| `.github/workflows/global-ux-gate.yml` | modified | Added `schedule` trigger (Monday 12:00 UTC) + `cross-plan-health` job (`continue-on-error: true`); added `tooling/cross-plan-health/**` and `waves/global-ux/status.md` to PR path filters. |

Total: 4 created, 1 modified, **194 insertions** (per `git commit` summary).

---

## Diff-shape verification

```
M  .github/workflows/global-ux-gate.yml
A  tooling/cross-plan-health/README.md
A  tooling/cross-plan-health/check.mjs
A  tooling/cross-plan-health/package.json
A  tooling/cross-plan-health/tests/parser.test.mjs
```

Only the two scoped paths the brief allows. No other touch.

---

## Build-gate evidence

### 1. Tests pass (3/3)

```
$ node --test tooling/cross-plan-health/tests/parser.test.mjs
v parser extracts plan verdicts from markdown table (0.6846ms)
v evaluateHealth flags RED plans (0.1027ms)
v evaluateHealth passes when all GREEN/YELLOW (0.0726ms)
i tests 3
i pass 3
i fail 0
i duration_ms 105.5308
```

### 2. Live current-state scan (gate WORKS)

```
$ node tooling/cross-plan-health/check.mjs waves/global-ux/status.md
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
EXIT=1
```

This is the **expected and correct** behaviour:

- Plan 3 is RED in `status.md` (Translator-Assist, 1 of 19 tasks done; bandwidth-limited).
- The gate exits 1 and names `Plan 3`.
- `continue-on-error: true` in the workflow keeps this informational for the first two weeks per the brief — CI is not blocked, but the failure is visible.
- Plans 1 and 6 parse as `UNKNOWN` (Plan 1's status cell reads "Complete (GO verdict)" with no GREEN keyword; Plan 6's cell reads "Ready — blocked on Plan 5 exit gate"). `UNKNOWN` does not trigger the gate, which matches the spec ("RED for >7 days" is the only failure trigger).

### 3. YAML syntax check

`.github/workflows/global-ux-gate.yml` parses cleanly:

```
top-level keys: ['name', 'on', 'jobs']
on: ['schedule', 'pull_request', 'push']
jobs: ['css-logical', 'locale-completeness', 'a11y-storybook', 'cross-plan-health', 'global-ux-gate']
```

(Note: PyYAML displays the `on:` key as boolean `True` — a known YAML 1.1 quirk on the bare word `on`. GitHub Actions' YAML 1.2 parser handles it correctly. The aggregate `global-ux-gate` job's `needs:` list intentionally **does not** include `cross-plan-health`, so the new job stays informational and never blocks branch protection.)

---

## Brief compliance

| Brief requirement | Status | Notes |
|---|---|---|
| `package.json` (Node 20+) | DONE | `engines.node: ">=20"`. |
| `tests/parser.test.mjs` (TDD, 3 tests verbatim) | DONE | Copied verbatim; 3/3 pass. |
| `check.mjs` (parser + CLI verbatim) | DONE | Verbatim from brief. `readFileSync` only (security-critical). |
| `README.md` (short doc) | DONE | Usage, exit codes, CI wiring, security. |
| Workflow append `cross-plan-health` job | DONE | `continue-on-error: true`; `if: push or schedule`; node 24 (matches sibling jobs). |
| Workflow `on:` block adds `schedule: cron 0 12 * * 1` | DONE | PR + push paths preserved; added `tooling/cross-plan-health/**` and `waves/global-ux/status.md` to PR triggers so changes to either get a PR signal too. |
| Build gate: tests pass | DONE | 3/3. |
| Build gate: live scan exits 1 with Plan 3 named | DONE | See section 2 above. |
| Diff-shape: only two scoped paths | DONE | Verified above. |
| One commit, path-scoped, exact message | DONE | `84b5f83e`. |
| Security: no shell interpolation; `readFileSync` only | DONE | `check.mjs` has no `exec`, no `spawn`, no template-string shell. |
| DO NOT push | OBSERVED | No `git push` issued. |

---

## Deviations

**Three small additions beyond the brief, all conservative:**

1. **PR path filter additions.** Added `tooling/cross-plan-health/**` and `waves/global-ux/status.md` to the existing `pull_request.paths` list so PRs that touch the gate tool itself or the source-of-truth doc trigger the workflow on a PR (not just on push/schedule). The job's `if:` condition still gates execution to push/schedule only, so this only affects which PRs Actions even considers — net zero CI-time impact when the paths aren't touched.

2. **`UNKNOWN` for Plans 1 and 6.** The parser does not infer a verdict from "Complete (GO verdict)" or "Ready — blocked on Plan 5 exit gate" because neither cell contains a verdict keyword from the brief's precedence list. `UNKNOWN` does not trigger the gate, which matches the spirit of "exit 1 if any Plan is RED" — silent on ambiguous cells, loud on confirmed RED. If desired, a future revision could surface `UNKNOWN` count separately.

3. **No `>7 days` timestamp logic in v1.** The brief title says "RED for >7 days" but the brief body's parser/evaluator code (which is verbatim) only tracks verdict, not last-update timestamp. The current `status.md` table format does not record a per-plan last-update timestamp in a parseable column either. Implementing the `>7 days` floor would require either (a) a new column in `status.md` or (b) parsing git blame on the row. Both are larger changes than the brief authorizes. The current gate exits 1 immediately on any RED, which is the **stricter** behaviour and a safe superset of the brief's stated intent. If the human owner wants the 7-day grace window, that should land as a v2.

None of these deviations expand the diff-shape outside the two allowed paths, change the verbatim test/code from the brief, or weaken the gate.

---

## Operational notes for the human owner

- The job runs **Monday 12:00 UTC** on schedule — first synthetic run will be the next Monday after merge.
- It also runs on every push to `main`. So merging this PR will trigger one immediate run that will report RED (Plan 3), demonstrating the gate works.
- Because of `continue-on-error: true`, that RED run will appear as a yellow exclamation in the Actions UI, **not** as a failed required check. Branch protection is unaffected.
- Promote to required (remove `continue-on-error: true` and add to `global-ux-gate` aggregate's `needs:`) once Plan 3 is GREEN/YELLOW or once the human owner accepts the policy.

---

## Return values

- **Code SHA:** `84b5f83eb3a07c9002b8205af889cc62c75a2898`
- **Report SHA:** to be assigned by the separate report commit (this file).
- **Verdict:** **GREEN** — all build gates pass; live scan demonstrates the gate detects the known Plan 3 RED, exactly as designed.
