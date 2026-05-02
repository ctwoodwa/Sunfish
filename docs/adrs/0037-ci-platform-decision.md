---
id: 37
title: CI Platform Decision (Stay on GitHub Actions, Adopt `act` for Local)
status: Accepted
date: 2026-04-26
tier: governance
concern:
  - governance
  - dev-experience
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0037 — CI Platform Decision (Stay on GitHub Actions, Adopt `act` for Local)

**Status:** Accepted (2026-04-26)
**Date:** 2026-04-26
**Resolves:** A maintainer question — *"should I stand up GitLab locally for CI/CD, or pick a different local-first OSS forge so the project isn't tied to GitHub?"* — surfaced during a session that had just landed four public-repo hardening PRs (#129, #130, #132, #133) on the existing GitHub Actions setup. This ADR closes the question by ratifying the status quo, names the alternatives that were considered and rejected, adopts `act` as a complementary local tool, and writes down the explicit trigger conditions under which the decision should be revisited.

> **Numbering note:** the brief that opened this ADR referenced "ADR 0033," but `0033` was already taken (Browser Shell v1 Render Model + Trust Posture, 2026-04-23) and ADRs through `0036` exist. This ADR takes the next free slot.

---

## Context

Sunfish is a public, solo-maintainer, pre-v1 OSS reference implementation hosted on GitHub. CI/CD currently runs on GitHub-hosted Actions runners. The maintainer asked whether it would be worthwhile to either (a) self-host GitLab CE on their workstation as a primary forge, or (b) migrate to one of the local-first / decentralized git collaboration alternatives that have appeared since GitHub's acquisition by Microsoft.

The question is not abstract. It surfaces in three concrete ways:

1. **Cost / minutes pressure.** Public GitHub repos get unlimited GitHub Actions minutes today, but if Sunfish ever flips back to private (per the *"private until LLC"* governance posture), minutes become metered and a self-hosted runner is the obvious mitigation.
2. **Mac/iOS coverage.** Anchor is a MAUI desktop app. Some target frameworks (iOS, MacCatalyst) are commented out of the build because GitHub-hosted macOS minutes are expensive and the maintainer doesn't yet have a CI need that justifies them. A future Mac mini under a desk would change that calculus.
3. **License-gated test deps.** compat-telerik tests run against trial Telerik builds today. Real-license CI for compat-telerik (and the queued compat-syncfusion / compat-devexpress / compat-infragistics workstreams) needs a runner where the licensed installer can live persistently — which fits a self-hosted runner more naturally than a fresh GitHub-hosted VM per job.

The session that opened this question had just spent considerable effort hardening the existing GitHub Actions setup — the four public-repo hardening PRs (#129 ban `pull_request_target`, #130 audit fork-PR approval gate, #132 add minimal `permissions:` blocks to all workflows, #133 audit auto-merge scope) plus the prior round of CI fixes (#108 cache layers + concurrency cancel, #110 MSB1006 semicolon escape, #116 paths-ignore for docs-only PRs, #119 p95 measurement after #110). Migrating off GitHub Actions would throw all of that away.

The question this ADR answers: **what is the primary CI platform for Sunfish today, what local tooling complements it, and what specific events would re-open the question?**

---

## Decision drivers

- **Public-repo + free-GHA-minutes posture** — Sunfish is public; GitHub Actions minutes are uncapped. The marginal compute cost of CI is zero. Cost-driven migrations away from GHA do not apply at this stage.
- **Solo-maintainer ops budget** — every hour spent administering a self-hosted forge or runner is an hour not spent on the product. The threshold for adopting any new ops surface is high.
- **Recent hardening sunk cost** — ~20 PRs of GHA-specific hardening, branch-protection rules, ruleset migration (PR #126), and minimal-permissions wiring would need to be re-derived in any forge migration. This work is defense-in-depth that survives a future migration only if the new forge has equivalent primitives.
- **Public-repo discoverability** — per the project's "reference implementation alongside *The Inverted Stack* book" positioning, GitHub network effects (search, stars, fork-and-PR contribution flow, GitHub-native CodeQL, GitHub Pages for the docs site) are part of the value proposition. P2P or self-hosted forge alternatives sacrifice this.
- **GitHub-Actions-compatible YAML as a portability hedge** — both `act` (local) and Forgejo Actions (alternative forge) consume the same `.github/workflows/*.yml`. Continued investment in GHA workflow files is not a lock-in.
- **Pre-release + breaking-changes-approved posture** — this isn't a constraint here; CI platform choice is operationally reversible at any time.

---

## Considered options

### Option A — Stay on GitHub Actions; adopt `act` for local workflow testing

Continue using GitHub-hosted runners as the primary CI. Add `act` (the nektos.io tool that runs GHA workflows locally in Docker) so workflow YAML changes can be tested without the push → wait 12 min → fail → fix → push loop.

- **Pro:** Zero migration cost; preserves the recent hardening PRs and branch protection rules.
- **Pro:** Free for public repos; no infra to maintain.
- **Pro:** GitHub-native CodeQL, Dependabot, GitHub Pages, and PR review UX continue to work as-is.
- **Pro:** `act` cuts the workflow-iteration loop from ~12 min to ~1-2 min for the common case of "did I get the YAML right?"
- **Con:** Still bound to a single vendor for the primary forge. If GitHub's governance shifts (e.g., a Microsoft policy that conflicts with the project's posture), there is migration cost to pay.
- **Con:** macOS minutes remain expensive; iOS/MacCatalyst CI stays uncovered until a self-hosted Mac runner appears.
- **Con:** License-gated runner needs (real Telerik etc.) still need a GHA self-hosted runner to be added someday.

### Option B — Self-host GitLab CE on the maintainer's workstation as primary CI

Stand up GitLab CE in Docker on the maintainer's machine. Mirror the GitHub repo to GitLab. Run CI/CD pipelines on GitLab Runner. Use GitHub purely as a public mirror for discoverability.

- **Pro:** Local control of the runner; no external dependency for builds.
- **Pro:** GitLab CI/CD is a mature pipeline system with good caching, environments, and DAG support.
- **Con:** GitLab CE's docker-compose stack is heavy — Postgres + Redis + Sidekiq + Workhorse + Gitaly + container registry + nginx. Memory footprint is real on a workstation that is also running Docker Desktop, .NET, IDE, and Anchor. Mac users especially feel it.
- **Con:** GitLab on Docker Desktop on Mac is fragile. GitLab's official docker-compose is Linux-host-tuned; HFS+/APFS quirks, Docker-Desktop-VM CPU pinning, and the sheer container count make stability a maintenance burden.
- **Con:** Throws away the ~20 GHA hardening PRs. GitLab CI YAML is not workflow-compatible with GitHub Actions; everything is a rewrite.
- **Con:** Public-repo + GitHub-contributor PRs would need a webhook bridge or one-way mirror to round-trip into GitLab CI. This bridge is itself a maintained component that can break silently.
- **Con:** Branch protection / required status checks set up on GitHub (PR #126's ruleset migration) become inert; they have to be re-implemented on GitLab as protected branches with required pipelines.
- **Net negative** for a solo-maintainer public OSS project on a public repo where GHA is free.
- **Rejected** for this stage. Reconsider only if ops bandwidth grows (second contributor) AND a specific GitHub-side blocker appears.

### Option C — Migrate to Forgejo + Forgejo Actions

Forgejo (Codeberg's hard fork of Gitea) ships its own CI runner that consumes GitHub-Actions-compatible YAML. Run on a small VPS or local server. Mirror to GitHub as a discoverability play.

- **Pro:** Lightest-weight forge alternative; single Go binary.
- **Pro:** Workflow YAML is portable from GHA — most workflows would run unchanged (with `runs-on: ubuntu-latest` mapped to a Forgejo runner image).
- **Pro:** Truly community-governed (Codeberg e.V. nonprofit); no vendor capture risk.
- **Pro:** Strong privacy / no-tracking posture aligns with Sunfish's local-first values.
- **Con:** Loses GitHub network effects — the Sunfish OSS audience is on GitHub. Forge-mirroring is a half-measure that creates two truths.
- **Con:** Forgejo Actions is younger than GHA; some GHA actions in the marketplace don't run cleanly. The recent hardening (especially `permissions:` blocks and `actions/cache@v4`) would need spot-validation on Forgejo Actions runners.
- **Con:** CodeQL doesn't run on Forgejo. SAST coverage would need a different tool (e.g., Semgrep self-hosted).
- **Defer.** Reconsider only if GitHub itself becomes the wrong primary (governance shift, censorship, fee structure change). Forgejo is the most likely successor primary if that day comes.

### Option D — Woodpecker CI (Drone fork) integrated with GitHub

Container-native CI server. Connects to GitHub via OAuth, runs pipelines on configurable workers, scales horizontally cheaply.

- **Pro:** Nice container-first model; pipeline-as-code in YAML.
- **Pro:** Self-hostable on tiny VPS; cheap to scale.
- **Con:** Solves a problem Sunfish doesn't have. Compute cost on a public GHA repo is zero. Adding Woodpecker means maintaining a CI server purely for "I prefer this YAML dialect," which doesn't carry weight.
- **Con:** Doesn't replace any of the GHA hardening; runs alongside, not instead of, GHA primitives like CodeQL and branch-protection.
- **Defer.** Reconsider if/when GHA minutes become metered AND a small VPS would be substantially cheaper.

### Option E — Dagger (write CI in real code)

Express CI/CD pipelines in Go/Python/TypeScript/Java SDKs against the Dagger engine. Pipelines run anywhere — locally for dev iteration, in GHA / GitLab / Jenkins for production.

- **Pro:** Pipelines are real programs — testable, refactorable, type-checked. A direct answer to YAML's worst sins.
- **Pro:** Same pipeline runs locally and in CI by construction; no "works on my machine" delta.
- **Pro:** Forge-agnostic — Dagger atop GHA today, atop Forgejo or GitLab tomorrow with pipeline code unchanged.
- **Con:** Migration cost — every existing workflow becomes a Dagger module.
- **Con:** Adds an SDK dependency to CI setup; contributors need Dagger installed locally.
- **Con:** The pain Dagger fixes (YAML brittleness, local-vs-CI divergence) is not yet acute on Sunfish. `act` solves the local-divergence half of the problem cheaply.
- **Watch.** Reconsider when (a) workflow YAML edits start failing repeatedly in subtle ways that `act` doesn't catch, OR (b) a Sunfish-internal preference emerges to write CI in C#/TypeScript instead of YAML.

### Option F — Radicle (P2P decentralized git)

Truly distributed, peer-to-peer git collaboration. No central forge.

- **Pro:** Maximum vendor-independence; aligns ideologically with the paper's local-first thesis.
- **Con:** No discoverability surface — Sunfish's positioning explicitly leans on GitHub network effects (search, stars, public PR contribution).
- **Con:** No CI primitive — pipelines have to be bolted on with external tooling; the maintainer is back to running CI somewhere.
- **Con:** Contributors need Radicle installed; raises the contribution friction floor.
- **Rejected.** Right tool for a different project; wrong tool for an OSS reference implementation that wants reach.

### Option G — Sourcehut, Pijul, Fossil, Gogs, OneDev

Other forge / VCS alternatives considered briefly.

- **Sourcehut** — email-driven workflow; mature CI; respected. Wrong contribution UX for the OSS audience Sunfish is targeting.
- **Pijul** — patch-based VCS; not yet stable enough for a pre-v1 production codebase.
- **Fossil** — bundled VCS+forge+wiki+tickets; wonderful for solo work; loses GitHub reach.
- **Gogs** — predecessor to Gitea; less actively maintained than Forgejo.
- **OneDev** — Java-based all-in-one; nice CI; small community.
- **All rejected** for being more disruptive than the problem warrants.

---

## Decision

**Adopt Option A: stay on GitHub Actions as the primary CI platform; add `act` for local workflow iteration; defer all forge / runner migrations until a specific trigger condition fires.**

The decision rests on three premises:

1. **The cost equation favors GHA today.** Public-repo GHA minutes are free; the recent hardening work has stabilized the workflow set; CodeQL, branch rulesets, and the auto-merge flow are already wired and audited. There is no operational pain that a migration would relieve.
2. **`act` solves the iteration-speed pain that *was* real.** The "push, wait 12 min, watch the workflow fail, fix one line, push, wait again" loop — felt acutely during PRs #108/#110/#116/#129/#132 — is exactly what `act` exists for. It runs the same YAML in a local Docker container in 1-3 minutes per pass.
3. **Optionality is preserved by writing GHA-compatible YAML.** As long as workflows stay in `.github/workflows/*.yml` and avoid GitHub-specific actions where reasonable substitutes exist, a future migration to Forgejo Actions (the most likely successor) is a runtime swap, not a rewrite.

This decision is operationally reversible. It is not a long-term commitment to GitHub the company; it is the right answer for the next 6-18 months given current load, contributor count, and posture.

### Adopted alongside this ADR

- **`act`** (https://github.com/nektos/act) — installed via Homebrew on the maintainer's Mac. The recommended `~/.actrc` for Sunfish workflows uses the medium runner image (sweet spot between compatibility and disk footprint). See [`docs/runbooks/mac-claude-session-setup.md`](../runbooks/mac-claude-session-setup.md) for setup steps.
- **The four public-repo hardening PRs already merged** — #129 (ban `pull_request_target`), #130 (fork-PR approval audit), #132 (minimal `permissions:` blocks), #133 (auto-merge scope audit). These are defense-in-depth wins that survive any future migration only if the next forge has equivalent primitives.
- **The CI hygiene PRs from the same wave** — #108 (cache layers + concurrency cancel), #110 (MSB1006 fix), #116 (paths-ignore for docs-only PRs), #119 (p95 measurement), #126 (ruleset migration). These are the load-bearing CI improvements that would be redone in any forge migration.

---

## Revisit triggers

This ADR should be re-opened — and the alternatives reconsidered — when **any one** of the following occurs:

1. **iOS / MacCatalyst CI need materializes.** When Anchor uncomments the iOS / MacCatalyst target frameworks (or when a contributor requests a build matrix that includes them), the cost of GitHub-hosted macOS minutes vs. a $600 Mac mini under a desk should be re-examined. The Mac mini wins quickly if more than ~50 macOS-job-minutes are consumed per month.
2. **License-gated test dependencies enter scope.** When compat-telerik's real-license CI (or the queued compat-syncfusion / compat-devexpress / compat-infragistics workstreams) needs persistent licensed-installer state on a runner, a self-hosted GHA runner becomes the path of least resistance. This does NOT mean migrating off GHA — just adding a self-hosted runner to the existing pool.
3. **GitHub Actions minutes overage.** If Sunfish flips back to private (per the LLC governance plan) and burns through the metered GHA minutes for the chosen plan, a self-hosted runner becomes cost-justified. Re-examine cost per CI minute vs. self-hosted opex at that point.
4. **Second contributor joins.** Two-person teams change the auto-merge / approval calculus. The current single-maintainer setup uses `gh pr merge --auto --squash` with CI as the only gate; with a second human, branch-protection's "required reviews" can do real work. This is still a GHA-vs-GHA configuration change, but it's also a natural moment to revisit whether the broader CI platform choice still fits.
5. **Always-on Mac mini / Linux server available.** A new always-on workstation in the maintainer's environment changes the maintenance-cost calculus for self-hosted forge or runner. Re-examine Forgejo + Forgejo Actions vs. self-hosted GHA runner in that scenario; both become cheaper to operate when the host is already running.

If a trigger fires, the response is: re-read this ADR, write a follow-up ADR that supersedes the relevant section, and document what changed. Do not silently migrate.

---

## Consequences

### Positive

- Zero immediate migration cost; recent hardening PRs continue to compound value.
- Local workflow iteration speeds up dramatically once `act` is installed (1-3 min vs. 8-12 min per try).
- Decision is documented, so future contributors and future-self can see *why* GitHub Actions was retained — not by inertia but by deliberate analysis.
- Trigger conditions are explicit, so the decision will be re-opened at the right moments rather than drifting.
- Continues to use GitHub network effects for reach; aligns with the *reference implementation alongside the book* positioning.

### Negative

- Continued single-vendor dependency on GitHub for forge + CI + (effectively) issue tracking. If GitHub's governance shifts in a way that conflicts with the project's posture, migration cost is real.
- Anchor's iOS / MacCatalyst targets remain uncovered until a self-hosted Mac runner appears (or the maintainer pays for hosted macOS minutes).
- License-gated compat-vendor CI is blocked on the same self-hosted runner that doesn't yet exist. The compat-package expansion workstream's CI story is "deferred."
- `act` covers ~80% of GHA workflow semantics; the remaining 20% (services containers, certain matrix shapes, `pull_request_target` semantics) still requires pushing to verify. This is a known and accepted limitation.

---

## References

- **Recent hardening PRs (the work this ADR ratifies):**
  - PR #108 — `ci(global-ux): cache layers + concurrency cancel + a11y-audit off PR critical path`
  - PR #110 — `fix(ci): MSB1006 semicolon escape + NETSDK1112 cache fix`
  - PR #116 — `ci: skip heavy gates on docs-only PRs via paths-ignore`
  - PR #119 — `docs(plan-5): Task 9 — CI pipeline p95 measurement (post-PR-110 baseline)`
  - PR #126 — `feat(infra): rewrite main branch-protection as GitHub Ruleset`
  - PR #129 — `security(ci): ban pull_request_target trigger (public-repo workflow-injection defense)`
  - PR #130 — `docs(security): fork-PR approval audit — current GitHub Settings state`
  - PR #132 — `ci(security): minimal permissions blocks on all workflows (public-repo hardening)`
  - PR #133 — `docs(security): auto-merge scope audit — no automation gate, manual CLI only`
  - PR #134 — `fix(a11y): 11 cascade-extension findings — progressbar names + dialog names + target sizes + nav structure`
- **Foundational context:**
  - [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) — the paper's local-first thesis (relevant to Option F's ideological alignment, but Sunfish is the *implementation*, not a P2P artifact itself).
- **Setup:**
  - [`docs/runbooks/mac-claude-session-setup.md`](../runbooks/mac-claude-session-setup.md) — how to set up a Mac to act on this decision (includes `act` install + config).
- **External:**
  - https://github.com/nektos/act — `act` upstream
  - https://forgejo.org — Forgejo (the deferred-but-most-likely successor)
  - https://dagger.io — Dagger (the watch-list option)
