# Plan 5 Task 4 — Branch-Protection Rule for `main` (Report)

**Status:** GREEN (authored, validated, NOT applied)
**Token:** `plan-5-task-4`
**Date:** 2026-04-25

---

## 1. Scope

Author a reproducible, version-controlled branch-protection rule for `main`,
plus an idempotent apply script. **Application is the human-approval gate** —
this task delivers the artifacts, the driver coordinates the actual
`gh api -X PUT` call interactively with the user.

## 2. Files Created

All under `infra/github/` (new directory):

| File | Bytes | Purpose |
|---|---:|---|
| `infra/github/branch-protection-main.json` | 486 | Proposed rule (the source of truth) |
| `infra/github/branch-protection-main-before.json` | 569 | Rollback reference: the captured state of `main`'s protection rule **before** Plan 5 changes |
| `infra/github/apply-branch-protection.sh` | 855 | Idempotent bash apply script (executable) |
| `infra/github/README.md` | 2824 | Operator documentation: purpose, apply, rollback, edit workflow |

Diff-shape: ONLY `infra/github/*` — no other paths touched.

## 3. Build-Gate Evidence

```
$ python -c "import json; json.load(open('infra/github/branch-protection-main.json'))"
PASS: branch-protection-main.json parses

$ python -c "import json; json.load(open('infra/github/branch-protection-main-before.json'))"
PASS: branch-protection-main-before.json parses

$ bash -n infra/github/apply-branch-protection.sh
PASS: apply-branch-protection.sh syntax-checks

$ ls -la infra/github/
-rwxr-xr-x apply-branch-protection.sh   (executable bit set)
-rw-r--r-- branch-protection-main.json
-rw-r--r-- branch-protection-main-before.json
-rw-r--r-- README.md
```

`jq` was unavailable in the agent's bash sandbox, so JSON parse was verified
with Python's `json.load`. The apply script itself uses `jq` at runtime — that
will be available on the human owner's machine when they run the apply step.

## 4. Before-Snapshot Result

`gh api repos/ctwoodwa/Sunfish/branches/main/protection` returned:

```json
{"message": "Branch not protected", "status": "404"}
```

`main` is **currently unprotected**. The `branch-protection-main-before.json`
file captures this fact as a structured note (per the brief's fallback
instruction). Rollback semantics are therefore special: re-applying the
"before" file is a no-op; to roll back the Plan 5 rule, the operator runs
`gh api -X DELETE repos/ctwoodwa/Sunfish/branches/main/protection`. This
quirk is documented in both the JSON file's `_rollback_meaning` field and the
`README.md` "How to roll back" section.

## 5. Proposed `required_status_checks.contexts` List

This is the most load-bearing piece of the deliverable. Every entry was
verified against the actual `name:` field of a job in a workflow that runs
on `pull_request: branches: [main]`:

| Status check name | Source workflow | Job ID | Why required |
|---|---|---|---|
| `Build & Test` | `.github/workflows/ci.yml` | `build-and-test` | Builds the full `Sunfish.slnx` and runs unit-tier `dotnet test` (windows-latest). Per the global-ux-gate.yml header comment lines 8-11, **the .NET-side gates — SUNFISH_I18N_001 analyzer, SmartFormat CLDR plural tests, XLIFF round-trip tests, bUnit-axe bridge tests — all ride on this single job** because every project lives in the slnx. This is the canonical .NET gate. |
| `Lint PR commits` | `.github/workflows/commitlint.yml` | `commitlint` | Conventional Commits enforcement via `wagoid/commitlint-github-action@v6`. Pairs with the Husky local hook from Plan 3 (PR #89) — local hook prevents bad commits being created, this CI gate prevents them being merged from machines without the hook installed. |
| `Analyze (csharp)` | `.github/workflows/codeql.yml` | `analyze` | CodeQL static security analysis on the foundation package. Already runs on every PR; making it a required check upgrades it from advisory to blocking. |
| `CSS logical-properties audit` | `.github/workflows/global-ux-gate.yml` | `css-logical` | Plan 4B §2 RTL gate — fails on physical-direction CSS properties (`margin-left`, `padding-right`, etc.) that break `dir="rtl"` rendering. Tooling at `tooling/css-logical-audit/audit.mjs`. |
| `Locale completeness check` | `.github/workflows/global-ux-gate.yml` | `locale-completeness` | i18n completeness gate. Currently in **report-only mode** (no `--fail-on-incomplete`) per the workflow comment lines 51-54 — runs the tool's fixture tests strictly but reports the real check non-blockingly until the first complete-tier locale clears the 95% floor. Listed as a required check so the **fixture tests** stay blocking; the report step's exit code is 0 by design today. |
| `A11y Storybook test-runner` | `.github/workflows/global-ux-gate.yml` | `a11y-storybook` | Plan 4 Task 4.2 — Storybook + `test-storybook` + `@axe-core/playwright` postVisit hook. Catches a11y regressions on every ui-core component story. The pnpm + Playwright + http-server toolchain is documented inline in the job. |
| `Global-UX Gate (aggregate)` | `.github/workflows/global-ux-gate.yml` | `global-ux-gate` | Per the workflow comment lines 100-103, this aggregator job is **explicitly designed as a single required check that passes only when all parent global-ux jobs pass**. It is `needs: [css-logical, locale-completeness, a11y-storybook]`. Including both the aggregator AND its leaf jobs is intentional defense-in-depth: the leaves give signal-grain visibility on the PR checks UI, the aggregator ensures the whole gate is green even if a future leaf is added without us also amending this rule. |

### Workflows intentionally excluded

- **`docs.yml`** — runs only on push-to-main (post-merge) for GitHub Pages deploy; never runs on PR, so cannot be a required check. Its build also doesn't gate anything safety-critical (docfx site).
- **`sbom.yml`** — runs on `release: published` and `workflow_dispatch` only; not a PR-time signal.

### Discrepancy with the brief's example list

The brief's example contained job names that **do not exist in any current
workflow**: `analyzers`, `xliff-round-trip`, `a11y-audit (1..4)`, `cldr-plural`.
Per the brief's instruction to "Adjust the `contexts` list based on actual job
names from `.github/workflows/global-ux-gate.yml`", these were replaced with
the canonical names. The `xliff-round-trip` / `cldr-plural` / `analyzers` /
bunit-bridge work is covered by the `Build & Test` job per the explicit
design comment in `global-ux-gate.yml` lines 8-11 — those tests live in the
slnx and ride the existing dotnet-test gate rather than getting their own
workflow jobs. The brief's `a11y-audit (1..4)` matrix doesn't exist; the
real a11y gate is the singleton `A11y Storybook test-runner`.

## 6. Other Rule Settings

| Setting | Value | Rationale |
|---|---|---|
| `required_status_checks.strict` | `true` | "Require branches to be up to date before merging" — prevents semantic merge conflicts where a PR passes against an old `main` but breaks against current `main`. |
| `enforce_admins` | `false` | The repo currently has a single admin (the human owner). Enforcing on admins would mean a CI outage blocks the owner from emergency merges. Revisit when the team grows past 1 admin. |
| `required_pull_request_reviews` | `null` | Solo-maintainer repo; review-required would self-block. The PR-with-auto-merge default (per user memory `feedback_pr_push_authorization`) already routes everything through PRs as the review surface; the value gate is CI green, not human approval count. Revisit when collaborators join. |
| `restrictions` | `null` | No push allowlist; the rule's enforcement is via PR + status checks. |
| `required_linear_history` | `false` | Repo currently uses squash-merge per user memory; that already produces a linear history without forcing this flag, which would also forbid merge commits on rare occasions where one is needed (e.g., release branches). |
| `allow_force_pushes` | `false` | Force-push to `main` is forbidden per user memory `feedback_pr_push_authorization` (`force push to main/master, warn the user`). This makes it physically impossible. |
| `allow_deletions` | `false` | Cannot delete `main`. |

## 7. Application Step (NOT done by this subagent)

Per the brief: this subagent does **steps 1-4 only**. The actual application
of the rule (`gh api -X PUT`) is **step 5**, which requires explicit
human-owner approval at the time of execution.

> **DRIVER + HUMAN OWNER must coordinate the actual `gh api` PUT call.**
> The apply script is authored, validated, executable, and documented in
> `infra/github/README.md`. It will be run interactively after the human
> owner approves Plan 5 Task 4. Until that moment, `main` remains unprotected
> and this commit is purely a paper artifact — no GitHub-side state changes.

Once approved, the operator runs:

```bash
bash infra/github/apply-branch-protection.sh
```

…and verifies the echoed `required_status_checks.contexts` matches the JSON.

## 8. Trust Boundary Compliance

- **TRUSTED used:** the brief itself; existing `gh` CLI; the actual workflow
  files at `origin/main` (`.github/workflows/*.yml`); Plan 5 implementation
  plan (PR #90 just merged).
- **UNTRUSTED ignored:** no other notes were used to drive job-name
  decisions. Job names were derived strictly from grepping the workflow
  files in the worktree.
- **Did NOT push.** **Did NOT run `gh api -X PUT`.** **Did NOT touch any
  path outside `infra/github/` and `waves/plan-5/`** (the latter being the
  report itself).

## 9. Verdict

**GREEN.** All artifacts authored, all build gates pass, diff-shape
respected, no GitHub-side state changed. Ready for human-owner review and
the subsequent application step.
