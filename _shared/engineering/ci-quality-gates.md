# CI Quality Gates

**Status:** Accepted
**Last reviewed:** 2026-04-19
**Governs:** Branch protection on `main`, the set of required status checks on every pull request, and the merge policy the maintainer enforces until community governance expands per [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md).
**Companion docs:** [testing-strategy.md](testing-strategy.md), [coding-standards.md](coding-standards.md), [package-conventions.md](package-conventions.md), [commit-conventions.md](commit-conventions.md), [releases.md](releases.md), [supply-chain-security.md](supply-chain-security.md), [`.github/SECURITY.md`](../../.github/SECURITY.md), [`GOVERNANCE.md`](../../GOVERNANCE.md).
**Agent relevance:** Loaded by agents preparing a PR or debugging required-check failures. High-frequency for any contribution.

Sunfish is a pre-release OSS platform on .NET 10. "Green on main" is non-negotiable — every merge must have passed CI, and `main` must always be tag-ready. This document records what CI actually runs today, what additions are planned, and the merge policy the BDFL (per [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md)) enforces before the project opens to external contributors.

The shape of this doc is deliberate: a snapshot of concrete state (what the three workflow files do right now, word-for-word), followed by policy layered on top (required checks, protection rules, merge strategy). Readers extending CI edit the workflow files first; readers evaluating the project's quality posture read the policy sections.

## Current workflows

Three workflows live in [`.github/workflows/`](../../.github/workflows/). Anything not in that folder is not running.

### `ci.yml` — Build & Test

Triggers: `pull_request` targeting `main`; `push` to `main`. Single job on `ubuntu-latest`:

1. `actions/checkout@v6`.
2. `actions/setup-dotnet@v5` pinned to `10.x`.
3. `dotnet restore Sunfish.slnx`.
4. `dotnet build Sunfish.slnx --no-restore` — fails on any warning because `Directory.Build.props` sets `TreatWarningsAsErrors=true` (see [coding-standards.md](coding-standards.md)).
5. `dotnet test Sunfish.slnx --no-build --logger "trx" --results-directory TestResults`.
6. `actions/upload-artifact@v7` publishes `TestResults/` so failures are triageable after the run.

This is the workhorse gate. Solution-wide build + test across every project in `Sunfish.slnx`, including the bUnit, integration, and Testcontainers-backed test projects listed in [testing-strategy.md](testing-strategy.md).

### `codeql.yml` — CodeQL (csharp)

Triggers: `pull_request` and `push` to `main`, plus a weekly cron at `30 4 * * 1` (Monday 04:30 UTC). Analyzes the `csharp` language pack. The build step currently targets only `packages/foundation/Sunfish.Foundation.csproj`, so CodeQL's analysis surface is Foundation-only until the job is broadened to the full solution — captured as a follow-up below.

### `docs.yml` — DocFX build and GitHub Pages deploy

Triggers: `push` to `main` (path-filtered to `apps/docs/**`, `packages/**/*.cs`, `packages/**/*.csproj`, `.config/dotnet-tools.json`, `.github/workflows/docs.yml`) and `workflow_dispatch`. Two jobs:

1. **build** — `dotnet tool restore`, `dotnet restore Sunfish.slnx`, `dotnet build Sunfish.slnx --configuration Debug --no-restore` to emit XML doc comments, then `dotnet docfx apps/docs/docfx.json --warningsAsErrors`. The `--warningsAsErrors` flag means a missing XML doc or broken xref in a published API fails the docs build.
2. **deploy** — publishes the built `_site` artifact to `github-pages` with `id-token: write`.

Concurrency is grouped under `pages` with `cancel-in-progress: true` so overlapping pushes don't race the Pages deployment.

## Required checks for merge to `main`

A pull request may not merge until every check below reports success.

| Check | Workflow | What it proves |
|---|---|---|
| Build passes | `ci.yml` | `dotnet build Sunfish.slnx` green with `TreatWarningsAsErrors=true`. |
| All tests pass | `ci.yml` | `dotnet test Sunfish.slnx` green — xUnit unit tests, bUnit component tests, Testcontainers-backed integration tests per [testing-strategy.md](testing-strategy.md). |
| CodeQL clean | `codeql.yml` | No new high-severity C# security findings. |
| Docs build succeeds | `docs.yml` (on PR, once path trigger extended) | DocFX builds with `--warningsAsErrors`. |
| PR template checklist complete | GitHub UI | Author has ticked every applicable box in [`.github/pull_request_template.md`](../../.github/pull_request_template.md). |
| DCO signoff | GitHub DCO app | Every commit carries `Signed-off-by:` per [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md) §4 and [commit-conventions.md](commit-conventions.md). |
| 1+ maintainer approval | Branch protection | BDFL approval today; broadens when the `3+ sustained external committers` trigger fires. |

The `docs.yml` trigger is currently `push`-only. Extending it to `pull_request` (with the same path filter) is a tracked follow-up so docs regressions catch before merge rather than after.

## Speed targets

Pre-merge checks should complete in **under 10 minutes p50, under 15 minutes p95**. The 10-minute target is the point at which a contributor loses flow waiting for the light to turn green; the 15-minute p95 is the point at which the maintainer's review cadence starts stalling on CI instead of on reading the diff. When the suite regresses past those thresholds, the fix is to split — not to relax the gate.

Slower or flaky-by-nature checks stay behind explicit triggers:

- **NBomber performance scenarios** (`accelerators/bridge/tests/Sunfish.Bridge.Tests.Performance/`) — manual `workflow_dispatch` and/or the `perf` label. Never on every PR.
- **Full Testcontainers integration matrix** — runs on `ci.yml` today because the suite is small; once Postgres + RabbitMQ + Aspire fixtures push wall-time past the p95 target, promote them to a separate workflow gated on an `integration` label with a nightly cron catching drift. The split must preserve green-on-main: the nightly is a required check against `main`'s tip, and a failure opens an issue automatically.
- **Weekly CodeQL scan** — already on cron; keeps long-window drift visible without bloating PR time.
- **Concurrency cancellation** — all PR workflows should carry `concurrency: { group: ${{ github.workflow }}-${{ github.ref }}, cancel-in-progress: true }` so a force-push supersedes its own in-flight run. `docs.yml` already does this for the Pages deployment; `ci.yml` and `codeql.yml` should adopt the same pattern.

## Linting and static analysis

Two layers, deliberately separate.

**Build-time (inside `dotnet build`):**

- `Nullable=enable` — every nullability warning is an error.
- `TreatWarningsAsErrors=true` — including analyzer warnings.
- `GenerateDocumentationFile=true` with CS1591 enforced — every public member has an XML doc or the build fails.
- xUnit analyzers — catches `Assert.Equal(1, xs.Count)` (xUnit2013) at compile time.

These run inside `ci.yml`'s build step; no separate workflow needed.

**Separate checks:**

- **CodeQL** — semantic security analysis, independent workflow.
- **`dotnet format --verify-no-changes`** — **planned**, not yet wired. Add as a step in `ci.yml` so style drift (brace placement, using-directive ordering per [coding-standards.md](coding-standards.md)) fails fast without re-running the full test suite. Target: a sub-60-second job.

## Coverage

`coverlet.collector` is already referenced in every test csproj per [testing-strategy.md](testing-strategy.md) §Coverage, so coverage data exists on every CI run — it just isn't published.

**Pre-1.0 policy:**

- **Tracked, not enforced.** Publish coverage to a dashboard (Codecov is the incumbent; Coveralls is the alternative — Codecov preferred for GitHub Actions ergonomics) as an informational signal.
- Add `codecov/codecov-action@v5` (or equivalent) as a non-required check. A drop of more than 5 percentage points on a PR gets a bot comment; it does not block merge.
- No per-package threshold enforced.

**Post-1.0 policy** (tracked as a follow-up ADR):

- Per-package coverage thresholds set in each package's ADR as it reaches GA.
- Threshold enforcement becomes a required check on the matching path filter.
- Target ranges: Foundation 85%+, ui-core 80%+, adapters 70%+, blocks 60%+. These are starting points for the ADR, not committed numbers.
- Coverage ratcheting: once a package's threshold is set, PRs that drop coverage below it fail the check. Threshold increases happen in a deliberate PR, not as a drive-by side effect.

## Branch protection for `main`

Configured in GitHub under Settings → Branches → `main`:

- **Require a pull request before merging.** Direct pushes to `main` are blocked.
- **Require 1 approval.** BDFL today; the count and reviewer pool broaden on the triggers in [`GOVERNANCE.md`](../../GOVERNANCE.md) §Transition triggers.
- **Dismiss stale approvals on new commits.**
- **Require status checks to pass** — CI build-and-test, CodeQL, Docs build, DCO. Marked as "required" exactly, not just "expected".
- **Require branches to be up to date before merging** — forces rebase-on-main or merge-queue repack.
- **Require linear history.** No merge commits on `main`.
- **Require signed commits** — deferred; re-evaluate when the first external contributor signs on, since it raises the onboarding floor noticeably.
- **Block force pushes.** Always.
- **Block deletions.** Always.
- **Include administrators.** Yes — the BDFL is not exempt from CI.

Protection rules evolve with governance. When the `3+ sustained external committers` trigger from [`GOVERNANCE.md`](../../GOVERNANCE.md) fires, the approval count rises to 2 and a `CODEOWNERS`-based reviewer routing becomes required. Until then, the BDFL self-reviews their own PRs only for trivial mechanical changes (doc typos, dependency bumps) and documents the self-review in the PR description.

## Merge strategy

**Squash-merge is the default** for feature branches. One PR = one commit on `main`. The squash commit title must conform to [commit-conventions.md](commit-conventions.md) (Conventional Commits 1.0.0) — the title is what downstream tooling parses for changelogs and SemVer inference. GitHub's squash UI pre-fills from the PR title, so PR titles are held to the same format.

**Rebase-merge** is allowed for short, linear chains where each commit is individually reviewable and already Conventional — typical for refactors split into reviewable steps.

**Merge commits** on `main` are prohibited — `Require linear history` enforces this. Inside a feature branch, the contributor may merge or rebase as they please; they just can't leak the topology into `main`.

## Auto-merge and merge queue

- **Auto-merge** is enabled repo-wide. A contributor may toggle "enable auto-merge (squash)" on a PR; GitHub merges it the moment all required checks turn green. This is safe because branch protection requires review approval first.
- **Dependabot** is configured to open PRs grouped by ecosystem (NuGet, GitHub Actions). Patch-level PRs are candidates for auto-merge once CI passes; minor/major PRs go through human review.
- **GitHub merge queue** is **deferred**. It earns its weight once there are enough concurrent PRs that contributors race each other into `main`. Trigger for adoption: first external contributor with recurring PRs, or Dependabot patch PRs stacking up faster than they can be eyeballed. At that point, enable the merge queue first for trusted updates (Dependabot patch-level) and expand from there. The queue runs checks against the speculative merge result rather than the PR branch, which catches "both green in isolation, red together" regressions that squash-merge alone does not.

## Release tags

Tag pushes matching `v*` trigger a separate workflow (planned — tracked in `releases.md` draft):

1. Checkout with full history.
2. Full build + full test suite, same as PR CI.
3. `dotnet pack` each shippable package with versioning driven from the tag.
4. SBOM generation per [supply-chain-security.md](supply-chain-security.md) — CycloneDX or SPDX, attached to the GitHub Release.
5. Signed artifacts (Sigstore/cosign) per the same supply-chain doc.
6. Push to NuGet.org and create the GitHub Release with generated changelog (sourced from Conventional Commits since the previous tag).

Version derivation is tag-driven: a tag of `v0.5.3-preview.2` produces packages at `0.5.3-preview.2`. MinVer or Nerdbank.GitVersioning are both on the table; the choice lives in `releases.md`. What is settled here: the release workflow must fail if a tagged build would overwrite an already-published package version, because NuGet.org rejects re-pushes anyway and failing early keeps the release log honest.

Release CI is permitted to be slower than PR CI — its failure budget is "release is delayed," not "velocity is throttled."

## How to extend

When adding a new package under `packages/`:

- **No CI config change is needed** for `ci.yml` — it runs against `Sunfish.slnx`. Add the project to the solution and it's covered.
- **Path filters in `docs.yml`** already cover `packages/**/*.cs` and `packages/**/*.csproj`. No change.
- **CodeQL in `codeql.yml`** currently builds only `packages/foundation/Sunfish.Foundation.csproj`. To cover a new security-sensitive package, extend that build step — ideally by replacing the single-csproj build with `dotnet build Sunfish.slnx` so every package is analyzed uniformly. Tracked as a follow-up.
- **Coverage upload** picks up every test csproj that carries `coverlet.collector`, which is the default per [testing-strategy.md](testing-strategy.md). No change.
- **New integration-test project** — name it `*.Integration` and keep fast unit tests in a sibling unit project, so the label-gated split (when it lands) can partition cleanly by project name.

Adding a new workflow (e.g., a perf benchmark gate, a bundle-manifest validator) follows the same shape: narrow trigger, explicit path filter, explicit timeout, artifact upload on failure.

## Secrets, permissions, and third-party actions

OSS CI attack surface is mostly "a malicious dependency runs with repo write scope." The controls below keep that surface minimal.

- **Default `GITHUB_TOKEN` permissions** are read-only at the repo level (Settings → Actions → General → Workflow permissions). Workflows opt in to what they need — `docs.yml` declares `contents: read`, `pages: write`, `id-token: write` explicitly, and `codeql.yml` declares `security-events: write`. New workflows do the same.
- **Third-party actions are pinned to full-length commit SHAs**, not version tags. A tag can be re-pointed; a SHA cannot. Dependabot's `github-actions` ecosystem keeps the pins fresh.
- **Secrets are scoped to environments**, not the repo, for anything touching release. NuGet.org publishing lives in a `release` environment with a required reviewer so a compromised token cannot auto-publish.
- **Fork PRs** run with read-only tokens by default; any workflow that needs repo write scope on a fork PR (rare) uses `pull_request_target` with explicit review gating.

## Cross-references

- [testing-strategy.md](testing-strategy.md) — what tests CI is running.
- [coding-standards.md](coding-standards.md) — the MSBuild settings CI enforces (`TreatWarningsAsErrors`, `GenerateDocumentationFile`, `Nullable`).
- [package-conventions.md](package-conventions.md) — csproj templates CI expects to find.
- [commit-conventions.md](commit-conventions.md) — squash-commit format enforced on merge.
- [releases.md](releases.md) — tag-CI contract and release artifact set.
- [supply-chain-security.md](supply-chain-security.md) — SBOM, signing, dependency review.
- [`.github/SECURITY.md`](../../.github/SECURITY.md) — private advisory flow (CodeQL is one input; human reports are another).
- [`GOVERNANCE.md`](../../GOVERNANCE.md) — who approves, when governance broadens.
- [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md) — DCO requirement, BDFL approval, transition triggers.
