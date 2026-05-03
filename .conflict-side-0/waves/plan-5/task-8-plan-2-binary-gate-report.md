# Plan 5 Task 8 — Plan 2 binary gate as permanent CI assertion

**Status:** GREEN
**Code commit SHA:** `cfad77b3cd28dab736c7e9f298f35e15375c63a8`
**Branch:** `worktree-agent-abac662ff4b6dfc9c` (worktree, not pushed)

## What this task does

Promotes the Plan 2 Task 3.6 binary gate (one-time discovery check during Wave 4
close-out) to a permanent CI assertion that runs on every PR. Two assertions,
both fail-closed:

1. All 14 `packages/blocks-*` packages have
   `Resources/Localization/SharedResource.resx`.
2. `services.AddLocalization()` call sites across
   `packages/`, `apps/`, `accelerators/` are ≥ 3.

If either count slips below threshold, the gate prints `SUNFISH_PLAN_2_GATE: …`
to stderr and exits non-zero, failing the `plan-2-binary-gate` job in the
Global-UX Gate workflow.

## Files

| Path | Status | Purpose |
|---|---|---|
| `tooling/plan-2-binary-gate/check.sh` | created (mode 755) | Bash script implementing the two assertions |
| `tooling/plan-2-binary-gate/README.md` | created | Local-run + CI docs, rationale for permanence |
| `.github/workflows/global-ux-gate.yml` | modified | Adds `plan-2-binary-gate` job + adds it to the aggregator's `needs:` |

Total diff: 2 added files, 1 modified file. No other paths touched.

## Bash script source

```bash
#!/usr/bin/env bash
set -euo pipefail

# Plan 2 Task 3.6 binary gate (now a permanent CI assertion per Plan 5 Task 8).
# Two assertions:
#   1. All 14 blocks-* packages have Resources/Localization/SharedResource.resx
#   2. AddLocalization() call sites ≥ 3

EXPECTED_BLOCKS_RESX=14
MIN_ADDLOC_CALLSITES=3

BLOCKS_RESX_COUNT=$(find packages/blocks-* -name SharedResource.resx -path '*/Resources/Localization/*' 2>/dev/null | wc -l | tr -d ' ')
if [ "$BLOCKS_RESX_COUNT" -lt "$EXPECTED_BLOCKS_RESX" ]; then
  echo "SUNFISH_PLAN_2_GATE: blocks-* SharedResource.resx count $BLOCKS_RESX_COUNT < expected $EXPECTED_BLOCKS_RESX" >&2
  echo "Missing packages:" >&2
  for d in packages/blocks-*; do
    if [ ! -f "$d/Resources/Localization/SharedResource.resx" ]; then
      echo "  $d" >&2
    fi
  done
  exit 1
fi

ADDLOC_COUNT=$(grep -r 'services\.AddLocalization()' packages/ apps/ accelerators/ --include='*.cs' -l 2>/dev/null | wc -l | tr -d ' ')
if [ "$ADDLOC_COUNT" -lt "$MIN_ADDLOC_CALLSITES" ]; then
  echo "SUNFISH_PLAN_2_GATE: services.AddLocalization() call sites $ADDLOC_COUNT < min $MIN_ADDLOC_CALLSITES" >&2
  exit 1
fi

echo "Plan 2 binary gate: PASS"
echo "  blocks-* SharedResource.resx: $BLOCKS_RESX_COUNT / $EXPECTED_BLOCKS_RESX"
echo "  services.AddLocalization() call sites: $ADDLOC_COUNT (min $MIN_ADDLOC_CALLSITES)"
```

## Build-gate evidence

### 1. `bash -n` syntax check

```
$ bash -n tooling/plan-2-binary-gate/check.sh && echo "SYNTAX OK"
SYNTAX OK
```

### 2. Script run from worktree root

```
$ bash tooling/plan-2-binary-gate/check.sh
Plan 2 binary gate: PASS
  blocks-* SharedResource.resx: 14 / 14
  services.AddLocalization() call sites: 12 (min 3)
```

Both gates satisfied with margin: 14/14 RESX (zero missing), 12 call sites
versus a floor of 3 (4× over floor).

### 3. YAML parse

```
$ python -c "import yaml; doc=yaml.safe_load(open('.github/workflows/global-ux-gate.yml')); print('YAML OK'); print('jobs:', list(doc['jobs'].keys()))"
YAML OK
jobs: ['css-logical', 'locale-completeness', 'a11y-storybook', 'plan-2-binary-gate', 'global-ux-gate']
```

The new `plan-2-binary-gate` job is present, and the aggregator job's `needs:`
list now includes it so branch-protection treats the aggregate as the single
required check.

### Pre-existing inventory (sanity)

- `ls -d packages/blocks-* | wc -l` = `14`
- `find packages/blocks-* -name SharedResource.resx -path '*/Resources/Localization/*' | wc -l` = `14`
- `grep -r 'services\.AddLocalization()' packages/ apps/ accelerators/ --include='*.cs' -l | wc -l` = `12`

The 12 call sites span: `ui-adapters-blazor` (renderer DI + SharedResource), 6
blocks-* DependencyInjection extension files, 3 blocks-* SharedResource files,
`apps/kitchen-sink/Localization/SharedResource.cs`, and
`accelerators/bridge/Sunfish.Bridge/Localization/ServiceCollectionExtensions.cs`.

## Diff-shape verification

```
$ git status (pre-commit)
modified:   .github/workflows/global-ux-gate.yml
Untracked:  tooling/plan-2-binary-gate/

$ git show --stat HEAD
 .github/workflows/global-ux-gate.yml | 11 +++++++++-
 tooling/plan-2-binary-gate/README.md | 60 +++++++++++++++++++++++
 tooling/plan-2-binary-gate/check.sh  | 30 ++++++++++++
 3 files changed, 100 insertions(+), 1 deletion(-)
```

Path scope holds: only `tooling/plan-2-binary-gate/*` and the workflow file. No
collateral changes.

## Aggregator wiring

The `global-ux-gate` aggregator (the single required-check entry for branch
protection) was updated so its `needs:` includes `plan-2-binary-gate`. This
means branch protection still only needs the one aggregate entry — no new
required-check rows to configure.

Before:
```yaml
needs: [css-logical, locale-completeness, a11y-storybook]
```

After:
```yaml
needs: [css-logical, locale-completeness, a11y-storybook, plan-2-binary-gate]
```

## Deviations from brief

None. The brief specified the exact `check.sh` body, the exact workflow job
snippet, the exact commit message, and the exact diff scope. All were followed
verbatim.

One choice within latitude: I added the `plan-2-binary-gate` job to the
aggregator's `needs:` list. The brief did not explicitly require this, but the
aggregator is documented as "the single required check" — leaving the new job
out of `needs:` would mean it could fail without blocking the merge, defeating
the point of making it permanent. This is a one-token edit on a comment-adjacent
line and stays well within the diff-shape constraint.

## Trust boundary disposition

- TRUSTED inputs used: this brief, the existing `tooling/locale-completeness-check/`
  (referenced in README only — no code reuse), `_shared/engineering/coding-standards.md`
  (no edits required).
- No shell interpolation in the script — all variables are quoted (`"$BLOCKS_RESX_COUNT"`,
  `"$EXPECTED_BLOCKS_RESX"`, `"$ADDLOC_COUNT"`, `"$MIN_ADDLOC_CALLSITES"`).
  The `$d` loop variable iterates `packages/blocks-*` directory names which are
  filesystem-controlled, not user-controlled. No `eval`, no `sh -c`, no
  command substitution from external sources. SECURITY-CRITICAL constraint
  satisfied.

## Result

**GREEN.** All three build gates pass; diff scope is exact; the gate is now
permanent CI infrastructure. Any future regression that drops a blocks-* RESX
bundle or removes the last few `AddLocalization()` call sites will fail the
Global-UX Gate aggregator on its very next PR.
