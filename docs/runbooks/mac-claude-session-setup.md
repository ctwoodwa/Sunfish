# Mac Claude Session Setup

**Audience:** A Claude Code session opening Sunfish on a fresh (or under-provisioned) Mac.
**Goal:** Bring the machine to a state where Claude can do real Sunfish work — build .NET, run pnpm/Storybook, drive `gh`, run `act` for local CI iteration, commit through Husky — without surprise failures mid-task.
**Companion ADR:** [ADR 0037 — CI Platform Decision](../adrs/0037-ci-platform-decision.md). This runbook is the operational counterpart: ADR 0037 says *what* (stay on GitHub Actions, adopt `act`); this runbook says *how to set up a Mac to act on that*.

> **Location note:** the brief that opened this runbook asked for `.claude/runbooks/`. The agent harness sandbox blocks writes under `.claude/`; this runbook lives under `docs/runbooks/` instead, which is the project's existing runbook home (see also `docs/runbooks/live-api-verification.md`). A symlink or pointer file under `.claude/runbooks/` can be added by hand if a session-side discoverability shortcut is desired.

This runbook is tactical. Read it top-to-bottom on a fresh clone; cherry-pick on a returning machine.

---

## Prerequisites

- **macOS Sequoia 15+.** Older versions may work but are not the target.
- **Apple Silicon (M1/M2/M3/M4).** Intel Macs work but Docker / `act` runner images are slower under x86_64 emulation; if Intel, prefer native ARM-incompatible workflows on hosted CI rather than `act` locally.
- **Homebrew installed** (https://brew.sh). All install steps below assume `brew` is on PATH.
- **GitHub account with repo access** (`ctwoodwa/Sunfish`) and `gh` configured (covered below).

---

## Required installs

### 1. Git + GitButler (`but` CLI)

The Sunfish repo is GitButler-managed. Plain `git` for branch / commit / push bypasses GitButler and goes stale on `gitbutler/workspace`. Use `but` for write operations; plain `git` for read operations (`git log`, `git blame`, `git show`) is fine.

```bash
brew install git
brew install --cask gitbutler        # provides the `but` CLI plus the desktop app
but --version                        # sanity check
```

If `but` is not on PATH after install, add `/Applications/GitButler.app/Contents/Resources/bin` to PATH (the desktop app installs the CLI there).

### 2. GitHub CLI (`gh`)

Required for every PR operation in Sunfish (the project uses PR-with-auto-merge as the default push path; direct push to main is forbidden).

```bash
brew install gh
gh auth login                        # follow the SSH or HTTPS prompt
gh auth status                       # should show: Logged in to github.com
```

### 3. Docker Desktop

Required for `act` (which runs GHA workflows in containers) and for any future self-hosted runner work flagged in ADR 0037's revisit triggers.

```bash
brew install --cask docker
open -a Docker                       # launch the GUI; wait for the whale icon to settle
docker info | head -5                # should show Server Version, no errors
```

Docker Desktop on Apple Silicon defaults to running x86_64 containers under Rosetta 2; this is fine for `act` but slower than native. The medium runner image (chosen below) is built for both arches.

### 4. `act` — local GitHub Actions runner

The decision in ADR 0037 to stay on GHA was made *with* `act` in scope. Without `act`, the iteration loop on workflow YAML edits is push-and-pray.

```bash
brew install act
act --version
```

Drop the following into `~/.actrc` (creates the file if missing) — these are the recommended defaults for Sunfish's workflows:

```text
# Sunfish-recommended act defaults.
# medium image is the sweet spot: large enough to run actions/setup-node,
# actions/setup-dotnet, pnpm install, and most of our jobs unmodified;
# smaller and less disk-hungry than the catthehacker/ubuntu:full image.
-P ubuntu-latest=catthehacker/ubuntu:act-latest
--container-architecture linux/amd64
--artifact-server-path /tmp/act-artifacts
```

The `--container-architecture linux/amd64` line forces x86_64 containers on Apple Silicon, which avoids the long tail of "this action's binary doesn't exist for arm64" failures. It is slower per job but more compatible.

### 5. .NET SDK — pinned to `global.json`

Sunfish's `global.json` pins the .NET SDK to a specific .NET 11 preview build (currently `11.0.100-preview.3.26207.106` with `rollForward: latestFeature`). Install at least that version, then verify.

```bash
brew install --cask dotnet-sdk
dotnet --info | head -10             # check that the SDK list contains a 11.0.100-preview.3+ build
cat global.json                      # cross-reference the pinned version
```

If the brew cask lags the preview, install directly from https://dotnet.microsoft.com/download/dotnet/11.0 (preview channel). The `latestFeature` roll-forward lets a newer preview-3 build satisfy the pin.

### 6. Node.js — 20+ LTS

Used by tooling/ scripts (CSS audits, locale-completeness check) and by the pnpm-driven Storybook + a11y test runner under `packages/ui-core`. There is no `.nvmrc` in the repo today; pin to LTS or current.

```bash
brew install node@20
brew link node@20 --force --overwrite
node --version                       # v20.x or higher
```

### 7. pnpm — version per the workflow file (currently 10.33.2)

The `global-ux-gate.yml` workflow installs `pnpm@10.33.2` explicitly. Match that locally so `pnpm install` produces the same lockfile resolution as CI.

```bash
npm install --global pnpm@10.33.2
pnpm --version                       # 10.33.2
```

### 8. Playwright browsers (only if working on Storybook / a11y tests locally)

`packages/ui-core` Storybook + a11y test runner needs Chromium. The CI workflow caches the install at `~/.cache/ms-playwright`; for local work do this once after `pnpm install`:

```bash
cd packages/ui-core
pnpm install                          # gets the playwright npm package
pnpm exec playwright install --with-deps chromium
```

`--with-deps` installs the OS-level libraries Chromium needs; on macOS this is mostly a no-op but the flag is harmless.

### 9. jq — used by branch-protection apply scripts

`infra/github/apply-branch-protection.sh` and `apply-main-ruleset.sh` both shell out to `jq` for response parsing.

```bash
brew install jq
jq --version
```

### 10. Claude Code CLI

The session you're running in uses Claude Code; if Claude Code itself needs to be installed/updated, follow the official docs at https://docs.claude.com/claude-code (Anthropic's documentation site). `brew install claude-code` may also be available depending on the Anthropic distribution channel at the time of install.

---

## Optional but recommended

### Husky bootstrap (run once after first clone)

Sunfish uses dotnet-husky for git hooks. The pre-commit hook validates SUNFISH_I18N_001 (RESX `<comment>` content) among other things. The hook will fail on commit if the bootstrap hasn't run.

```bash
dotnet tool restore                  # restores the husky-net dotnet tool
dotnet husky install                 # wires .husky/pre-commit into .git/hooks
```

If a hook misfires later with a "command not found: husky" error, rerun the two commands above. PR #115 (`chore(husky): drop Node-husky bootstrap so fresh worktrees commit cleanly`) is the relevant history — the project deliberately uses dotnet-husky over Node-husky to avoid bootstrap order issues with fresh worktrees.

### GitButler skill for Claude (one-time per machine)

Per the global `use-gitbutler.md` rule, install GitButler's own Claude Code skill which provides programmatic guidance for the `but` CLI:

```bash
but skill install --global
```

This is upstream-maintained by the GitButler team and updates independently of Sunfish.

### direnv or asdf (only if managing multiple project versions)

If the maintainer's Mac runs other projects on different .NET / Node versions, `direnv` (with a per-project `.envrc`) or `asdf` (with `.tool-versions`) avoids version-thrash. Sunfish itself does not require these — `global.json` pins .NET, and Node 20+ is broad-enough.

---

## First-session bootstrap checklist

Steps a Claude session should run on a fresh clone, in order:

```bash
# 1. Clone (skip if the maintainer already cloned)
git clone https://github.com/ctwoodwa/Sunfish.git
cd Sunfish

# 2. Verify Docker is running (act + future runner work depends on this)
docker info | head -3

# 3. Verify gh is authenticated (every PR op needs this)
gh auth status

# 4. Verify .NET matches global.json
dotnet --info | grep -A1 "Version:" | head -2
cat global.json

# 5. Bootstrap Husky pre-commit hook
dotnet tool restore && dotnet husky install

# 6. Verify act enumerates the workflows (no execution yet)
act --list

# 7. Optional: dry-run a workflow without executing actions
act -n -W .github/workflows/ci.yml
```

If any step fails, do not start writing code — fix the environment first. The most common failure modes are: Docker Desktop not started (step 2), `gh` not logged in (step 3), .NET SDK missing the preview build (step 4).

---

## Sunfish-specific gotchas

Lessons from prior sessions; quote / link these so a new session doesn't re-discover them painfully.

### Husky needs `_/husky.sh` shim on fresh clones

PR #115 is the fix. The shim file is created by `dotnet husky install`; if the `.husky/_/husky.sh` file is missing after clone, rerun the install. Symptom: pre-commit fails with `.husky/pre-commit: line N: _/husky.sh: No such file or directory`.

### Commitlint type-enum is strict

Only these commit types are accepted: `feat | fix | docs | style | refactor | perf | test | build | ci | chore | revert`. The full config is in `commitlint.config.mjs`. The project memory note `project_commitlint_type_enum.md` documents this — it has bitten enough sessions that it's pinned in long-term memory. Custom types (like `release` or `wip`) will fail commitlint and block the PR.

Subject case is relaxed to a warning (severity 1) per a deliberate decision documented in the config — sentence-case in subjects referring to ADRs / Plans / Waves is allowed.

Header max length is 100 chars. Body line max length is 100 chars. Subject must be imperative mood per `_shared/engineering/commit-conventions.md`.

Valid scopes are also enum-restricted (severity 1, warning only): `foundation`, `foundation-catalog`, `foundation-multitenancy`, `foundation-featuremanagement`, `foundation-localfirst`, `foundation-integrations`, `ui-core`, `ui-adapters-blazor`, `ui-adapters-react`, `blocks-leases`, `compat-telerik`, `bridge`, `kitchen-sink`, `apps-docs`, `scaffolding-cli`, `icm`, `adrs`, `governance`, `docs`, `deps`, `repo`. Use these where applicable.

### GitButler is enabled — use `but` not plain git

Per the global GitButler rule: detect with `test -d .git/gitbutler`. If on `main` or a feature branch in the GitButler-managed repo, switch to `gitbutler/workspace` (or run `but setup`) before write operations. The exception: `worktree-agent-*` branches under `.claude/worktrees/` are intentional plain-git worktrees and should use plain `git` for branch / commit / push.

### Anchor MAUI requires `RuntimeIdentifiers=win-x64` on restore for Windows builds

PR #110 fix. Symptom: `dotnet restore` fails on the Anchor project with NETSDK1112 errors when building Windows targets without the runtime ID. Not directly relevant on Mac (the Anchor MAUI project's iOS / MacCatalyst targets are commented out per ADR 0037's discussion), but Mac users may see the failure if they accidentally trigger Windows-targeted builds via a solution-wide build. Stick to project-targeted builds (`dotnet build packages/foundation/Sunfish.Foundation.csproj`) when working on a Mac.

### Path triggers — docs-only changes skip heavy CI gates

PR #116. Workflows under `.github/workflows/` set `paths-ignore` for docs-only paths (`*.md`, `.wolf/**`, `docs/**`, `waves/**`, `icm/**`, `_shared/**`). When running `act` locally, predict which workflows will fire by checking what file paths your change touches. A docs-only PR will skip `ci.yml` and `global-ux-gate.yml`; useful to know so you don't waste minutes running `act` on workflows that GHA wouldn't run for the equivalent push.

### PR-with-auto-merge is the default push path

Per the project memory rule `feedback_pr_push_authorization.md`: every change ships as feature-branch + PR + `gh pr merge --auto --squash`. CI is the only gate (single-maintainer setup). Direct push to main is forbidden. The auto-merge audit (PR #133) confirms there is no automation gate enforcing this — it's a manual CLI discipline. A new session should follow it.

### Public-repo hardening is in effect

Per PRs #129 / #132: `pull_request_target` is banned; all workflows have minimal `permissions:` blocks. When adding a new workflow, copy the `permissions:` shape from an existing one (`ci.yml` has the canonical example). Don't introduce broad `permissions: write-all` anywhere.

---

## When to read which doc

| Situation | Read |
|---|---|
| First time on a Mac, fresh clone | This runbook, top to bottom |
| Asked "should I migrate off GitHub Actions?" | [ADR 0037](../adrs/0037-ci-platform-decision.md) — answer is no, here's why |
| Asked "what's GitButler?" | The global rule: `~/.claude/rules/use-gitbutler.md` |
| Pre-commit fails on a fresh clone | Husky bootstrap section above + PR #115 history |
| Workflow YAML edit you want to test | `act -n -W .github/workflows/<name>.yml` (dry run) then `act -W .github/workflows/<name>.yml` (real run) |
| Commit rejected by commitlint | Commitlint section above + `commitlint.config.mjs` |
| Need to understand the kernel architecture | `_shared/product/local-node-architecture-paper.md` (foundational) |
| Need to understand Anchor vs. Bridge | [ADR 0031](../adrs/0031-bridge-hybrid-multi-tenant-saas.md) and [ADR 0032](../adrs/0032-multi-team-anchor-workspace-switching.md) |

---

## References

- [ADR 0037 — CI Platform Decision](../adrs/0037-ci-platform-decision.md) — the *why* behind the GHA + `act` choice this runbook installs.
- [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) — the foundational paper that justifies every other architectural choice in the repo.
- [`commitlint.config.mjs`](../../commitlint.config.mjs) — exact commit type / scope / length enforcement.
- [`global.json`](../../global.json) — pinned .NET SDK version.
- [`.github/workflows/`](../../.github/workflows/) — CI workflow set; `act` runs these locally.
- [`infra/github/`](../../infra/github/) — branch-protection / ruleset apply scripts (require `jq`).
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — contribution workflow including the PR-with-auto-merge expectation.
- Sibling runbook: [`docs/runbooks/live-api-verification.md`](./live-api-verification.md).
- Global rule files: `~/.claude/rules/use-gitbutler.md`, `~/.claude/rules/context7.md`.
- Project memory notes referenced above: `project_commitlint_type_enum.md`, `feedback_pr_push_authorization.md`, `feedback_use_gitbutler.md`, `project_sunfish_private_until_llc.md`.
