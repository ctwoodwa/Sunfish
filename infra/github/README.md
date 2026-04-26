# `infra/github/` — GitHub repository configuration

Reproducible, version-controlled GitHub configuration for the Sunfish repository.

## Purpose

GitHub-side configuration (branch-protection rules, required status checks, etc.)
is normally managed in the GitHub UI, which makes it invisible to code review
and easy to drift. This directory keeps the canonical rule definitions in-repo
as JSON, paired with idempotent apply scripts so any change is reviewable as a
PR diff and reproducible from a clean state.

## Files

| File | Purpose |
|---|---|
| `branch-protection-main.json` | Proposed/applied protection rule for `main`. Edit this to change the rule. |
| `branch-protection-main-before.json` | Rollback reference snapshot captured **before** the Plan 5 rule was first applied. |
| `apply-branch-protection.sh` | Idempotent script that PUTs `branch-protection-main.json` to the GitHub API. |

## How to apply

> **Mandatory human-owner approval before running.** This script mutates
> repository-level GitHub settings. The driver agent and human owner must
> coordinate the actual `gh api -X PUT` call — no agent should run this script
> without an explicit, contemporaneous "yes, apply now" from the human owner.

From the repo root, with the GitHub CLI authenticated as a user with admin
rights on `ctwoodwa/Sunfish`:

```bash
bash infra/github/apply-branch-protection.sh
```

Optional environment overrides:

```bash
REPO=ctwoodwa/Sunfish \
BRANCH=main \
RULE_FILE=infra/github/branch-protection-main.json \
  bash infra/github/apply-branch-protection.sh
```

The script:
1. Validates the JSON parses (`jq .`).
2. PUTs the rule to `repos/$REPO/branches/$BRANCH/protection`.
3. Echoes the resulting `required_status_checks.contexts` so you can eyeball it.

## How to roll back

The `before` snapshot for the Plan 5 rule is the special "no protection"
sentinel — `main` was unprotected at capture time. Re-applying it as JSON would
fail (the API expects the full rule shape, not the 404 placeholder). To roll
back, **delete** the protection instead:

```bash
gh api -X DELETE repos/ctwoodwa/Sunfish/branches/main/protection
```

For future iterations where `before` is a real rule snapshot, roll back via:

```bash
RULE_FILE=infra/github/branch-protection-main-before.json \
  bash infra/github/apply-branch-protection.sh
```

## Editing the rule

1. Edit `branch-protection-main.json`.
2. Validate locally: `jq . infra/github/branch-protection-main.json`.
3. Open a PR — reviewers can see the rule diff.
4. After merge, the human owner runs `apply-branch-protection.sh` to push the
   new rule to GitHub.

The required-status-checks list must use the **exact `name:`** of each workflow
job (or the workflow file name if `name:` is omitted). Matrix jobs use
`Job Name (matrix-value)` per GitHub convention.
