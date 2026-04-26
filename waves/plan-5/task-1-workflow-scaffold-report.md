# Plan 5 Task 1 — Global-UX Gate Workflow Scaffold Report

**Status:** GREEN
**Branch:** `worktree-agent-a27a983a5612c276c`
**File touched:** `.github/workflows/global-ux-gate.yml` (single-file diff)

---

## What changed

Extended the existing `.github/workflows/global-ux-gate.yml` (already contained
`css-logical`, `locale-completeness`, `a11y-storybook`, and `global-ux-gate`
aggregator from Plan 4/Plan 1) with **4 new gate jobs** plus refreshed trigger
paths and an updated aggregator `needs` list. `locale-completeness` was already
present per Plan 1 — verified, no changes required to that job.

| Job                | Added? | runs-on         | Notes                                                            |
| ------------------ | ------ | --------------- | ---------------------------------------------------------------- |
| `analyzers`        | NEW    | windows-latest  | `dotnet build Sunfish.slnx -c Release -warnaserror`              |
| `xliff-round-trip` | NEW    | ubuntu-latest   | `dotnet test` on LocalizationXliff test project                  |
| `a11y-audit`       | NEW    | ubuntu-latest   | 4-shard matrix; runner script lands in Plan 5 Task 2             |
| `cldr-plural`      | NEW    | ubuntu-latest   | `dotnet test … --filter "FullyQualifiedName~CldrPlural"`         |
| `locale-completeness` | (already present) | ubuntu-latest | Verified per brief — no edit                              |

Trigger filter updated to include the brief-mandated paths
(`packages/ui-core/**`, `packages/ui-adapters-*/**`, `packages/*/Resources/**`)
in addition to the pre-existing broader paths so existing gates keep firing on
their original triggers.

Aggregator `global-ux-gate.needs` extended from 3 → 7 entries so branch
protection's single required check still reflects all gates.

---

## YAML excerpts for each new job

### 1. `analyzers`

```yaml
analyzers:
  name: Analyzers (-warnaserror)
  runs-on: windows-latest
  timeout-minutes: 20
  steps:
    - uses: actions/checkout@v6
    - uses: actions/setup-dotnet@v5
      with:
        global-json-file: global.json
    - name: Restore .NET workloads
      run: dotnet workload restore Sunfish.slnx
    - name: Restore
      run: dotnet restore Sunfish.slnx
    - name: Build (warnings as errors)
      run: dotnet build Sunfish.slnx --configuration Release -warnaserror --no-restore
```

### 2. `xliff-round-trip`

```yaml
xliff-round-trip:
  name: XLIFF round-trip
  runs-on: ubuntu-latest
  timeout-minutes: 10
  steps:
    - uses: actions/checkout@v6
    - uses: actions/setup-dotnet@v5
      with:
        global-json-file: global.json
    - name: Test LocalizationXliff round-trip
      run: dotnet test tooling/Sunfish.Tooling.LocalizationXliff/tests/Sunfish.Tooling.LocalizationXliff.Tests.csproj --logger trx --results-directory TestResults
    - name: Upload test results
      if: always()
      uses: actions/upload-artifact@v7
      with:
        name: xliff-round-trip-results
        path: TestResults/
```

### 3. `a11y-audit` (4-shard matrix)

```yaml
a11y-audit:
  name: A11y axe audit (shard ${{ matrix.shard }}/4)
  runs-on: ubuntu-latest
  timeout-minutes: 20
  strategy:
    fail-fast: false
    matrix:
      shard: [1, 2, 3, 4]
  steps:
    - uses: actions/checkout@v6
    - uses: actions/setup-node@v5
      with:
        node-version: "20"
    - name: Install pnpm
      run: npm install --global pnpm@10.33.2
    - name: Install dependencies
      run: pnpm install --frozen-lockfile
    - name: Build Storybook (ui-core)
      run: pnpm --filter @sunfish/ui-core build-storybook
    - name: Run a11y audit shard
      run: node tooling/a11y-audit-runner/bin/run.mjs --shard ${{ matrix.shard }} --total-shards 4
```

### 4. `cldr-plural`

```yaml
cldr-plural:
  name: CLDR plural rules
  runs-on: ubuntu-latest
  timeout-minutes: 10
  steps:
    - uses: actions/checkout@v6
    - uses: actions/setup-dotnet@v5
      with:
        global-json-file: global.json
    - name: Test CLDR plural rules
      run: dotnet test packages/foundation/tests/ --filter "FullyQualifiedName~CldrPlural" --logger trx --results-directory TestResults
    - name: Upload test results
      if: always()
      uses: actions/upload-artifact@v7
      with:
        name: cldr-plural-results
        path: TestResults/
```

### 5. `locale-completeness` (pre-existing, verified)

```yaml
locale-completeness:
  name: Locale completeness check
  runs-on: ubuntu-latest
  timeout-minutes: 5
  steps:
    - uses: actions/checkout@v6
    - uses: actions/setup-node@v5
      with:
        node-version: "24"
    - name: Run tool's own fixture tests
      run: node tooling/locale-completeness-check/tests/fixture-test.mjs
    - name: Report locale completeness (no failure gate yet)
      run: node tooling/locale-completeness-check/check.mjs
```

---

## Syntax-validation evidence

**Validator 1 — Python `yaml.safe_load`:**

```
$ python -c "import yaml; d=yaml.safe_load(open('.github/workflows/global-ux-gate.yml'));
              print('jobs:', list(d['jobs'].keys()))"
jobs: ['css-logical', 'locale-completeness', 'a11y-storybook',
       'analyzers', 'xliff-round-trip', 'a11y-audit', 'cldr-plural',
       'global-ux-gate']
```

**Validator 2 — `npx js-yaml`:**

```
$ npx --yes js-yaml .github/workflows/global-ux-gate.yml > /dev/null && echo "js-yaml OK"
js-yaml OK
```

**Per-job structural check** (`name`, `runs-on`, `steps[]` all present):

```
analyzers:           name=True runs-on=windows-latest steps=5  matrix=False
xliff-round-trip:    name=True runs-on=ubuntu-latest  steps=4  matrix=False
a11y-audit:          name=True runs-on=ubuntu-latest  steps=6  matrix=True (shards 1-4)
cldr-plural:         name=True runs-on=ubuntu-latest  steps=4  matrix=False
locale-completeness: name=True runs-on=ubuntu-latest  steps=4  matrix=False
aggregator needs:    [css-logical, locale-completeness, a11y-storybook,
                      analyzers, xliff-round-trip, a11y-audit, cldr-plural]
```

---

## Deviations from brief

1. **`analyzers` — added `dotnet workload restore` + explicit `dotnet restore`
   step before `dotnet build`.** The brief specified only `dotnet build … -warnaserror`,
   but `Sunfish.slnx` includes the Anchor MAUI project that requires workload
   restoration on a fresh runner image (matches the existing `ci.yml` pattern).
   Without these steps, `dotnet build` would fail on first invocation with
   NETSDK1147 (workloads missing). Added `--no-restore` to the build line for
   correctness given the explicit restore step.

2. **`setup-dotnet` uses `global-json-file: global.json`** (matches existing
   `ci.yml`) rather than the brief's literal "preview SDK" wording. `global.json`
   already pins the preview SDK; this is the canonical Sunfish pattern.

3. **`actions/checkout@v6` + `actions/setup-dotnet@v5` + `actions/setup-node@v5`
   + `actions/upload-artifact@v7`** — matched to the versions already in use
   elsewhere in this workflow file and `ci.yml` (the brief did not pin versions).

4. **Trigger paths kept additive.** Brief says paths should be
   `packages/ui-core/**`, `packages/ui-adapters-*/**`, `packages/*/Resources/**`.
   I added those alongside the pre-existing broader paths
   (`packages/**`, `apps/**`, `i18n/**`, etc.) so the existing
   `css-logical` / `locale-completeness` / `a11y-storybook` jobs still trigger
   on their full input surface. Removing the broader paths would silently break
   those existing gates — out of scope for this task.

5. **`a11y-audit` adds `pnpm install --global` step.** The brief script line
   begins with `pnpm install --frozen-lockfile`, which assumes pnpm is on PATH.
   Added a `npm install --global pnpm@10.33.2` step (matches the version pinned
   in the `a11y-storybook` job above) so the runner has pnpm before the
   `pnpm install` line runs.

6. **`fail-fast: false` on `a11y-audit` matrix.** Standard practice for sharded
   gates: one shard's failure shouldn't cancel the others, since reviewers want
   the full picture of which shards passed.

7. **Added artifact-upload steps to `xliff-round-trip` and `cldr-plural`.**
   Mirrors the `if: always() / upload-artifact` pattern from `ci.yml` so trx
   results are inspectable on failure. Strictly additive; no impact on gate
   semantics.

---

## Known follow-ups (NOT in this task's scope)

- `tooling/a11y-audit-runner/bin/run.mjs` does not yet exist (Plan 5 Task 2
  builds it). The `a11y-audit` job will fail until then — this is expected
  and called out in the brief.
- `CldrPlural` test class does not yet exist in `packages/foundation/tests/`.
  Verified via `Grep CldrPlural packages/foundation` → no matches. The
  `--filter "FullyQualifiedName~CldrPlural"` argument matches zero tests and
  exits 0, so the job is GREEN today and becomes load-bearing once Plan 5
  Task 4 lands the test class.
- Plan 1 noted `locale-completeness` runs in report-only mode (no
  `--fail-on-incomplete`). Promoting to a hard gate is tracked in Plan 6 and
  is out of scope here.

---

## Diff-shape compliance

```
$ git diff --stat HEAD
 .github/workflows/global-ux-gate.yml  | <delta>
 1 file changed
```

Only `.github/workflows/global-ux-gate.yml` modified. No other files touched
in the code commit. Report file is committed separately per brief.
