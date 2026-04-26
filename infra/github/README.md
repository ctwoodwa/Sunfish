# `infra/github/` — GitHub repository configuration

Reproducible, version-controlled GitHub configuration for the Sunfish repository.

## Purpose

GitHub-side configuration (branch-protection rules, required status checks, etc.)
is normally managed in the GitHub UI, which makes it invisible to code review
and easy to drift. This directory keeps the canonical rule definitions in-repo
as JSON, paired with idempotent apply scripts so any change is reviewable as a
PR diff and reproducible from a clean state.

## Two parallel approaches

This directory ships **two** ways to express the same protection intent for `main`:

| Approach | Files | API | Status |
|---|---|---|---|
| **Ruleset** (recommended, modern) | `main-ruleset.json`, `apply-main-ruleset.sh` | `POST/PATCH /repos/{owner}/{repo}/rulesets` | Use this. |
| Legacy branch-protection | `branch-protection-main.json`, `branch-protection-main-before.json`, `apply-branch-protection.sh` | `PUT /repos/{owner}/{repo}/branches/{branch}/protection` | Kept for rollback reference and historical diff. |

Both define the same intent (no force-push, no delete, strict status checks,
no required reviews per solo-maintainer policy). Choose based on the table
below.

### When to use the Ruleset approach (default)

- New protection setup or any change to the protection rule.
- You want layerable, additive rules (org rulesets can stack on top of repo
  rulesets — useful when Sunfish moves to an org).
- You want a richer bypass model (`bypass_actors` with `actor_type` +
  `bypass_mode` of `always`/`pull_request`/`exempt`).
- You want the option to dry-run the rule against real PRs without blocking
  merges (`enforcement: "evaluate"`).
- You want `do_not_enforce_on_create: true` so creating a brand-new branch
  doesn't get blocked by required-check evaluation at branch-create time.

### When to fall back to the Legacy approach

- Emergency revert of the Ruleset to the prior baseline (the legacy file
  represents the static target state, the ruleset version may have drifted in
  the UI).
- Tooling that explicitly requires the legacy API (none in Sunfish today).

## Files

| File | Purpose |
|---|---|
| `main-ruleset.json` | **Ruleset definition for `main` (recommended).** Edit this to change the rule. |
| `apply-main-ruleset.sh` | Idempotent apply script for the ruleset. Supports `--dry-run` and `--evaluate`. |
| `branch-protection-main.json` | Legacy protection rule. Kept for rollback / historical reference. |
| `branch-protection-main-before.json` | Rollback reference snapshot captured **before** the Plan 5 rule was first proposed. |
| `apply-branch-protection.sh` | Idempotent apply script for the legacy rule. |

## How to apply the Ruleset (recommended path)

> **Mandatory human-owner approval before running.** This script mutates
> repository-level GitHub settings. The driver agent and human owner must
> coordinate the actual `gh api -X POST/PATCH` call — no agent should run
> this script without an explicit, contemporaneous "yes, apply now" from the
> human owner.

From the repo root, with the GitHub CLI authenticated as a user with admin
rights on `ctwoodwa/Sunfish`:

```bash
# 1. Dry-run first — verify the resolved payload + create-vs-update decision:
bash infra/github/apply-main-ruleset.sh --dry-run

# 2. First-time apply — start in evaluate mode to observe behavior on PRs
#    without blocking merges. Watch a few PRs go through, confirm the
#    expected check contexts appear in "Rule Insights" on github.com,
#    and confirm no false positives.
bash infra/github/apply-main-ruleset.sh --evaluate

# 3. Promote to active enforcement once evaluate-mode behavior is verified:
bash infra/github/apply-main-ruleset.sh
```

Optional environment overrides:

```bash
REPO=ctwoodwa/Sunfish \
RULESET=infra/github/main-ruleset.json \
  bash infra/github/apply-main-ruleset.sh
```

The script:

1. Validates the JSON parses (`jq` if present, else Python's `json.load`).
2. Strips `_comment_*` keys from the payload (the API rejects unknown
   top-level fields; comments are kept in-repo for human readers).
3. Looks up whether a ruleset named `main-protection` already exists; if
   yes, PATCHes it by id; if no, POSTs a new one. **Idempotent** — re-running
   converges to the same state.
4. Echoes the applied ruleset's name, enforcement, target, conditions, rule
   types, and required-check contexts so the operator can eyeball it.

## How to apply the Legacy rule

```bash
bash infra/github/apply-branch-protection.sh
```

Same mandatory human-owner approval applies. See script header for env
overrides. This path is kept primarily for rollback; new edits should go to
the ruleset.

## How to roll back

### Roll back the ruleset

Delete the ruleset by id (look up the id from the apply script's verification
echo or from `gh api repos/ctwoodwa/Sunfish/rulesets`):

```bash
gh api -X DELETE "repos/ctwoodwa/Sunfish/rulesets/<RULESET_ID>"
```

Or "soft-disable" without deleting by editing `main-ruleset.json` to set
`"enforcement": "disabled"` and re-running `apply-main-ruleset.sh`. This is
preferred — the ruleset stays in the UI for inspection and can be re-enabled
in one PR.

### Roll back the legacy rule

The `branch-protection-main-before.json` file is a special "no-protection"
sentinel — `main` was unprotected at the time of Plan 5 capture. Re-applying
it as JSON would fail. To roll the legacy rule back:

```bash
gh api -X DELETE repos/ctwoodwa/Sunfish/branches/main/protection
```

For future iterations where the `before` file is a real legacy snapshot:

```bash
RULE_FILE=infra/github/branch-protection-main-before.json \
  bash infra/github/apply-branch-protection.sh
```

## Editing the rule

1. Edit `main-ruleset.json` (or, for legacy, `branch-protection-main.json`).
2. Validate locally:
   ```bash
   bash infra/github/apply-main-ruleset.sh --dry-run
   ```
3. Open a PR — reviewers see the rule diff.
4. After merge, the human owner runs the apply script to push the new rule
   to GitHub.

The required-status-checks list must use the **exact `name:`** of each
workflow job (or the workflow file name if `name:` is omitted). Matrix jobs
use `Job Name (matrix-value)` per GitHub convention.

## Paths-ignore hazard (READ THIS BEFORE APPLY)

GitHub Rulesets, like the legacy branch-protection API, will leave a required
status check in **"Pending"** if its workflow is **skipped** (whether via
`paths:`, `paths-ignore:`, `branches-ignore:`, commit-message skip, or any
other workflow-level filter). A pending required check **permanently blocks
the merge button** — this is the same gotcha that affected the legacy rule
and is **not solved** by switching to Rulesets.

The Rulesets API offers **two related but partial mitigations**:

- `do_not_enforce_on_create: true` — Permits *branch creation* without checks
  having reported. Does **not** affect PR merges. Sunfish enables it as a
  guardrail against branch-creation friction; it has no effect on this hazard.
- `integration_id: 15368` (the GitHub Actions app id) — Scopes each required
  check to a specific producer (here, the GitHub Actions integration). This
  prevents check-name collisions from third-party bots; it does **not** make
  a skipped check pass.

There is **no native Rulesets feature** that auto-passes a skipped check.
GitHub-staff acknowledged this gap as backlogged; community workarounds use
job-level conditionals + a singular always-running aggregator job.

### Concrete impact in this repo

Of the 7 contexts in `main-ruleset.json`:

| Context | Workflow | Always runs on PR? |
|---|---|---|
| `Build & Test` | `ci.yml` | Yes (no path filter). Safe. |
| `Lint PR commits` | `commitlint.yml` | Yes. Safe. |
| `Analyze (csharp)` | `codeql.yml` | Yes. Safe. |
| `CSS logical-properties audit` | `global-ux-gate.yml` | **No** — workflow is `paths`-filtered to `packages/**`, `apps/**`, `tooling/css-logical-audit/**`, `tooling/Sunfish.Tooling.*`/**, `i18n/**`. |
| `Locale completeness check` | `global-ux-gate.yml` | **No** — same filter. |
| `A11y Storybook test-runner` | `global-ux-gate.yml` | **No** — same filter. |
| `Global-UX Gate (aggregate)` | `global-ux-gate.yml` | **No** — the aggregator inherits the workflow's `paths` filter. |

A docs-only PR (e.g., touching only `docs/**` or `_shared/**`) will not
trigger any of the four `global-ux-gate.yml` jobs, so the four corresponding
required checks will stay "Pending" forever and the PR will be unmergeable.

### Recommended fix sequence (post-merge of this PR)

1. **First apply with `--evaluate`** to confirm the hazard is observable but
   non-blocking on a docs-only PR. The "Rule Insights" tab on github.com
   shows what would have been blocked.
2. **Pick one** of the structural fixes (out-of-scope for this artifact PR;
   open a separate PR):
    - **Option A (preferred):** Remove the `paths:` filter from
      `.github/workflows/global-ux-gate.yml` so all four jobs always run on
      every PR. Tradeoff: ~12 min of extra CI on docs-only PRs (per Plan 5
      Task 9 p95 measurement).
    - **Option B:** Refactor `global-ux-gate.yml` to drop the workflow-level
      `paths:` and put the path-conditional logic at the job level (using
      `dorny/paths-filter` or equivalent), with a fan-in `global-ux-gate`
      aggregator job that **always runs** and only requires the leaf jobs
      that actually executed.
    - **Option C (least invasive):** Reduce `main-ruleset.json` to require
      only the three always-running checks (`Build & Test`, `Lint PR commits`,
      `Analyze (csharp)`). Removes the merge block but loses Global-UX gate
      enforcement on PRs whose paths trigger the workflow.
3. **Once the structural fix is merged**, re-apply the ruleset with default
   `active` enforcement.

## Diff between the legacy and ruleset definitions

Semantic intent is identical; the JSON shape and a few capabilities differ:

| Concept | Legacy field | Ruleset equivalent |
|---|---|---|
| Required checks | `required_status_checks.contexts: [string]` | `rules[].type=required_status_checks → parameters.required_status_checks: [{context, integration_id}]` |
| Strict (up-to-date) | `required_status_checks.strict: true` | `parameters.strict_required_status_checks_policy: true` |
| Block force-push | `allow_force_pushes: false` | `rules[].type=non_fast_forward` |
| Block delete | `allow_deletions: false` | `rules[].type=deletion` |
| No required reviews | `required_pull_request_reviews: null` | `rules[].type=pull_request → parameters.required_approving_review_count: 0` |
| Squash-only merge | (not directly expressible) | `parameters.allowed_merge_methods: ["squash"]` |
| Admin bypass | `enforce_admins: false` | `bypass_actors: [{actor_type: RepositoryRole, actor_id: 5, bypass_mode: always}]` |
| Branch creation grace | (not available) | `parameters.do_not_enforce_on_create: true` |
| Dry-run mode | (not available) | `enforcement: "evaluate"` |

The ruleset's `actor_id: 5` is GitHub's well-known repository role id for
"Admin". Other repository role ids: `1` Read, `2` Triage, `3` Write,
`4` Maintain, `5` Admin.
