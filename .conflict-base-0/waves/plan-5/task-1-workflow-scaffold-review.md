# Plan 5 Task 1 Review — workflow YAML scaffolding

**Date:** 2026-04-26
**Code commit:** fe88418d
**Report commit:** 2cb9c2f0
**Branch:** worktree-agent-a27a983a5612c276c

## Per-criterion results

- **(a) Diff scope (single file).** PASS. `git show --name-only fe88418d` returns exactly one path: `.github/workflows/global-ux-gate.yml`. No other files touched. `git diff --stat fe88418d^ fe88418d` confirms `1 file changed, 96 insertions(+), 1 deletion(-)`.

- **(b) 5 required jobs present and correctly configured.** PASS.
  - `analyzers` — `runs-on: windows-latest`, runs `dotnet build Sunfish.slnx --configuration Release -warnaserror --no-restore` (preceded by `dotnet workload restore Sunfish.slnx` and `dotnet restore Sunfish.slnx`, see deviation #1). Confirmed in lines under `# Plan 5 Task 1 — analyzers gate.`
  - `xliff-round-trip` — `runs-on: ubuntu-latest`, runs `dotnet test tooling/Sunfish.Tooling.LocalizationXliff/tests/Sunfish.Tooling.LocalizationXliff.Tests.csproj --logger trx --results-directory TestResults`. Targets the LocalizationXliff suite directly.
  - `a11y-audit` — `runs-on: ubuntu-latest`, `strategy.matrix.shard: [1, 2, 3, 4]`, executes `node tooling/a11y-audit-runner/bin/run.mjs --shard ${{ matrix.shard }} --total-shards 4`. Brief command verbatim.
  - `cldr-plural` — `runs-on: ubuntu-latest`, runs `dotnet test packages/foundation/tests/ --filter "FullyQualifiedName~CldrPlural" --logger trx --results-directory TestResults`. Filter exact.
  - `locale-completeness` — PRESERVED from Plan 1 (lines unchanged from `fe88418d^`). Plus the css-logical and a11y-storybook jobs from Plan 1 also remain. An `aggregator` job (`global-ux-gate`) lists all 7 in `needs:` for branch-protection.

- **(c) YAML parses cleanly.** PASS. `python -c "import yaml; yaml.safe_load(open('.github/workflows/global-ux-gate.yml'))"` returned `PARSE OK` and enumerated the 8 job keys correctly: `['css-logical', 'locale-completeness', 'a11y-storybook', 'analyzers', 'xliff-round-trip', 'a11y-audit', 'cldr-plural', 'global-ux-gate']`.

- **(d) Trigger filter includes the 3 brief-required paths.** PASS. The `on.pull_request.paths` block in fe88418d includes `packages/ui-core/**`, `packages/ui-adapters-*/**`, and `packages/*/Resources/**` — all three brief-required entries — added on top of the pre-existing 6 paths (additive, see deviation #4). Diff vs `fe88418d^` shows these three are net-new.

- **(e) Commit message contains `plan-5-task-1` token.** PASS. Subject: `ci(global-ux): plan-5-task-1 — extend gate workflow with 5 required jobs`. Body also contains `Token: plan-5-task-1`.

- **(f) Diff-shape: only workflow YAML touched.** PASS. No `.csproj`, no tooling source, no other YAML. Single-file scope confirmed under (a).

- **(g) Deviation evaluation.** All 7 documented deviations confirmed acceptable — see next section.

- **(h) Known follow-ups documented.** PASS. The two expected gaps (a11y-audit-runner script not yet present → Plan 5 Task 2; CldrPlural test class not yet present → Plan 5 Task 4) are explicitly noted in the inline workflow comments (`# delivered by Plan 5 Task 2; until that lands this job will fail (expected).` and `# becomes load-bearing once Plan 5 Task 4 lands the CldrPlural test class`). Failure of those two jobs at the next CI run is expected and is the correct dependency-handoff signal.

## Deviation evaluation

1. **`dotnet workload restore` + explicit `dotnet restore` for analyzers.** ACCEPTABLE. Sunfish.slnx contains the Anchor MAUI project; the Windows runner needs MAUI workload installation before build. Matches the rationale already documented in `ci.yml`. The `--no-restore` on the build step correctly pairs with the explicit prior `restore`.

2. **`setup-dotnet` uses `global-json-file: global.json`.** ACCEPTABLE. Canonical Sunfish pattern (matches `ci.yml` and the rest of the repo). Avoids version-drift between workflows.

3. **Pinned action versions (`@v6`/`@v5`/`@v7`).** ACCEPTABLE. Matches the prevailing pattern already in this same workflow file (`actions/checkout@v6`, `actions/setup-node@v5`, `actions/upload-artifact@v7` are used by the pre-existing Plan 1 jobs). Consistency upheld.

4. **Additive trigger paths.** ACCEPTABLE. The pre-existing `packages/**`, `apps/**`, `tooling/css-logical-audit/**`, `tooling/Sunfish.Tooling.ColorAudit/**`, `i18n/**`, and `.github/workflows/global-ux-gate.yml` paths are preserved; the brief's three paths (`packages/ui-core/**`, `packages/ui-adapters-*/**`, `packages/*/Resources/**`) plus three new tooling paths (`tooling/Sunfish.Tooling.LocalizationXliff/**`, `tooling/a11y-audit-runner/**`, `tooling/locale-completeness-check/**`) are added. Preserves css-logical/a11y-storybook trigger coverage from Plan 1 and aligns paths with the new jobs' source dependencies. Brief explicitly permits this deviation.

5. **`pnpm install --global pnpm@10.33.2` step on a11y-audit.** ACCEPTABLE. Matches the version pin already used by the Plan 1 a11y-storybook job in this same file. Cross-job consistency.

6. **`fail-fast: false` on a11y-audit matrix.** ACCEPTABLE. Sensible for a 4-shard parallel matrix — one failing shard should not cancel diagnostic data from the other three. Standard a11y-sharding pattern.

7. **Artifact-upload steps for xliff-round-trip + cldr-plural.** ACCEPTABLE. Mirrors the pattern used by `ci.yml` and by the pre-existing `a11y-storybook` job. `if: always()` ensures TestResults upload even on test failure — exactly the desired diagnostic posture for a gate.

## Final verdict: GREEN
