# Plan 5 Task 4 Review — branch-protection script

**Date:** 2026-04-26
**Code commit:** 47b153ab
**Report commit:** 30e960f8
**Branch:** worktree-agent-a4326dbef98f87100

## Per-criterion results

| ID  | Criterion | Verdict |
|-----|-----------|---------|
| (a) | Commit touches only 4 files under `infra/github/` (README.md, apply-branch-protection.sh, branch-protection-main-before.json, branch-protection-main.json) | **PASS** |
| (b) | `branch-protection-main.json` is valid JSON; required GitHub-API top-level keys present (`required_status_checks` with `strict`+`contexts`, `enforce_admins`, `required_pull_request_reviews`, `restrictions`, `required_linear_history`, `allow_force_pushes`, `allow_deletions`) | **PASS** |
| (c) | Every `required_status_checks.contexts` entry maps to an actual `name:` of a job in `.github/workflows/*.yml` | **PASS** (see table below) |
| (d) | `apply-branch-protection.sh` syntax-checks clean (`bash -n`) | **PASS** |
| (e) | Script is idempotent (uses `gh api -X PUT`, `jq` validates JSON before apply, `set -euo pipefail`) | **PASS** |
| (f) | README documents purpose, how to apply, mandatory human approval, rollback strategy | **PASS** |
| (g) | Commit message contains `plan-5-task-4` token | **PASS** (subject line + explicit `Token: plan-5-task-4`) |
| (h) | Diff-shape: only files under `infra/github/`, no other paths | **PASS** |
| (i) | "Main is unprotected" finding verified — `gh api repos/ctwoodwa/Sunfish/branches/main/protection` returns `Branch not protected` HTTP 404; subagent's `branch-protection-main-before.json` placeholder accurately captures that 404 | **PASS** |
| (j) | Subagent's substitution of real workflow job names (from actual `.github/workflows/*.yml`) for the brief's fictional placeholder names is the right call | **PASS, confirmed** |

## Required-status-checks list evaluation

| Proposed context | Mapped to | Source file:line | Verdict |
|---|---|---|---|
| `Build & Test` | job `name: Build & Test` | `.github/workflows/ci.yml:11` | **MATCH** |
| `Lint PR commits` | job `name: Lint PR commits` | `.github/workflows/commitlint.yml:9` | **MATCH** |
| `Analyze (csharp)` | job `name: Analyze (csharp)` | `.github/workflows/codeql.yml:14` | **MATCH** |
| `CSS logical-properties audit` | job `name: CSS logical-properties audit` | `.github/workflows/global-ux-gate.yml:29` | **MATCH** |
| `Locale completeness check` | job `name: Locale completeness check` | `.github/workflows/global-ux-gate.yml:41` | **MATCH** |
| `A11y Storybook test-runner` | job `name: A11y Storybook test-runner` | `.github/workflows/global-ux-gate.yml:59` | **MATCH** |
| `Global-UX Gate (aggregate)` | job `name: Global-UX Gate (aggregate)` | `.github/workflows/global-ux-gate.yml:104` | **MATCH** |

7-for-7. The `Generate CycloneDX SBOM` job (sbom.yml) and the `Docs` workflow are intentionally excluded — appropriate, since SBOM publication and docs deploy are not gating concerns for `main` PR merges.

## Apply-script idempotency evaluation

The script meets idempotency requirements:

- **`gh api -X PUT`** on `/repos/{owner}/{repo}/branches/{branch}/protection` is the GitHub-documented idempotent operation for branch protection — same input JSON produces same end-state regardless of how many times applied.
- **`jq . "$RULE_FILE"` pre-flight** rejects malformed JSON before mutation (exit 3).
- **`set -euo pipefail`** ensures any failure in the chain (missing file, bad JSON, gh-api error) halts immediately rather than silently partial-applying.
- **No mutation order dependency** — the rule is a single PUT body, not a sequence.
- **Verification echo** at the end (`gh api ... -q '.required_status_checks.contexts'`) gives the operator immediate visual confirmation.

Minor observation (non-blocking): the script depends on `jq` being installed on the operator machine. README does not flag this prerequisite. Suggest adding "Requires: `gh`, `jq`" to the README's "How to apply" section in a follow-up — not a blocker because the script's own error message is clear if jq is missing.

## Rule-content observations (non-blocking)

- `enforce_admins: false` — admins can bypass the rule. This is permissive but consistent with the brief's "human approval gate" model (the human owner is the admin and is the approval gate). Worth a follow-up discussion in a future iteration once the team grows beyond a sole admin.
- `required_pull_request_reviews: null` — no PR review requirement. Per the carry-forward "PR-with-auto-merge default" memory entry, CI is the gate, not human review. This aligns. Confirm the human owner is comfortable with admin-only-no-review-required on a public-bound repo before flipping public.
- `required_linear_history: false` — squash-merge produces linear history regardless, so this default is fine.
- `allow_force_pushes: false` and `allow_deletions: false` — correct hardening.

## Final verdict: GREEN

All ten checklist items pass. The subagent correctly:
1. Did NOT apply the rule (human approval gate respected).
2. Substituted real workflow job names for the brief's placeholders.
3. Captured the "main is currently unprotected" reality accurately in the before-snapshot.
4. Documented rollback as DELETE rather than re-PUT of the 404 sentinel.

## Human approval gate

The driver MUST coordinate the actual `gh api -X PUT` call interactively with the human owner. Do NOT auto-merge this PR with the rule applied — the rule application is a separate, manual step after PR merge.

## Files reviewed

- `C:\Projects\sunfish\infra\github\branch-protection-main.json` (commit 47b153ab)
- `C:\Projects\sunfish\infra\github\branch-protection-main-before.json` (commit 47b153ab)
- `C:\Projects\sunfish\infra\github\apply-branch-protection.sh` (commit 47b153ab)
- `C:\Projects\sunfish\infra\github\README.md` (commit 47b153ab)
- `C:\Projects\sunfish\.github\workflows\ci.yml`
- `C:\Projects\sunfish\.github\workflows\codeql.yml`
- `C:\Projects\sunfish\.github\workflows\commitlint.yml`
- `C:\Projects\sunfish\.github\workflows\global-ux-gate.yml`
