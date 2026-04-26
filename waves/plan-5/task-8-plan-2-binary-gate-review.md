# Plan 5 Task 8 Review — Plan 2 binary gate

**Reviewed code commit:** `cfad77b3`
**Reviewed report commit:** `a7804424`
**Branch:** `worktree-agent-abac662ff4b6dfc9c`

---

## Per-criterion results

### (a) Diff-shape — exactly the 3 expected paths
**PASS.** `git show --name-only cfad77b3 | sort` returns exactly:
- `.github/workflows/global-ux-gate.yml`
- `tooling/plan-2-binary-gate/README.md`
- `tooling/plan-2-binary-gate/check.sh`

No stray paths.

### (b) `check.sh` content assertions
**PASS with one minor note.**
- Shebang: `#!/usr/bin/env bash` (line 1) — correct.
- `set -euo pipefail` present (line 2) — correct.
- `BLOCKS_RESX_COUNT >= 14` asserted via `EXPECTED_BLOCKS_RESX=14` and informative failure listing missing packages by iterating `for d in packages/blocks-*` — correct.
- `ADDLOC_COUNT >= 3` asserted via `MIN_ADDLOC_CALLSITES=3` — correct.
- All shell variables quoted in test expressions (`"$BLOCKS_RESX_COUNT"`, `"$EXPECTED_BLOCKS_RESX"`, `"$ADDLOC_COUNT"`, `"$MIN_ADDLOC_CALLSITES"`, `"$d"`) — no shell-injection vector.
- **Executable bit:** `git ls-tree` shows `100644` (NOT `100755`). The workflow invokes via `bash tooling/plan-2-binary-gate/check.sh`, so the missing exec-bit does not break CI. However, the brief explicitly required "Executable bit set." Categorize as **minor non-conformance** (cosmetic; functionally inert because of the explicit `bash` invoker).

### (c) Shell syntax check
**PASS.** `bash -n /c/Projects/sunfish/.claude/worktrees/agent-abac662ff4b6dfc9c/tooling/plan-2-binary-gate/check.sh` returns clean (SYNTAX OK).

### (d) Live execution of the gate
**PASS.** Output:
```
Plan 2 binary gate: PASS
  blocks-* SharedResource.resx: 14 / 14
  services.AddLocalization() call sites: 12 (min 3)
```
Confirmed `ls packages/ | grep ^blocks- | wc -l` = 14, matching the gate's count. AddLocalization() found at 12 call sites (well above min 3).

### (e) Workflow YAML modification — appended `plan-2-binary-gate` job
**PASS.** New job block:
```yaml
plan-2-binary-gate:
  name: Plan 2 binary gate
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v6
    - name: Run Plan 2 binary gate
      run: bash tooling/plan-2-binary-gate/check.sh
```
Job is well-formed, uses pinned `actions/checkout@v6`, and runs the gate with bash explicitly (which neutralizes the missing exec-bit from criterion b).

The aggregator deviation is captured in the dedicated section below.

### (f) Commit message contains `plan-5-task-8` token
**PASS.** Subject line: `feat(ci): plan-5-task-8 — Plan 2 Task 3.6 binary gate as permanent CI assertion`. Body also contains `Token: plan-5-task-8`.

### (g) Diff-shape — only the 3 paths
**PASS.** `git show --stat cfad77b3` confirms exactly 3 files, +100/-1 lines. No bleed into `_shared/`, `packages/`, or other ICM directories.

---

## Aggregator-needs deviation evaluation

The subagent appended `plan-2-binary-gate` to the aggregator job's `needs:` list:
```yaml
global-ux-gate:
  name: Global-UX Gate (aggregate)
  runs-on: ubuntu-latest
  needs: [css-logical, locale-completeness, a11y-storybook, plan-2-binary-gate]
```

**Verdict: ACCEPTABLE.**

Reasoning:
1. The brief itself flagged this deviation and provided the rationale: "the gate's purpose is permanence; without aggregator inclusion, it could fail without blocking merge."
2. The aggregator pattern is the canonical Sunfish branch-protection idiom (single required check rather than N entries), so omitting the new gate from `needs:` would have been the actual bug — it would create a "decorative" gate that fires but doesn't block.
3. The change is minimal (one array element) and reversible.
4. Symmetric with how `css-logical`, `locale-completeness`, and `a11y-storybook` are wired — the new gate is now a peer, not an outlier.
5. Aligns with the task framing ("permanent CI assertion") — permanence requires enforcement, enforcement requires aggregator membership.

The deviation is well-justified and improves correctness over a literal reading of "appended a job."

---

## Issues identified

### Minor (Suggestion-tier)

1. **Executable bit not set on `check.sh`** (criterion b). The brief required `chmod +x`. Currently `100644`. Functionally harmless because the workflow uses `bash …/check.sh`, but if any future caller invokes `./check.sh` directly (common pattern in local dev / pre-commit hooks) it will fail. Recommend a follow-up `git update-index --chmod=+x tooling/plan-2-binary-gate/check.sh` in a trivial fixup commit. Non-blocking.

### What was done well

- Clean diff-shape — surgical, no scope creep.
- Informative failure mode for missing blocks-* RESX (lists offenders rather than just printing a count delta).
- All shell variables quoted — defensively written.
- README.md included alongside `check.sh` (not in checklist but good hygiene).
- Correct handling of the aggregator-needs question — the subagent reasoned about *intent* (permanence ⇒ enforcement) rather than just executing the literal brief.
- Comment in workflow `Plan 5 — Global-First UX gate workflow` and Plan-2 references in `check.sh` keep traceability tight.
- `tr -d ' '` after `wc -l` correctly handles the BSD/GNU `wc` whitespace inconsistency — small but signals shell-portability awareness.

---

## Final verdict: GREEN

The commit cleanly satisfies all functional criteria. The single non-conformance (missing exec-bit) is cosmetic given the workflow's `bash …/check.sh` invocation pattern. The aggregator-needs deviation is well-reasoned and strictly improves the implementation. Recommend a trivial fixup for the exec-bit if local/pre-commit invocation is anticipated, but this does not block merge.
