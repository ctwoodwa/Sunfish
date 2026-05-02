---
id: 38
title: Branch Protection via GitHub Rulesets (not legacy branch-protection)
status: Accepted
date: 2026-04-26
tier: governance
concern:
  - governance
  - threat-model
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0038 — Branch Protection via GitHub Rulesets (not legacy branch-protection)

**Status:** Accepted (2026-04-26)
**Date:** 2026-04-26
**Resolves:** A maintainer decision made implicitly during PR #126 (`feat(infra): rewrite main branch-protection as GitHub Ruleset`) and refined in PR #138 (`chore(infra): trim ruleset to always-on checks + add --delete flag for rollback`). Both PRs landed without an ADR; this document backfills the architectural rationale so future-maintainers (and future-self) understand *why* Sunfish chose the Rulesets API over the legacy branch-protection endpoint, what the API gives and doesn't give, and what the reproducible-apply contract looks like.

---

## Context

GitHub provides two distinct mechanisms for protecting a branch:

1. **Legacy branch protection** (`PUT /repos/{owner}/{repo}/branches/{branch}/protection`) — the original endpoint, ~2016-vintage. Per-branch, single-rule shape, no preview/observe mode, no native rollback semantics.
2. **Rulesets** (`POST/PATCH /repos/{owner}/{repo}/rulesets`) — the modern replacement, ~2023. Multi-pattern targets via `conditions.ref_name.include`, layered rule types in one document, named bypass actors, an `enforcement: evaluate` mode for canary observation, and clean delete semantics by id.

Before PR #126 Sunfish enforced main with the legacy endpoint via `infra/github/apply-branch-protection.sh` + `branch-protection-main.json`. The need to migrate surfaced from a related PR (#116, *"skip heavy gates on docs-only PRs via paths-ignore"*): once the heavy CI workflows started skipping themselves on docs-only PRs, the legacy required-status-check list left those PRs in a permanent *"Pending"* state — the legacy API has no concept of "this required check intentionally didn't run; treat as not-applicable."

PR #126 rewrote the protection as a Ruleset on the assumption that Rulesets handled the paths-ignore case better. PR #138 discovered they do not (see ADR 0039) but kept the Ruleset migration anyway because the operational properties (rollback, evaluate mode, layered rules) were independently worth having. This ADR ratifies that reasoning.

---

## Decision drivers

- **Rollback story** — The legacy endpoint has no DELETE; rollback means re-PUTting an empty/permissive payload, which is itself a mutation that can drift. Rulesets have a numeric `id` and a clean DELETE endpoint, so `apply-main-ruleset.sh --delete` is one command and idempotent.
- **Canary mode** — Rulesets support `enforcement: evaluate` (rule visible on PRs but does not block merges). This is the correct way to verify a new rule shape before flipping to `active`. The legacy endpoint has no equivalent; the only test of a new rule was "merge a real PR and see what happens." Note: `evaluate` is an Enterprise-plan-only feature on github.com; on lower plans the substitute is a canary branch test.
- **Multi-rule layering** — A Ruleset payload can declare `deletion`, `non_fast_forward`, `required_status_checks`, and `pull_request` rules together as one logical policy. The legacy endpoint flattens these into a single object, so changes to any one field re-PUT the whole policy.
- **Bypass actor model** — Rulesets express bypass via `bypass_actors` with `actor_type` and `bypass_mode` (always | pull_request). Legacy branch protection's bypass is implicit (admins always bypass; everyone else doesn't). The explicit named-actor model is auditable.
- **Forward direction of GitHub investment** — GitHub's documented direction is that new safety primitives ship on Rulesets first. Staying on legacy means missing future improvements.
- **Solo-maintainer + reproducible-IaC posture** — `apply-main-ruleset.sh` runs idempotently (POST if absent, PATCH if present, DELETE on `--delete`); the JSON payload + script + this ADR together mean any future maintainer can reproduce the exact protection state from the repo.

---

## Considered options

### Option A — Keep legacy branch protection as-is

Continue with `infra/github/branch-protection-main.json` + `apply-branch-protection.sh`. No migration cost.

- **Pro:** Zero work; the endpoint still works and is not deprecated.
- **Con:** No rollback primitive. Reverting to a known-good state requires manually re-PUTting the prior payload (which has its own version).
- **Con:** No canary mode. New required checks have to be tested in production.
- **Con:** Cannot stack multiple rules (e.g., `deletion` + `non_fast_forward` + `required_status_checks` + `pull_request`) without a single fragile JSON object.
- **Rejected** — operational properties of Rulesets are strictly better and the migration cost is low.

### Option B — Rewrite as GitHub Ruleset (this ADR)

Replace the legacy script + JSON with `apply-main-ruleset.sh` + `main-ruleset.json`. Same idempotent shape: POST-or-PATCH on apply, DELETE on rollback.

- **Pro:** All four decision-driver properties (rollback, evaluate, layered rules, bypass model).
- **Pro:** Comments-in-JSON pattern (`_comment_*` keys stripped before POST) lets the payload itself document its own rationale — the payload IS the readable spec.
- **Pro:** GitHub's docs and UI now lead with Rulesets; legacy branch protection is the back-compat path. New maintainer ramp-up is easier on the modern API.
- **Con:** Requires Rulesets API permissions on the GitHub token; on Org repos this needs an org admin step. Not a blocker for personal repos.
- **Con:** `evaluate` mode is Enterprise-only on github.com; on Free/Pro plans the script accepts the flag and the API rejects it. The README documents the canary-branch substitute pattern.
- **Adopted.**

### Option C — Maintain both (legacy + Ruleset) in parallel

Keep both files, apply both. Belt-and-suspenders for the migration window.

- **Pro:** Defense in depth; a Ruleset misconfiguration doesn't drop protection because legacy is still in place.
- **Con:** GitHub does NOT compose them additively — when both exist, the resolution is "either one rule OR the other can block, but the UI/PR-checks experience becomes confusing because two protection sources are visible." For a solo-maintainer repo this is more confusing than it's worth.
- **Con:** Doubles the maintenance surface for no real benefit once the Ruleset is verified.
- **Rejected** for steady state. Was implicitly the state during the PR #126 → PR #138 transition window (both files present), but the legacy script + JSON should be retired once the Ruleset is verified in production.

### Option D — Manage protection via GitHub UI (no IaC)

Click-ops in the repo Settings → Rulesets / Branches UI.

- **Pro:** Lowest friction for one-time setup.
- **Con:** Not reproducible. Any future maintainer (or DR rebuild) starts from scratch.
- **Con:** No diff history; changes are invisible to PR review.
- **Rejected** for a public OSS repo where the protection policy is part of the project's auditable security posture.

---

## Decision

**Adopt Option B: maintain branch protection on `main` via the Rulesets API. Reproduce via `infra/github/apply-main-ruleset.sh` + `infra/github/main-ruleset.json`. Retire the legacy `apply-branch-protection.sh` + `branch-protection-main.json` after the Ruleset has been verified in production for two weeks (target: 2026-05-10).**

The decision rests on three premises:

1. **Operational properties are strictly better.** Rollback, canary mode (where available), layered rules, and the bypass-actor model are each individually worth the migration. Together they upgrade the protection from "configured once, hope it stays right" to "reproducible, observable, and revertable."
2. **The migration is cheap and reversible.** The script shape is identical (idempotent shell wrapping a JSON payload). If Rulesets prove worse in practice, the legacy script and JSON are still in the repo; reverting is one PR.
3. **Rulesets are GitHub's forward direction.** New protection primitives ship on Rulesets first. Staying on the modern API means future improvements (e.g., better path-condition support, finer-grained bypass) come automatically.

### Reproducible-apply contract

The `apply-main-ruleset.sh` script holds the contract:

- **Idempotent**: `POST` if no ruleset with the target name exists; `PATCH` by numeric id otherwise. Re-running converges on the same state.
- **Comment-stripping**: All `_comment_*` keys (recursively, including nested under `parameters`) are stripped before POST. The GitHub API rejects unknown top-level fields; this lets the JSON payload carry its own rationale.
- **Three flags**: `--dry-run` (resolve + print payload, no mutation), `--evaluate` (override `enforcement: evaluate` for the apply), `--delete` (look up by name + DELETE; no-op if absent). All flags are mutually exclusive where it matters and the script enforces the constraints.
- **JSON validation**: jq if available, Python fallback otherwise. The agent sandbox usually lacks jq, hence the dual path.

### What this ADR does NOT solve

This ADR does NOT solve the paths-ignore hazard. **Both** the legacy endpoint and the Rulesets API will leave a required check in `Pending` if the workflow is skipped (paths-filter, branches-filter, commit-message skip, etc.), which under `strict_required_status_checks_policy: true` is indistinguishable from "the check failed." The mitigation is to require only checks that fire on every PR — see [ADR 0039](./0039-required-check-minimalism-public-oss.md) for the rationale and the trimmed-list policy.

---

## Consequences

### Positive

- Branch protection is now declared in version-controlled JSON with inline rationale; reproducible by anyone with `gh` and the repo.
- Rollback is one command (`apply-main-ruleset.sh --delete`) and is idempotent — safe to re-run.
- Canary observation (`--evaluate`) gives a way to add new required checks without risking a permanent block, on plans that support it.
- The bypass-actor list is explicit and auditable; "who can bypass main protection" is a grep against a JSON file.
- Future protection improvements (GitHub's roadmap is on Rulesets) flow in by editing the JSON.

### Negative

- The `_comment_*` stripping convention is a Sunfish-internal pattern; new contributors need to read the script header to understand it. Mitigated by the README in `infra/github/`.
- `evaluate` mode requires GitHub Enterprise on github.com. The script accepts `--evaluate` and the API rejects it on Free/Pro; the script surfaces this clearly but it's still a sharp edge.
- The legacy `apply-branch-protection.sh` + `branch-protection-main.json` files remain in the repo through the verification window. Two-file world for ~two weeks; cleanup PR scheduled.
- Anyone managing GitHub via the Settings UI (click-ops) can drift the live state away from the JSON. Mitigation is social, not technical: the README + this ADR + the `apply-main-ruleset.sh` script all point at the same source of truth.

---

## Revisit triggers

This ADR should be re-opened if:

1. **GitHub deprecates legacy branch protection** — at that point the legacy fallback files (`apply-branch-protection.sh`, `branch-protection-main.json`) should be deleted. Schedule cleanup PR.
2. **Ruleset API gains paths-aware required-check semantics** — specifically, a way to mark a required check as "satisfied if the workflow's path conditions caused it to skip." Today this does not exist (see ADR 0039); if it ships, we can re-add the path-conditional checks (Build & Test, A11y Storybook, Global-UX Gate) to the required-checks list and tighten the protection.
3. **Second contributor joins** — multi-maintainer repos benefit from the Ruleset bypass-actor model in ways the solo case doesn't. Re-examine whether `required_approving_review_count` should rise from 0 to 1 at that point. (Today's `0` is a single-maintainer accommodation, not a security posture.)
4. **Org-level Rulesets become available** — GitHub Org-level Rulesets can apply across many repos. If Sunfish becomes an Org with multiple repos, the per-repo Ruleset moves up to the Org and this ADR migrates accordingly.

---

## References

- **PRs that this ADR ratifies:**
  - PR #126 — `feat(infra): rewrite main branch-protection as GitHub Ruleset (handles paths-ignore properly)` — the initial migration. The PR title's "handles paths-ignore properly" was incorrect; ADR 0039 documents the actual paths-ignore hazard and the trimmed-list response.
  - PR #138 — `chore(infra): trim ruleset to always-on checks + add --delete flag for rollback` — the operational refinements (trimmed required-checks list per ADR 0039; added `--delete` flag) made after Phase 1 verification.
- **Files this ADR governs:**
  - [`infra/github/main-ruleset.json`](../../infra/github/main-ruleset.json) — the canonical Ruleset payload (with `_comment_*` rationale).
  - [`infra/github/apply-main-ruleset.sh`](../../infra/github/apply-main-ruleset.sh) — idempotent apply / dry-run / evaluate / delete script.
  - [`infra/github/README.md`](../../infra/github/README.md) — when to use Rulesets vs legacy; canary-branch test pattern; paths-ignore hazard discussion.
- **Companion ADR:**
  - [ADR 0039](./0039-required-check-minimalism-public-oss.md) — the required-check minimalism policy that the trimmed list in `main-ruleset.json` implements. ADR 0038 is *how* (Rulesets); ADR 0039 is *what to require* (only always-on checks).
- **Related ADR:**
  - [ADR 0037](./0037-ci-platform-decision.md) — staying on GitHub Actions; the Ruleset is one of the GHA-specific assets ADR 0037 names as load-bearing for the platform decision.
- **External:**
  - GitHub Rulesets API: `https://docs.github.com/en/rest/repos/rules` and `https://docs.github.com/en/rest/repos/rulesets`.
  - Legacy branch protection (for back-compat reference): `https://docs.github.com/en/rest/branches/branch-protection`.
