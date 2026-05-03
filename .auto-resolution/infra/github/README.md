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
  merges (`enforcement: "evaluate"`). **Note:** evaluate mode is
  Enterprise-plan-only; on Pro/Team, use the canary branch test described
  below.
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
| `apply-main-ruleset.sh` | Idempotent apply script for the ruleset. Supports `--dry-run`, `--evaluate`, and `--delete`. |
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

# 2. (Enterprise plans only) First-time apply in evaluate mode to observe
#    behavior on PRs without blocking merges. NOT available on Pro/Team —
#    see "Substitute for --evaluate on non-Enterprise plans" below.
bash infra/github/apply-main-ruleset.sh --evaluate

# 3. Apply with active enforcement (Pro/Team plans skip step 2 and use the
#    canary-branch test described below before this step):
bash infra/github/apply-main-ruleset.sh

# 4. One-command rollback — looks up the ruleset by name and DELETEs it.
#    No-op (exit 0) if the ruleset doesn't exist, so safe to re-run.
bash infra/github/apply-main-ruleset.sh --delete
```

### Substitute for `--evaluate` on non-Enterprise plans

GitHub gates the Rulesets `enforcement: "evaluate"` mode behind the Enterprise
plan tier. On Free / Pro / Team, the API rejects the apply with:

> "Enforcement evaluate option is not supported on this plan. Please upgrade
> to Enterprise to enable it."

See [GitHub docs — Creating rulesets for a repository](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/creating-rulesets-for-a-repository)
for the canonical plan matrix.

The substitute on lower plans is the **canary branch test** pattern:

1. Edit `main-ruleset.json` and temporarily change
   `conditions.ref_name.include` from `["refs/heads/main"]` to
   `["refs/heads/test/ruleset-canary"]` (any throwaway branch name works).
2. Run `bash infra/github/apply-main-ruleset.sh` to apply the ruleset
   scoped to that single canary branch.
3. Push test commits to `test/ruleset-canary` and open trial PRs against it.
   Observe whether expected checks fire and whether the merge gate behaves
   as intended.
4. When done, run `bash infra/github/apply-main-ruleset.sh --delete` to
   remove the canary ruleset. Restore the `include` field in
   `main-ruleset.json` to `["refs/heads/main"]` and commit the revert.
5. Now run `bash infra/github/apply-main-ruleset.sh` for the real `main`
   apply.

This is best-effort — the canary branch may not exhibit every PR shape that
`main` will see — but it isolates blast radius without paying for
Enterprise. The `--delete` flag exists specifically to make step 4 a single
command.

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

**Preferred — one-command delete via the apply script:**

```bash
bash infra/github/apply-main-ruleset.sh --delete
```

The script looks up the ruleset by name (`main-protection`) and DELETEs it
via the GitHub API. Exits 0 with a "nothing to delete" message if no
matching ruleset exists, so the flag is safe to re-run. Cannot be combined
with `--dry-run` or `--evaluate` (the script will exit 2 with an error).

**Alternative — delete by id directly** (look up the id from the apply
script's verification echo or from `gh api repos/ctwoodwa/Sunfish/rulesets`):

```bash
gh api -X DELETE "repos/ctwoodwa/Sunfish/rulesets/<RULESET_ID>"
```

**Alternative — soft-disable without deleting** by editing
`main-ruleset.json` to set `"enforcement": "disabled"` and re-running
`apply-main-ruleset.sh`. The ruleset stays in the UI for inspection and can
be re-enabled in one PR.

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

`main-ruleset.json` was originally drafted with 7 required contexts. After
Phase-1 verification on the last 10 merged PRs, it was **trimmed to 4
always-on contexts** (see the inline `_comment_required_checks_choice`
field in `main-ruleset.json` for the rationale). The current required list
is:

| Context | Workflow | Always runs on PR? | Appearances/10 |
|---|---|---|---|
| `Lint PR commits` | `commitlint.yml` | Yes. Safe. | 10/10 |
| `Analyze (csharp)` | `codeql.yml` | Yes. Safe. | 10/10 |
| `CodeQL` | `codeql.yml` (separate from `Analyze (csharp)`) | Yes. Safe. | 10/10 |
| `semgrep-cloud-platform/scan` | Semgrep Cloud Platform | Yes. Safe. | 10/10 |

**Removed from required (still run when applicable, just not gate-blocking):**

| Context | Workflow | Why removed |
|---|---|---|
| `Build & Test` | `ci.yml` | Skipped on docs-only PRs via `paths-ignore`. 6/10 appearances. |
| `CSS logical-properties audit` | `global-ux-gate.yml` | Skipped on most PRs — `paths` filter to `packages/**`, `apps/**`, `tooling/css-logical-audit/**`, `tooling/Sunfish.Tooling.*`/**, `i18n/**`. 1/10. |
| `Locale completeness check` | `global-ux-gate.yml` | Same filter. 1/10. |
| `A11y Storybook test-runner` | `global-ux-gate.yml` | Same filter. 1/10. |
| `Global-UX Gate (aggregate)` | `global-ux-gate.yml` | Aggregator inherits the workflow `paths` filter. 1/10. |

If the original 7-check list had been applied as-is, 9 of the last 10 PRs
would have been permanently blocked because their required checks never
report. **Strict mode + a missing required check = block forever.**

### Recommended fix sequence (post-merge of this PR)

1. **Apply the trimmed ruleset** (canary-branch test first per the
   "Substitute for `--evaluate`" section above; this repo is on a
   non-Enterprise plan and `--evaluate` is unavailable).
2. **Pick one** of the structural fixes to re-include the global-ux-gate
   checks (out-of-scope for this artifact PR; open a separate PR):
    - **Option A (preferred):** Remove the `paths:` filter from
      `.github/workflows/global-ux-gate.yml` so all four jobs always run on
      every PR. Tradeoff: ~12 min of extra CI on docs-only PRs (per Plan 5
      Task 9 p95 measurement).
    - **Option B:** Refactor `global-ux-gate.yml` to drop the workflow-level
      `paths:` and put the path-conditional logic at the job level (using
      `dorny/paths-filter` or equivalent), with a fan-in `global-ux-gate`
      aggregator job that **always runs** and only requires the leaf jobs
      that actually executed.
    - **Option C:** Move the relevant gates into `ci.yml` so they share the
      always-on lifecycle.
3. **Once the structural fix is merged**, re-add the four `global-ux-gate`
   contexts and `Build & Test` to the `required_status_checks` array in
   `main-ruleset.json` and re-apply.

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
