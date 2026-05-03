# Fork-PR Approval Audit — github.com/ctwoodwa/Sunfish

**Date:** 2026-04-26
**Auditor:** Claude (read-only)
**Scope:** GitHub repository Settings → Actions → General — fork-PR
workflow approval policy and adjacent hardening knobs.
**Repo state:** Public (`visibility: public`, confirmed via
`gh api repos/ctwoodwa/Sunfish`).

---

## TL;DR

- Fork-PR approval is currently set to **`first_time_contributors`** —
  the middle policy. First-time contributors require maintainer
  approval; returning external contributors do not.
- `GITHUB_TOKEN` is **NOT** allowed to approve PRs
  (`can_approve_pull_request_reviews: false`) — good, leave as-is.
- `default_workflow_permissions` is **`read`** — minimum-privilege
  default for `GITHUB_TOKEN` — good, leave as-is.
- **Recommendation:** tighten the fork-PR approval policy from
  `first_time_contributors` to `all_external_contributors` for a
  solo-maintainer public repo. Cheapest defense-in-depth for fork-PR
  workflow attacks (e.g. malicious workflow edits, credential
  exfiltration via test fixtures, miner injection).
- **No changes were made.** This is a read-only audit. The exact
  `gh api` command for the human owner to apply is in section 5
  below.

---

## 1. Endpoints checked

All read-only `GET` calls against `repos/ctwoodwa/Sunfish/...`:

| Endpoint | Result | Response |
|---|---|---|
| `actions/permissions` | 200 | `{"enabled":true,"allowed_actions":"all","sha_pinning_required":false}` |
| `actions/permissions/access` | 422 | "Access policy only applies to internal and private repositories." (expected for public repo) |
| `actions/permissions/workflow` | 200 | `{"default_workflow_permissions":"read","can_approve_pull_request_reviews":false}` |
| `actions/permissions/fork-pr-contributor-approval` | 200 | `{"approval_policy":"first_time_contributors"}` |
| `actions/permissions/artifact-and-log-retention` | 200 | `{"days":90,"maximum_allowed_days":90}` |
| `actions/permissions/fork-pr-workflows` | 404 | (legacy/wrong path — the real endpoint is `fork-pr-contributor-approval`) |

Note on the task brief: the docs at
<https://docs.github.com/en/rest/actions/permissions> list **three**
enum values for `approval_policy`, not four:
`first_time_contributors_new_to_github`,
`first_time_contributors`, `all_external_contributors`. The brief
mentioned a fourth value `all` — that does not appear in the current
REST API surface.

---

## 2. Current setting — fork-PR approval

```
GET /repos/ctwoodwa/Sunfish/actions/permissions/fork-pr-contributor-approval
→ {"approval_policy":"first_time_contributors"}
```

### What this means in practice

| Contributor class | Workflow runs without approval? |
|---|---|
| Brand-new GitHub account opening their first-ever PR anywhere | **No** — needs approval |
| Someone who has contributed to other repos but never to Sunfish | **No** — needs approval (first time *here*) |
| Someone who has had at least one prior PR merged into Sunfish | **Yes** — runs immediately |

The current policy catches drive-by attackers but trusts anyone who
has gotten one PR through before. For a solo-maintainer public repo
with zero forks today (`forks_count: 0`) and no
"returning external contributor" cohort to disrupt, the looser
policy buys nothing.

---

## 3. Recommendation

**Change `approval_policy` from `first_time_contributors` to
`all_external_contributors`.**

### Rationale

1. **Threat model fit.** Solo-maintainer + public repo + tight CI =
   every fork PR run is a chance to leak `secrets.*`, mine crypto on
   GitHub-hosted runners, or pivot via a compromised workflow file.
   The cost of one incident dwarfs the cost of one extra
   "Approve and run" click.
2. **No legitimate-contributor friction yet.** `forks_count: 0`,
   `subscribers_count: 0`. There is nobody whose flow this disrupts.
   When the project grows enough that the policy hurts throughput,
   loosen it then.
3. **Pairs with existing hardening.** `default_workflow_permissions:
   read` and `can_approve_pull_request_reviews: false` are both
   correctly set; tightening the fork-PR gate completes the
   defense-in-depth posture.
4. **Reversible.** One `gh api -X PUT` call to roll back if it
   becomes a contributor-experience problem.

### Counter-considerations

- Manual click required on every fork PR — adds latency for
  legitimate external contributors. Acceptable at current contributor
  volume; reassess if/when the project gains regular outside
  contributors.
- Does not protect against branch-PRs from collaborators with write
  access — for that, use `CODEOWNERS` + branch protection
  (out of scope for this audit).

---

## 4. Adjacent hardening — already correct, leave alone

| Setting | Current | Verdict |
|---|---|---|
| `default_workflow_permissions` | `read` | Good — `GITHUB_TOKEN` defaults to read-only; per-workflow `permissions:` blocks grant what they need. |
| `can_approve_pull_request_reviews` | `false` | Good — `GITHUB_TOKEN` cannot self-approve PRs. Critical for public repos. |
| `allowed_actions` | `all` | Acceptable — tightening to `selected` adds maintenance burden for a solo project. Revisit if a supply-chain incident hits a popular Action. |
| `sha_pinning_required` | `false` | Acceptable — pin-by-SHA in workflow files is preferable but not enforced at the policy level. Could be flipped to `true` once all workflows use pinned SHAs. |
| `secret_scanning` | `enabled` | Good. |
| `secret_scanning_push_protection` | `enabled` | Good. |
| `dependabot_security_updates` | `enabled` | Good. |

---

## 5. Exact command for human owner to apply

**Do not run this from an agent session.** Owner runs locally or
performs the equivalent click in
Settings → Actions → General → "Fork pull request workflows from
outside collaborators".

```bash
gh api \
  -X PUT \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  /repos/ctwoodwa/Sunfish/actions/permissions/fork-pr-contributor-approval \
  -f approval_policy=all_external_contributors
```

Verify:

```bash
gh api repos/ctwoodwa/Sunfish/actions/permissions/fork-pr-contributor-approval
# expect: {"approval_policy":"all_external_contributors"}
```

Rollback (one-liner):

```bash
gh api -X PUT \
  /repos/ctwoodwa/Sunfish/actions/permissions/fork-pr-contributor-approval \
  -f approval_policy=first_time_contributors
```

---

## 6. References

- [REST API — Actions permissions](https://docs.github.com/en/rest/actions/permissions)
- [Approving workflow runs from public forks](https://docs.github.com/en/actions/managing-workflow-runs/approving-workflow-runs-from-public-forks)
- [Security hardening for GitHub Actions](https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions)
- GitHub Security Lab, "Keeping your GitHub Actions and workflows secure: Preventing pwn requests" — covers the fork-PR threat model that this setting mitigates.

---

## 7. Audit verdict

**GREEN** — All five Actions-permissions endpoints accessible via
REST. Findings are unambiguous. One concrete, low-risk, reversible
recommendation. No settings were changed by this audit.
