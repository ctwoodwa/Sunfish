# GitHub Rulesets

This directory holds the source-of-truth JSON for the repository's GitHub
branch and tag rulesets. The JSON is kept in version control so that rule
changes are reviewable in PRs the same way any other config is. The rules
themselves must still be imported (or edited) in GitHub's UI or via the
REST API — GitHub does not auto-apply JSON in this folder.

## Files

| File | Target | Purpose |
|---|---|---|
| `main-branch.json` | `~DEFAULT_BRANCH` (currently `main`) | Gate merges into the default branch behind PR review + CI |
| `release-tags.json` | `refs/tags/v*` | Prevent deletion or rewriting of published release tags |

## What each rule does

### `main-branch.json`

| Rule | Effect |
|---|---|
| `deletion` | Blocks deletion of the default branch. |
| `non_fast_forward` | Blocks force pushes. |
| `required_linear_history` | Disallows merge commits (squash-only merges align with the existing commit history style: `feat(...): ... (#NN)`). |
| `pull_request` | Requires a PR to merge. 1 approving review, dismiss stale reviews on push, require CODEOWNERS review, require all review threads resolved, restrict merge method to **squash**. |
| `required_status_checks` (strict) | Requires CI jobs `Build & Test` (from `.github/workflows/ci.yml`) and `Analyze (csharp)` (from `.github/workflows/codeql.yml`) to pass, and requires the branch to be up to date with `main` before merge. |

**Bypass:** repository **Admin** role (`actor_id: 5`, `bypass_mode: always`).
This is the pragmatic posture for the current BDFL governance model — the
sole maintainer can merge their own PRs (GitHub does not permit self-approval)
and ship emergency fixes. External contributors always go through the full
gate because they do not hold the Admin role.

### `release-tags.json`

| Rule | Effect |
|---|---|
| `deletion` | Blocks deletion of any `v*` tag. |
| `update` | Blocks rewriting a tag to point at a different commit. |
| `non_fast_forward` | Defense-in-depth against force updates to tags. |

**Bypass:** repository **Admin** role. Intentionally narrow — if a release
tag ever needs to be retracted, it is a manual, visible, audited action.

## How to apply

### Option A — Import in the GitHub UI (recommended)

1. Repository → **Settings** → **Rules** → **Rulesets**.
2. Click **New ruleset** → **Import a ruleset**.
3. Select `main-branch.json` (or `release-tags.json`) from this folder.
4. Review the imported ruleset, then click **Create**.

Repeat for the second file.

### Option B — Apply via REST API

```bash
# Requires a token with `repo` (or fine-grained `administration: write`) scope.
curl -L \
  -X POST \
  -H "Accept: application/vnd.github+json" \
  -H "Authorization: Bearer $GITHUB_TOKEN" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  https://api.github.com/repos/ctwoodwa/Sunfish/rulesets \
  -d @.github/rulesets/main-branch.json

curl -L \
  -X POST \
  -H "Accept: application/vnd.github+json" \
  -H "Authorization: Bearer $GITHUB_TOKEN" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  https://api.github.com/repos/ctwoodwa/Sunfish/rulesets \
  -d @.github/rulesets/release-tags.json
```

### Option C — Edit in place, then re-import

If you tweak a rule in the UI, export the result (⋯ menu on the ruleset →
**Export**) and overwrite the corresponding file here so this folder stays
in sync with the live configuration.

## Policy choices — deliberately omitted

| Rule | Why it is off today | Turn on when |
|---|---|---|
| `required_signatures` (cryptographic commit signing) | `GOVERNANCE.md` commits to **DCO sign-off** (`git commit -s`), not GPG/Sigstore signing. Enabling this would block DCO-only contributors. | The project standardizes on signed commits (would be a governance revision). |
| Branch-creation restrictions | External contributors need to push feature branches. | Never, under the current BDFL + open-contributor model. |
| Dependabot / Actions bypass | Dependabot PRs should pass CI like any human PR. | Never — keep the gate uniform. |
| `release/*` branch rule | No long-lived release branches exist yet; Sunfish tags releases off `main`. | A release-branch workflow is introduced. |

## Review gates and governance linkage

These rulesets encode the review gates described in `GOVERNANCE.md`
(“Decision types and mechanisms”) and the ICM **07_review** stage:

- CODEOWNERS review → maps to the ownership table in `.github/CODEOWNERS`.
- Required status checks → map to the `CI` and `CodeQL` workflows.
- Squash-only + linear history → matches the existing commit-message
  convention and keeps the `CHANGELOG.md` generation straightforward.

When `GOVERNANCE.md` transitions fire (for example, the maintainer tier is
added), revise `bypass_actors` here in the same PR as the governance change
so the live rules and the documented posture stay aligned.
