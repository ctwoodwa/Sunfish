# ADR 0039 — Required-Check Minimalism on Public OSS Repos

**Status:** Accepted (2026-04-26)
**Date:** 2026-04-26
**Resolves:** A policy decision implicit in PR #138 (`chore(infra): trim ruleset to always-on checks + add --delete flag for rollback`) — when the Ruleset's required-status-check list was trimmed from 7 to 4 entries — and earlier in PR #133 (`docs(security): auto-merge scope audit — no automation gate, manual CLI only`) where the audit established that CI is the only merge gate. This ADR backfills the rationale: which CI checks belong on the required list, which do not, and why path-conditional gates are intentionally advisory rather than blocking on a public OSS repo.

A predecessor ruleset named *"Protect main"* was deleted during PR #138 verification because it required path-conditional checks under `strict_required_status_checks_policy: true` and was permanently blocking docs-only PRs. That deletion was the proof of the rule this ADR codifies.

---

## Context

Sunfish runs ~30 GitHub Actions workflows. Some fire on every PR (lint, CodeQL, semgrep). Others are conditional — gated by `paths:` or `paths-ignore:` filters in the workflow YAML — so they fire only when relevant files change:

| Check | Fires on | Why |
|---|---|---|
| Lint PR commits | Every PR | Commitlint runs on every commit message |
| Analyze (csharp) | Every PR | CodeQL native scan |
| CodeQL | Every PR | Aggregate CodeQL workflow |
| semgrep-cloud-platform/scan | Every PR | SAST coverage |
| Build & Test | Code PRs only (`paths-ignore: docs/**`, `**/*.md`) | Heavy; skipped on docs-only |
| CSS logical-properties audit | UI PRs only (`paths: packages/ui-adapters-*/**`) | Style audit |
| Locale completeness check | i18n PRs only (`paths: **/SharedResource*.resx`) | LocUnused parity |
| A11y Storybook test-runner | UI PRs only (`paths: packages/ui-adapters-*/**`) | bUnit-axe rendered audit |
| Global-UX Gate aggregate | UI PRs only (rolls up the four above) | Aggregate verdict |

The conflict surfaced when GitHub's `strict_required_status_checks_policy: true` setting was combined with the longer required-checks list (the 9 above). Strict mode means: **a required check must report a *successful* conclusion before merge is allowed.** A check that did not run because its `paths:` filter excluded the PR reports as `expected` (Pending) forever — it does not report `success`. The GitHub UI shows it as *"Waiting for status to be reported,"* and the PR cannot merge.

Concretely: a one-line README typo PR would never merge because it would never trigger Build & Test, A11y Storybook, the CSS audit, the locale check, or the Global-UX Gate. All four would sit in `Pending` indefinitely.

The deleted *"Protect main"* ruleset hit this trap on the very first docs-only PR that was opened against it. The fix in PR #138 was to trim the required-check list to only checks that fire on every PR.

The question this ADR answers: **what is the policy for required-check selection on a public OSS repo where docs-only PRs are common, multiple workflows are path-gated, and the maintainer wants merge to be self-service for trivial PRs?**

---

## Decision drivers

- **Strict-mode + missing-required = permanent block.** This is GitHub's published behavior for both legacy branch protection AND Rulesets. The decision-driver framing must accept it as a constraint, not a problem to be designed around.
- **Public OSS repo + docs-friendly contribution flow.** The project's contribution posture (per README and the *"reference implementation alongside the book"* positioning) is that small docs PRs from external contributors should land cleanly. Anything that makes "fix a typo" require a maintainer override is a contribution-flow tax.
- **Solo-maintainer + auto-merge default.** Per the user's `feedback_pr_push_authorization` memory, every PR ships as `gh pr merge --auto --squash`. CI is the only gate. A required check that hangs indefinitely defeats auto-merge.
- **Defense-in-depth lives in many places, not just required-checks.** A check being advisory (visible on the PR, not blocking merge) is not the same as the check being absent. Advisory checks still:
  - Show up in the PR's checks panel for human review.
  - Run on `main` after merge and surface failures via standard GitHub Actions notifications.
  - Are visible to anyone scanning the repo's recent workflow runs.
  - Block subsequent dependent jobs that depend on them (e.g., A11y depends on Build).
- **Cost of over-requiring is asymmetric.** A required check that should be advisory blocks merge for valid PRs (false positive). An advisory check that should be required lets a regression land for hours-to-days before the post-merge run catches it (delayed detection). On a solo-maintainer project, the latter is recoverable; the former is a contribution-flow break.
- **Pre-release posture.** Until v1, the project tolerates more churn — a regression caught post-merge is `git revert` plus a follow-up PR. Post-v1, the calculus shifts and some advisory checks become required (see Revisit triggers).

---

## Considered options

### Option A — Require everything (the predecessor "Protect main" approach)

Require all 9 checks listed above. Trust strict mode to enforce them.

- **Pro:** Highest theoretical safety; nothing lands without every gate green.
- **Con:** Permanent block on docs-only PRs (the actual experience that deleted the predecessor ruleset).
- **Con:** Permanent block on UI-only PRs that don't touch i18n (locale check sits Pending).
- **Con:** Maintainer override becomes the merge path for ~half of PRs, defeating the auto-merge convention.
- **Rejected** — this is the option the deleted *"Protect main"* ruleset implemented. Its deletion is the experimental evidence.

### Option B — Require only checks that fire on every PR (this ADR)

Require the four always-on checks: Lint, Analyze (csharp), CodeQL, semgrep. Treat path-conditional checks (Build & Test, A11y Storybook, CSS audit, Locale completeness, Global-UX Gate) as advisory.

- **Pro:** Strict mode + always-on required checks composes cleanly. Every PR triggers all four; all four can succeed; merge proceeds.
- **Pro:** Docs-only PRs land via auto-merge. UI-only PRs land via auto-merge. Code-only PRs land via auto-merge. The path-conditional checks still run when they apply, still report on the PR, and still block by failing — they just don't block by *not running*.
- **Pro:** Maintainer override is reserved for genuine emergencies, not contribution-flow workarounds.
- **Con:** A regression in UI a11y, CSS logical-properties, or locale completeness can land if the maintainer doesn't notice the failed advisory check. Mitigation: post-merge runs on `main` are visible in the Actions tab; the maintainer's session-end review catches them.
- **Con:** External contributors might see green required-checks and assume the PR is fully validated when in fact the path-conditional checks are still running. UI mitigation: the PR checks panel shows all checks regardless of required/advisory status.
- **Adopted.**

### Option C — Restructure all path-conditional workflows to fire on every PR (no-op when out of scope)

Rewrite Build & Test, A11y, CSS audit, Locale check, Global-UX Gate to NOT use `paths:` / `paths-ignore:` filters at all. Instead, run an early step that detects "is this PR in scope?" and exits 0 (success) when out of scope.

- **Pro:** Strict mode + full required-check list works. Every check fires on every PR and reports success when out-of-scope.
- **Pro:** No false sense of safety from advisory checks; required is required.
- **Con:** Significant rework of ~5 workflows. Each needs a "is this PR in scope?" detection step duplicating the logic that the YAML `paths:` filter expressed declaratively.
- **Con:** Wastes CI minutes — every docs-only PR runs the full Build & Test workflow's container startup just to exit 0 in step 1. On a public repo with free GHA minutes this is "only" wasted compute, but it's also wasted wall-clock time on every PR (slow start of every container).
- **Con:** The `paths:` filter is the GitHub-recommended pattern for scope-gating workflows. Working around it loses the platform's intended affordance.
- **Defer.** This is the right answer if Option B's advisory-check experience proves insufficient. Until then, the rework cost is unjustified.

### Option D — Use required checks selectively per branch / per file pattern

Different ruleset rules for different `conditions.ref_name.include` or per-file-pattern conditions. e.g., docs-only PRs require only Lint + CodeQL; code PRs require everything.

- **Pro:** Matches the actual scope-of-impact gradient; right gates for right PRs.
- **Con:** GitHub Rulesets do NOT support per-file-pattern conditions on a single branch's protection. The condition shape is ref-based (branches matter; file patterns don't).
- **Con:** The closest workaround — multiple Rulesets layered on `main` with different conditions — is not what `conditions.ref_name` means. The feature does not exist today.
- **Rejected** — not implementable on the current API surface.

### Option E — Advisory mode on the workflow side (continue-on-error)

Mark all path-conditional workflows with `continue-on-error: true` so they never fail, only succeed-with-warnings.

- **Pro:** Required-check list can stay long; nothing ever blocks.
- **Con:** Defeats the purpose of running the check at all — failures are invisible in the PR UI; the check shows green when it should be red.
- **Con:** Worst-of-both-worlds: keeps the operational complexity of long required-check lists, removes the safety value.
- **Rejected.**

---

## Decision

**Adopt Option B: only require checks that fire on EVERY PR. The required-check list on `main` is exactly four entries: Lint PR commits, Analyze (csharp), CodeQL, semgrep-cloud-platform/scan.**

All other CI gates (Build & Test, A11y Storybook test-runner, CSS logical-properties audit, Locale completeness check, Global-UX Gate aggregate) are **advisory** — they run when their `paths:` filter matches, they report on the PR's checks panel, and they block merge by *failing* but not by *not running*.

This decision is encoded as the trimmed `required_status_checks` list in [`infra/github/main-ruleset.json`](../../infra/github/main-ruleset.json) with the rationale captured in the `_comment_required_checks_choice` annotation on the parameters object.

### When the required-check list grows again

A check may be added to the required list when ALL of the following are true:

1. **The check fires on EVERY PR** (no `paths:` / `paths-ignore:` filter, OR the filter is structured to always reach a success/failure conclusion via in-job scope detection).
2. **The check is fast enough to not delay merge** (target: <5 min p95 to first conclusion). Background: see PR #119's p95 baseline.
3. **The check has been advisory for ≥2 weeks with zero false-positive failures attributable to flakiness** — the check must be a reliable signal before being made blocking.

When all three hold, add the check to `infra/github/main-ruleset.json` `required_status_checks`, run `apply-main-ruleset.sh --evaluate` if on a plan that supports it, observe one week, then re-apply without `--evaluate` to make it active.

### When the required-check list shrinks

Any check on the required list that begins to skip itself on common PR shapes (i.e., grows a `paths:` filter) MUST be removed from required-checks in the same PR. The `_comment_required_checks_choice` annotation must be updated to record the new always-on contract.

---

## Consequences

### Positive

- Auto-merge works for all PR shapes: docs-only, UI-only, code-only, mixed. No more permanent-block surprises.
- Required-check list reflects what genuinely must pass for safety on a public OSS repo with the project's current posture.
- The four required checks are cheap (Lint, CodeQL, semgrep) — they finish fast and don't bottleneck merge.
- Path-conditional checks continue to run, continue to be visible, continue to block by failing. The protection isn't weaker; it's correctly scoped.
- Future re-additions to the required list have a clear three-condition test (fires-on-every-PR, fast, low-flake-history). No bikeshedding.

### Negative

- A failed advisory check does not block merge. A maintainer who skims the PR may merge with an a11y or CSS regression visible in the checks panel. Mitigation: session-end review of `main`'s post-merge runs (which is the maintainer's existing habit per the *"docs(cleanup) audit"* PRs).
- External contributors might mistake required-checks-green for "fully validated." The PR template should be updated to call out advisory checks (deferred — separate small PR).
- The four required checks ARE a single point of failure: if Lint or CodeQL has an outage, no PR merges. This is acceptable because GHA outages are visible system-wide and the maintainer can use the bypass-actor in the Ruleset to merge in an emergency.
- The trimmed list is a public-repo posture decision. If/when Sunfish flips to private (per the *"private until LLC"* governance memory), the contribution-flow tax of a long required-check list disappears (no external contributors to optimize for) and the trimmed list could expand. Tracked as a Revisit trigger.

---

## Revisit triggers

This ADR should be re-opened — and the required-check list re-examined — when **any one** of the following occurs:

1. **Repo flips to private (LLC governance milestone).** External contribution flow stops being a concern; the required-check list can grow more aggressively because maintainer-override-on-block is acceptable for the maintainer's own PRs.
2. **A regression lands via advisory-check-failed-but-not-blocking that costs >2 hours of recovery work.** That's the threshold where Option C's restructure-all-workflows cost becomes justified. Open a follow-up ADR.
3. **First v1 release ships.** Post-v1, the per-PR safety bar rises. Some advisory checks should be promoted to required (specifically: Build & Test, once it's restructured to fire on every PR per Option C; A11y Storybook, once the rendered-test surface stabilizes).
4. **Second contributor joins.** Two-person team can use `required_approving_review_count: 1` (currently `0`) and the Ruleset's `pull_request` rule in addition to status checks. Required-check list policy doesn't change but the surrounding posture does.
5. **GitHub adds path-aware required-check semantics.** Specifically, a `required_status_checks` field that accepts "satisfied if the workflow's path conditions caused it to skip." This would make Option A safe and the trimmed list could re-expand.

---

## References

- **PRs that this ADR ratifies:**
  - PR #126 — `feat(infra): rewrite main branch-protection as GitHub Ruleset` — initial migration; the PR title's "handles paths-ignore properly" claim was incorrect (Rulesets have the same hazard as legacy).
  - PR #133 — `docs(security): auto-merge scope audit — no automation gate, manual CLI only` — established that CI is the only merge gate, which makes required-check selection a load-bearing decision.
  - PR #138 — `chore(infra): trim ruleset to always-on checks + add --delete flag for rollback` — the trimming this ADR codifies; verified in production after the deleted *"Protect main"* ruleset's docs-PR-block experience.
- **Predecessor experiment:**
  - The deleted *"Protect main"* ruleset — required all 9 checks; permanently blocked the first docs-only PR opened against it; deleted as part of PR #138's Phase 1 verification. The experiment that proved Option A doesn't work.
- **Companion ADR:**
  - [ADR 0038](./0038-branch-protection-via-rulesets.md) — *how* protection is applied (Rulesets API, idempotent script, `--delete` rollback). ADR 0039 is *what* to require under that mechanism.
- **Related ADR:**
  - [ADR 0037](./0037-ci-platform-decision.md) — staying on GitHub Actions; this required-check policy is GHA-specific in encoding (it would re-derive on Forgejo Actions if a migration ever happens).
- **Files this ADR governs:**
  - [`infra/github/main-ruleset.json`](../../infra/github/main-ruleset.json) — the trimmed `required_status_checks` list with the `_comment_required_checks_choice` annotation.
  - [`infra/github/README.md`](../../infra/github/README.md) — paths-ignore hazard discussion.
- **Memory:**
  - User's `reference_github_actions_paths_schema` — the global rule that GH Actions schema rejects `paths:` + `paths-ignore:` on the same trigger. Related but distinct: that is a workflow-loading error; ADR 0039 addresses the Pending-state hazard for required checks under strict mode.
- **External:**
  - GitHub docs on required status checks behavior under strict mode: `https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches#require-status-checks-before-merging`.
