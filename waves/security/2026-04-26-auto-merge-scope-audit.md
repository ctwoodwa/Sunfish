# Auto-Merge Scope Audit — Public-Repo Defense Review

**Date:** 2026-04-26
**Audit scope:** Determine whether any automation in this repository auto-merges
pull requests, and whether such automation is properly scoped to repo-owner
(and other trusted) authors so external/fork PRs cannot ride the same path.
**Verdict:** **GREEN — no scoping change required.** The repository has no
automation that performs `gh pr merge --auto` (or equivalent) on behalf of
arbitrary PR authors. The maintainer's "feature-branch + PR + `gh pr merge
--auto --squash`" pattern is a CLI habit, not a workflow. Branch protection
is the merge gate.

---

## 1. Background

`github.com/ctwoodwa/Sunfish` is a public repository. Per the maintainer
preference recorded in `feedback_pr_push_authorization`, every change ships
as feature-branch → PR → `gh pr merge --auto --squash`. That pattern is safe
when the maintainer authors every PR. On a public repo, fork PRs from any
GitHub user could in principle ride the same path **if** a workflow performed
auto-merge based on labels, CI-green status, or Dependabot tags.

This audit checks whether such a workflow exists, and if so, whether it is
properly scoped.

## 2. Discovery surface

### 2.1 Workflows scanned

All six workflow files under `.github/workflows/` were searched for
auto-merge primitives:

| Workflow | Auto-merge surface? |
|---|---|
| `ci.yml` | None — build + unit tests on `pull_request` and `push:main` |
| `codeql.yml` | None — CodeQL analysis on `pull_request`, `push:main`, weekly cron |
| `commitlint.yml` | None — `wagoid/commitlint-github-action@v6` lint only |
| `docs.yml` | None — DocFX build + Pages deploy on `push:main` |
| `global-ux-gate.yml` | None — 10 gate jobs (CSS-logical, locale, a11y, analyzers, XLIFF, CLDR, plan-2, RESX-XSS, cross-plan, aggregate) |
| `sbom.yml` | None — CycloneDX SBOM on `release:published` |

Search patterns (case-insensitive): `auto.?merge`, `automerge`,
`enable-pull-request-automerge`, `pascalgn`, `peter-evans`, `gh pr merge`,
`merge_method`, `github-script` calls to merge endpoints, `pull_request_target`,
`workflow_run`. **Zero matches** in any workflow. The single match for
`merge` in `.github/` is in `rulesets/main-branch.json` line 23:
`"allowed_merge_methods": ["squash"]` — that is a branch-protection
configuration, not an automation.

### 2.2 Third-party bot configs

| Path | Present? |
|---|---|
| `.mergify.yml` | No |
| `.github/mergify.yml` | No |
| Other (Bulldozer, Kodiak, Probot-auto-merge) | None found |

### 2.3 Repository-level auto-merge setting

```json
$ gh api repos/ctwoodwa/Sunfish --jq '{allow_auto_merge, allow_squash_merge,
    allow_merge_commit, allow_rebase_merge, delete_branch_on_merge,
    allow_update_branch}'
{
  "allow_auto_merge": true,
  "allow_merge_commit": false,
  "allow_rebase_merge": false,
  "allow_squash_merge": true,
  "allow_update_branch": false,
  "delete_branch_on_merge": true
}
```

`allow_auto_merge: true` enables the **feature**, but does not itself merge
anything — a user (or bot) must still call `gh pr merge --auto --squash`
on a specific PR to schedule it. That call requires push access (or PR-level
write via the GitHub Actions token, which no workflow uses for this purpose).

### 2.4 Dependabot configuration

`.github/dependabot.yml` defines two ecosystems:

- `nuget` weekly (Mondays), 5 open-PR limit, `aspnetcore` + `testing` groups
- `github-actions` weekly (Mondays)

**Dependabot is NOT auto-merge-wired.** No `auto-merge` field in the config
and no companion workflow listens for `dependabot[bot]` PRs and calls merge
endpoints. Dependabot PRs go through the same human gate as every other PR.

### 2.5 Branch protection

Source-of-truth lives in two places that are intentionally redundant during
the migration to the Ruleset format (see PR #126 still open):

- `.github/branch-protection-main.json` — legacy classic-protection input
- `.github/rulesets/main-branch.json` — current Ruleset (active)

Both require:

- Pull request with **1 approving review** (CODEOWNERS-required in the Ruleset)
- All conversations resolved
- Required status checks: `Build & Test` + `Analyze (csharp)` (Ruleset adds
  `Global-UX Gate (aggregate)` per branch-protection JSON)
- Linear history; squash-only; deletion blocked; force-push blocked
- **Bypass:** repository Admin role only (`bypass_actors[0].actor_id: 5`)

This is the actual merge gate. Even if a fork PR (or Dependabot) somehow
invoked auto-merge, GitHub would still block the merge until the required
review and checks resolve. Required-review with `require_code_owner_review`
(active in the Ruleset) means the maintainer must explicitly approve.

## 3. The maintainer's CLI pattern is not automation

The pattern recorded in `feedback_pr_push_authorization` is the maintainer
typing `gh pr merge --auto --squash` from their local terminal **after** they
push a PR they themselves authored. GitHub then schedules the merge to fire
when branch-protection conditions resolve. Three properties matter for
public-repo defense:

1. **It runs as the maintainer's user identity.** The auth is the local
   `gh` CLI's PAT/token, scoped to the maintainer.
2. **It is invoked once per PR, by hand.** No webhook, no workflow, no bot
   reacts to label/status events to invoke it on someone else's PR.
3. **It is gated by branch protection regardless.** A fork-PR contributor
   running the same command on their own PR would either lack permission
   (PRs from forks cannot self-enable auto-merge without write access to the
   base repo) or, if granted, would still need maintainer review before the
   merge fires.

So a fork-PR author cannot weaponize the maintainer's CLI habit. The only
attack surface would be a workflow that ran `gh pr merge --auto` on PRs it
did not own — and no such workflow exists.

## 4. What was changed by this PR

**No code change.** This PR adds only this audit report. The current state
is correctly scoped:

- No workflow performs auto-merge → nothing to guard with `if:` filters.
- Branch protection requires CODEOWNERS approval → external PRs cannot land
  without the maintainer.
- Dependabot has no auto-merge wiring → its PRs follow the same gate.

## 5. Defense-in-depth options (not adopted today)

If, in the future, the project introduces a workflow that auto-merges (for
example, an "auto-merge Dependabot patch updates" job — a common pattern),
the following guards should be applied at that time:

```yaml
# Restrict to repo-owner + Dependabot.
if: >-
  github.event.pull_request.user.login == 'ctwoodwa' ||
  github.event.pull_request.user.login == 'dependabot[bot]'
```

Or, more robustly, drive eligibility off CODEOWNERS plus the
`pull_request_review` event so that a maintainer must approve before the
merge schedules. Either pattern keeps fork PRs out of the auto-merge path
even when one is added later.

For Dependabot specifically, the canonical safe pattern is:

```yaml
on: pull_request_target
jobs:
  dependabot-automerge:
    if: github.actor == 'dependabot[bot]'
    runs-on: ubuntu-latest
    steps:
      - uses: dependabot/fetch-metadata@v2
      - if: steps.metadata.outputs.update-type == 'version-update:semver-patch'
        run: gh pr merge --auto --squash "$PR_URL"
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          PR_URL: ${{ github.event.pull_request.html_url }}
```

The `pull_request_target` trigger runs in the base-repo context (so the
secret is available) but only the `if: github.actor == 'dependabot[bot]'`
guard makes it safe — without that guard, any fork PR would have a
checked-out script in base-repo context, which is the well-known
`pull_request_target` privilege-escalation footgun. **This is documented
here so a future contributor adding such a workflow does not paste an
unscoped version.**

## 6. Self-verdict

**GREEN.** Audit complete. No scoping change applied because none was
needed: there is no auto-merge automation in this repository today. The
existing branch-protection ruleset + the maintainer-only CLI pattern is
the merge gate, and it is correctly restrictive for a public repo.

If this changes (a workflow is added that runs `gh pr merge --auto` or
equivalent on PRs the workflow did not author), re-run this audit and
apply the scoping patterns in §5.

---

**Audited by:** Claude (worktree-agent-ac6c238135d61d785)
**Cross-references:**

- `.github/dependabot.yml`
- `.github/rulesets/main-branch.json`
- `.github/rulesets/README.md`
- `.github/branch-protection-main.json`
- `.github/workflows/*.yml`
