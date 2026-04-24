# Global-First UX — Phase 1 Week 6 CI Gates

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the GitHub Actions workflow that makes every Phase 1 quality contract (WCAG 2.2 AA, RTL regression, CLDR plural rules, XLIFF round-trip, analyzer diagnostics) a required status check on `main`. Plan 5 is the exit gate for Phase 1 — Phase 2 cascade (Plan 6) cannot begin until this workflow is live and all gates pass on the Phase 1 surface.

**Architecture:** Plan 5 is wiring, not new engineering. The analyzers, tests, and tooling it gates on are already produced by Plans 2 (loc-infra), 3 (translator-assist), and 4 (a11y harness cascade). Plan 5 builds the `tooling/a11y-audit-runner/` orchestrator, authors `.github/workflows/global-ux-gate.yml`, promotes `SUNFISH_I18N_001` / `SUNFISH_I18N_002` / `SUNFISH_A11Y_001` to error severity, and updates `main` branch protection so the new jobs are required.

**Tech stack:** GitHub Actions (`windows-latest` for .NET analyzers + Blazor bUnit, `ubuntu-latest` for Node tooling), Storybook 8 test-runner, `@axe-core/playwright` 4.x, Playwright 1.59+, pnpm 10 matrix sharding (4-way), `dotnet test` for CLDR + XLIFF round-trip suites, `gh api` for branch-protection configuration.

**Scope boundary:** This plan covers Phase 1 Week 6 ONLY (5 business days, after Plan 2 Week-4 polish and parallel with the tail of Plan 4's a11y-harness cascade). It does NOT cover:
- The underlying analyzers, tests, or harness code — Plans 2, 3, 4 build those; Plan 5 only consumes them.
- Phase 2 cascade to `apps/bridge/`, `apps/anchor/`, `apps/kitchen-sink/`, `ui-adapters-blazor`, and `blocks-*` — that is Plan 6, blocked on Plan 5 landing.
- The CSS-logical-props lint (spec §8) and theme-validator gates — those track a parallel Week 3–6 workstream (Sections 2, 5, 6) and are wired in a follow-up plan if they are not ready when Plan 5 enters CI.
- Nightly / scheduled jobs (CVD emulation sweep, full-locale completeness audit) — these are post-Phase-1 work.

**Parent spec:** [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../specs/2026-04-24-global-first-ux-design.md) §8 (CI Gates + Analyzer Package), §8 Phase 1 exit gate (line 893)
**Predecessor plans:**
- [Plan 1 — Week 1 Tooling Pilot](./2026-04-24-global-first-ux-phase-1-week-1-plan.md) (complete — GO verdict 2026-04-24)
- [Plan 2 — Weeks 2-4 Loc-Infra Cascade](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md) (in flight)
- Plan 3 — Translator-Assist Phase 1 core (in flight)
- Plan 4 — A11y Foundation cascade (in flight)

---

## Context & Why

Spec §8 makes the argument plainly: without an enforced CI gate, every Phase 1 deliverable (localizer wrapper, XLIFF round-trip, Storybook harness, analyzers) becomes an unenforceable convention. Plans 2–4 built the contracts; Plan 5 is the mechanism that keeps cascaded components compliant. The spec's line-893 exit condition — "Phase 2 cascade cannot begin until Section 8 CI workflow is live on `main` with all gates passing on the Phase 1 surface" — is the explicit handoff.

Week-1 runtime measurement (`waves/global-ux/week-1-runtime-measurement.md`) projects 12 minutes on 4 shards for the production axe config against the 1,440-scenario ui-core matrix. That fits the spec's p95 < 15 min budget with a narrow margin. Plan 5 honors that 4-shard constraint as-built and names 8-shard expansion as the first fallback on breach.

---

## Success Criteria

### PASSED — Phase 1 exit gate clears; Plan 6 (Phase 2 cascade) unblocked

- `.github/workflows/global-ux-gate.yml` lives on `main`, triggers on every PR that touches `packages/ui-core/`, `packages/ui-adapters-*/`, or `packages/*/Resources/`, and on every push to `main`.
- All five required gate jobs are present, green on the Phase 1 surface, and listed as required status checks on the `main` branch-protection rule.
- `SUNFISH_I18N_001` (missing `<comment>`), `SUNFISH_I18N_002` (unused resource), and `SUNFISH_A11Y_001` (component missing sibling `*.stories.ts`) emit at **error severity** in Release builds — the Plan-2 and Plan-4 warning-tier rollout is promoted by Plan 5. `-warnaserror` on the `analyzers` job enforces the promotion.
- `tooling/a11y-audit-runner/` ships with a `--shard N --total-shards 4` entrypoint that orchestrates Storybook test-runner + `@axe-core/playwright` + Sunfish contract assertions (focus order, live region, RTL icon mirror per ADR 0034's contract block) across a deterministic story allocation.
- Runtime p95 across 10 consecutive Phase-1-surface runs stays under 15 minutes per shard; hard timeout is 20 minutes (spec §8 "Runtime budget").
- `waves/global-ux/week-4-phase1-exit-gate-report.md` records the 10-run measurement, PASS/FAIL per criterion, and the explicit handoff to Plan 6.
- Branch-protection rule update committed via `gh api` in a reproducible script at `infra/github/branch-protection-main.json` so it is replayable, not just UI-clicked.

### FAILED — triggers scope cut to a Plan 5.1

- p95 runtime > 15 min on 4 shards across 10 runs, **and** 8-shard expansion does not close the gap within 2 days of measurement → escalate: drop CVD × 3 simulations from the per-commit matrix (nightly-only), per spec §8 Section 7 fallback already contemplated.
- `@axe-core/playwright` flake rate > 2% on the Phase 1 surface after 3-retry-with-backoff is applied → pin axe version + Chromium build, file an issue against upstream, proceed with known-flake quarantine list in `_shared/engineering/a11y-baseline.md`.
- `SUNFISH_A11Y_001` promotion to error blocks > 5 components that are legitimately waiting on Phase 2 cascade → keep `SUNFISH_A11Y_001` at warning severity for Phase 1 exit; add a named entry to `i18n-baseline.md` with 2026-09-01 target date for upgrade, and gate Phase 2 kickoff on the upgrade instead.
- XLIFF round-trip gate is non-deterministic on the full locale-matrix surface → this is a Plan 2 regression, not a Plan 5 failure; reopen Plan 2 Task 1.4 before proceeding.

### Kill trigger (14-day timeout)

If Plan 5 has not landed all success criteria by **2026-05-29** (14 days from Plan 2's projected Week-4 gate on 2026-05-15), escalate to BDFL for scope cut. Named options:
- (a) Ship Plan 5 with `SUNFISH_A11Y_001` at warning-only; defer error promotion to Plan 6 exit
- (b) Run `a11y-audit` as a `continue-on-error: true` informational job for two weeks; promote to required only after flake rate stabilizes
- (c) Skip locale-completeness-check in Plan 5 entirely; defer to Plan 6's application surface where the completeness-floor data actually matters

Any of (a)/(b)/(c) still satisfies the §8 line-893 exit condition because the CI workflow **is live on main** — the gate exists; only the severity/requiredness of individual jobs shifts.

---

## Assumptions & Validation

| Assumption | VALIDATE BY | IMPACT IF WRONG |
|---|---|---|
| Week-1 measurement's 2 s/scenario extrapolation holds under the production axe hook | Task 2.3 — re-measure on the Plan 4 harness output; 10-run median | If > 2.5 s/scenario, trigger 8-shard expansion immediately; p95 budget still holds |
| 4-shard story allocation is deterministic (shard N always runs the same stories) | Task 2.1 — allocator test; stable SHA-based hashing of story IDs | Non-determinism breaks local reproducibility; fallback is an explicit shard-manifest file committed to `tooling/a11y-audit-runner/shards/` |
| `windows-latest` runner supports .NET 11 preview analyzer builds without extra setup | Task 3.1 — CI dry-run on a throwaway branch before wiring to `main` | If not, pin to `windows-2022` with an explicit `actions/setup-dotnet@v4` step targeting the preview SDK; adds ~20 s per job |
| `pnpm build-storybook` artifact is < 500 MB (fits default actions cache) | Task 2.4 — measure on the Plan 4 cascade output | If larger, move to `actions/cache@v4` with sha-keyed artifacts; adds ~1 min restore step per shard |
| Branch-protection-rule update via `gh api` succeeds without disrupting in-flight PRs | Task 4.2 — apply the rule during a low-traffic window; verify existing open PRs re-run checks cleanly | If in-flight PRs go red from the new required checks, auto-rerun via `gh pr checks --watch`; document as known first-hour friction |
| All five gate jobs fit within GitHub's free 20-concurrency limit for `sunfish/sunfish` | Task 4.3 — observe queue depth on a synthetic 5-PR burst | If queue-starved, move non-critical jobs (theme-validator, locale-completeness) to a scheduled daily job; keep a11y-audit + analyzers + round-trip on every PR |

---

## File Structure (Week 6 deliverables)

```
.github/workflows/
  global-ux-gate.yml                                   ← NEW — the Phase 1 exit gate workflow

infra/github/
  branch-protection-main.json                          ← NEW — replayable branch-protection spec
  apply-branch-protection.sh                           ← NEW — `gh api` wrapper script

tooling/a11y-audit-runner/
  package.json                                         ← NEW — Node 20 CLI package
  src/
    index.ts                                           ← Entry: parse --shard / --total-shards
    shardAllocator.ts                                  ← SHA-based deterministic allocation
    storyRunner.ts                                     ← Storybook test-runner orchestration
    contractAssertions.ts                              ← Sunfish parameters.a11y.sunfish checks
    budgetEnforcer.ts                                  ← p95 timing assertion + per-shard timeout
  tests/
    shardAllocator.test.ts                             ← Determinism + balance tests
    contractAssertions.test.ts                         ← Contract-assertion unit tests
  README.md                                            ← Ops + local-reproduction guide

packages/analyzers/i18n/
  Sunfish.Analyzers.I18n.props                         ← MODIFIED — severity = error for Plan-5 scope
.editorconfig                                          ← MODIFIED — SUNFISH_I18N_001/002 / SUNFISH_A11Y_001 = error

_shared/engineering/
  a11y-baseline.md                                     ← MODIFIED — Phase 1 known-flake quarantine (if any)
  i18n-baseline.md                                     ← MODIFIED — any Plan-2 cascade deferrals surfaced by gate
  ci-quality-gates.md                                  ← MODIFIED — add Plan 5 workflow + runtime-budget math

waves/global-ux/
  week-4-phase1-exit-gate-report.md                    ← NEW — 10-run measurement + PASS/FAIL verdict
  status.md                                            ← MODIFIED — Phase 1 → Phase 2 handoff

docs/diagnostic-codes.md                               ← MODIFIED — SUNFISH_I18N_001/002 + SUNFISH_A11Y_001 severity update notes
```

---

## Week 6 — CI Gate Landing (sequential, gate-driven)

### Task 1: Inventory gate inputs and confirm Plan 2/3/4 readiness

**Files:**
- Create: `waves/global-ux/week-4-gate-inputs-inventory.md`

**Why:** Plan 5 gates on deliverables from three upstream plans. Before writing a single workflow line, confirm each input exists at a stable, consumable entrypoint.

- [ ] **Step 1:** Inventory the five required inputs. For each, record the file path, command, expected exit code on success, and the Plan that owns it:
  - WCAG 2.2 AA axe-core gate → Plan 4 harness; command `node tooling/a11y-audit-runner/dist/index.js`
  - RTL regression gate → Plan 4 Storybook stories with `globalTypes.direction`; command folded into the a11y-audit-runner entrypoint (same invocation, direction matrix from story parameters)
  - CLDR plural-rule gate → Plan 2 Task 3.2; command `dotnet test tooling/Sunfish.Tooling.LocalizationXliff/tests/CldrPluralTests.cs`
  - XLIFF round-trip gate → Plan 2 Task 1.4; command `dotnet test tooling/Sunfish.Tooling.LocalizationXliff/tests/RoundTripTests.cs` + `TwelveLocaleTests.cs`
  - Analyzer gate → Plan 2 Task 4.3 + Plan 4 `SUNFISH_A11Y_001`; command `dotnet build --configuration Release -warnaserror`
- [ ] **Step 2:** Block on any input not yet landed. Each missing input is a Plan-5 blocker, not a workaround candidate. Document blockers in `waves/global-ux/status.md` and escalate.
- [ ] **Step 3:** Record the 4-shard story-count allocation from Plan 4: shard 1 = stories [0, 360); shard 2 = [360, 720); shard 3 = [720, 1080); shard 4 = [1080, 1440). This is the target distribution; Task 2.1 validates actual distribution stays within ±5% of balanced.

### Task 2: Build `tooling/a11y-audit-runner/`

**Files:**
- Create: `tooling/a11y-audit-runner/package.json`
- Create: `tooling/a11y-audit-runner/src/index.ts`
- Create: `tooling/a11y-audit-runner/src/shardAllocator.ts`
- Create: `tooling/a11y-audit-runner/src/storyRunner.ts`
- Create: `tooling/a11y-audit-runner/src/contractAssertions.ts`
- Create: `tooling/a11y-audit-runner/src/budgetEnforcer.ts`
- Create: `tooling/a11y-audit-runner/README.md`

**Why:** Spec §8 line 785 names this tool; no other Plan owns it. It is the only net-new engineering in Plan 5. Everything else is YAML + config.

#### Task 2.1: Shard allocator

- [ ] **Step 1:** Implement `allocate(stories, shardIndex, totalShards)` using `createHash('sha256')(storyId)` → integer → modulo `totalShards`. Deterministic across runs.
- [ ] **Step 2:** Unit tests: (a) same input + same shard → identical story list; (b) distribution balance — no shard has more than `(stories.length / totalShards) × 1.05 + 1` stories; (c) full coverage — union of all shards equals the input set exactly once.
- [ ] **Step 3:** Export `listShard(shardIndex, totalShards)` CLI so a contributor can locally reproduce any shard's assignment without running the gate.

#### Task 2.2: Story runner + contract assertions

- [ ] **Step 1:** `storyRunner.ts` wraps Storybook test-runner's `postVisit` hook: runs `@axe-core/playwright` with `moderate` impact threshold; on any hit, fails the current story with structured output (story ID, violation IDs, nodes).
- [ ] **Step 2:** `contractAssertions.ts` reads `parameters.a11y.sunfish` (per ADR 0034) and asserts: focus order matches visible tab traversal; live region matches announced behavior; `rtlIconMirror` matches CSS `transform` / `dir="rtl"` observed rendering. RTL stories are a second Playwright page load with `dir="rtl"`; LTR + RTL both run for every story (spec §8 "RTL regression gate" line 786).
- [ ] **Step 3:** Screenshot diffing for RTL is handled by Playwright's built-in `toHaveScreenshot` with a per-story baseline under `tooling/a11y-audit-runner/__snapshots__/`. Baseline creation is a one-time `--update-snapshots` run by the Plan-4 author; subsequent runs diff.

#### Task 2.3: Budget enforcer + timing

- [ ] **Step 1:** Per-shard wall-clock budget: hard timeout 20 min (fail-loud), warning at 15 min (emit annotation to PR). Implemented as a `Promise.race` against a setTimeout.
- [ ] **Step 2:** Per-story timing histogram emitted as JSON artifact; ingested by Task 5 (10-run measurement) and archived under `waves/global-ux/` as timing baselines.
- [ ] **Step 3:** Re-measure the Week-1 extrapolation on real Plan 4 output. Record p50, p95, p99 per shard in `waves/global-ux/week-4-runtime-remeasurement.md`. This data feeds Task 5's go/no-go.

#### Task 2.4: Package plumbing + artifact size check

- [ ] **Step 1:** `package.json` declares `type: module`, Node `>= 20`, dependencies pinned to exact versions (Playwright, axe-core, Storybook test-runner).
- [ ] **Step 2:** Add to root `pnpm-workspace.yaml` if not already present via Plan 4.
- [ ] **Step 3:** Measure `pnpm build-storybook` artifact size on the Plan 4 cascade; record in the same `week-4-runtime-remeasurement.md`. If > 500 MB, switch to `actions/cache@v4` with SHA-keyed cache before Task 3.1.

### Task 3: Author `.github/workflows/global-ux-gate.yml`

**Files:**
- Create: `.github/workflows/global-ux-gate.yml`

**Why:** This is the Phase 1 exit-gate artifact. Every other task in Plan 5 either feeds it or validates it.

- [ ] **Step 1:** Workflow skeleton (sketch — implementer fills in cache keys, exact action SHAs, secret refs):

```yaml
name: Global-First UX Gate
on:
  pull_request:
    paths:
      - 'packages/ui-core/**'
      - 'packages/ui-adapters-*/**'
      - 'packages/*/Resources/**'
      - 'tooling/a11y-audit-runner/**'
      - 'tooling/Sunfish.Tooling.LocalizationXliff/**'
      - 'packages/analyzers/**'
      - '.github/workflows/global-ux-gate.yml'
  push:
    branches: [main]

concurrency:
  group: global-ux-gate-${{ github.ref }}
  cancel-in-progress: true

jobs:
  analyzers:
    name: Analyzer gate (SUNFISH_I18N_001, SUNFISH_I18N_002, SUNFISH_A11Y_001 = error)
    runs-on: windows-latest
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '11.0.x', include-prerelease: true }
      - run: dotnet build --configuration Release -warnaserror
      - name: Assert diagnostic promotion
        run: dotnet build packages/ui-core/ --configuration Release -warnaserror /p:TreatWarningsAsErrors=true

  xliff-roundtrip:
    name: XLIFF 2.0 round-trip (12 locales)
    runs-on: windows-latest
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - run: dotnet test tooling/Sunfish.Tooling.LocalizationXliff/tests/ --configuration Release --logger "trx;LogFileName=xliff-roundtrip.trx"

  cldr-plurals:
    name: CLDR plural-rule gate (12 locales)
    runs-on: windows-latest
    timeout-minutes: 5
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - run: dotnet test tooling/Sunfish.Tooling.LocalizationXliff/tests/CldrPluralTests.cs --configuration Release

  a11y-audit:
    name: WCAG 2.2 AA + RTL regression (shard ${{ matrix.shard }}/4)
    runs-on: ubuntu-latest
    timeout-minutes: 20
    strategy:
      fail-fast: false
      matrix:
        shard: [1, 2, 3, 4]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20', cache: 'pnpm' }
      - uses: pnpm/action-setup@v4
      - run: pnpm install --frozen-lockfile
      - run: pnpm --filter @sunfish/ui-core build-storybook
      - run: npx playwright install --with-deps chromium
      - run: node tooling/a11y-audit-runner/dist/index.js --shard ${{ matrix.shard }} --total-shards 4
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: a11y-timings-shard-${{ matrix.shard }}
          path: tooling/a11y-audit-runner/artifacts/

  locale-completeness:
    name: Locale completeness floor (locales.json)
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
      - run: node tooling/locale-completeness-check/index.js
```

- [ ] **Step 2:** Dry-run the workflow on a throwaway branch `global-ux/plan-5-dry-run`. Every job must go green on a no-op PR. Any red is a Plan 5 blocker, not a "known flake to quarantine."
- [ ] **Step 3:** Confirm path-triggers fire correctly: open a test PR modifying only `packages/foundation/Localization/` (Plan 2 surface) and verify `analyzers` + `xliff-roundtrip` + `cldr-plurals` fire; `a11y-audit` and `locale-completeness` may skip if `paths` don't match — document observed behavior.

### Task 4: Analyzer-severity promotion + branch protection

**Files:**
- Modify: `.editorconfig` (root)
- Modify: `packages/analyzers/i18n/Sunfish.Analyzers.I18n.props`
- Modify: `packages/analyzers/accessibility/Sunfish.Analyzers.Accessibility.props`
- Modify: `docs/diagnostic-codes.md`
- Create: `infra/github/branch-protection-main.json`
- Create: `infra/github/apply-branch-protection.sh`

#### Task 4.1: Promote analyzer severities

- [ ] **Step 1:** In root `.editorconfig`, add:
  ```
  dotnet_diagnostic.SUNFISH_I18N_001.severity = error
  dotnet_diagnostic.SUNFISH_I18N_002.severity = error
  dotnet_diagnostic.SUNFISH_A11Y_001.severity = error
  ```
- [ ] **Step 2:** Mirror in the analyzer `.props` files so consumers that don't inherit root `.editorconfig` still get the promotion.
- [ ] **Step 3:** Run `dotnet build --configuration Release -warnaserror` on the full repo. Record the count of newly failing files. If > 5 files fail **legitimately** (not Plan-2/3/4 regressions), file a baseline entry in `_shared/engineering/i18n-baseline.md` with a 2026-07-01 target date — do not suppress the rule.
- [ ] **Step 4:** Update `docs/diagnostic-codes.md` with the severity change, the Plan-5 effective date, and a link back to spec §8.

#### Task 4.2: Branch-protection rule

- [ ] **Step 1:** Author `infra/github/branch-protection-main.json` — the exact GitHub branch-protection payload including `required_status_checks.contexts`:
  ```json
  {
    "required_status_checks": {
      "strict": true,
      "contexts": [
        "Analyzer gate (SUNFISH_I18N_001, SUNFISH_I18N_002, SUNFISH_A11Y_001 = error)",
        "XLIFF 2.0 round-trip (12 locales)",
        "CLDR plural-rule gate (12 locales)",
        "WCAG 2.2 AA + RTL regression (shard 1/4)",
        "WCAG 2.2 AA + RTL regression (shard 2/4)",
        "WCAG 2.2 AA + RTL regression (shard 3/4)",
        "WCAG 2.2 AA + RTL regression (shard 4/4)",
        "Locale completeness floor (locales.json)"
      ]
    },
    "enforce_admins": false,
    "required_pull_request_reviews": { "required_approving_review_count": 1 },
    "restrictions": null,
    "allow_force_pushes": false,
    "allow_deletions": false
  }
  ```
- [ ] **Step 2:** `apply-branch-protection.sh` wraps `gh api -X PUT /repos/{owner}/{repo}/branches/main/protection --input branch-protection-main.json`. Idempotent; re-runnable.
- [ ] **Step 3:** Apply during a low-traffic window (e.g., end of business day). Monitor existing open PRs for new-required-check red; instruct PR authors to rerun via `gh pr checks --watch`.

#### Task 4.3: Concurrency burst test

- [ ] **Step 1:** Open 5 synthetic PRs modifying different Phase-1-surface paths. Verify GitHub's action-concurrency does not queue-starve any job beyond 5 minutes.
- [ ] **Step 2:** If starvation observed, move `locale-completeness` to a scheduled daily job and remove from the required-check list; document in `_shared/engineering/ci-quality-gates.md`.

### Task 5: 10-run measurement + Phase 1 exit-gate report

**Files:**
- Create: `waves/global-ux/week-4-runtime-remeasurement.md`
- Create: `waves/global-ux/week-4-phase1-exit-gate-report.md`
- Modify: `waves/global-ux/status.md`
- Modify: `_shared/engineering/ci-quality-gates.md`

**Why:** Spec §8 "Runtime budget" requires p95 < 15 min. A single run is insufficient evidence; 10 consecutive runs establish the p95 baseline for the exit-gate verdict and for post-Phase-1 drift monitoring.

- [ ] **Step 1:** Trigger 10 consecutive runs of `global-ux-gate.yml` on the Phase-1 surface (use a loop of empty-commit amends on a test branch, or 10 sequential PRs). Record per-shard wall-clock and the union p95 (slowest shard per run).
- [ ] **Step 2:** Compute union p50 / p95 / p99. If p95 > 15 min → FAIL per §8 budget → trigger 8-shard expansion per spec §8 "Remedy on breach." Re-measure; if still > 15 min, apply Plan 5 FAILED-trigger (a), (b), or (c) from Success Criteria.
- [ ] **Step 3:** Author `week-4-phase1-exit-gate-report.md` with: per-criterion PASS/FAIL/DEFERRED evidence links, the 10-run timing data, any baseline entries added to `a11y-baseline.md` / `i18n-baseline.md`, and the explicit binary verdict: **PROCEED to Plan 6 (Phase 2 cascade)** OR **scope-cut-per-kill-trigger**.
- [ ] **Step 4:** Update `waves/global-ux/status.md` — mark Phase 1 complete (or in-flight with named fallback), point the next-agent handoff at Plan 6.
- [ ] **Step 5:** Update `_shared/engineering/ci-quality-gates.md` with the Plan 5 workflow as the canonical Phase 1+ gate.

---

## Verification

### Automated

- `pnpm --filter @sunfish/a11y-audit-runner test` — shard allocator determinism + balance + coverage; contract assertion unit tests
- `.github/workflows/global-ux-gate.yml` dry-run on `global-ux/plan-5-dry-run` — all 5 jobs green
- Synthetic 5-PR burst → no queue-starvation beyond 5 min
- 10-run p95 measurement → < 15 min union across all shards

### Manual

- Open a PR that deliberately violates each rule and confirm the correct job fails with a clear message:
  - `.resx` missing `<comment>` → `analyzers` job fails with `SUNFISH_I18N_001 error`
  - Component without sibling `*.stories.ts` → `analyzers` fails with `SUNFISH_A11Y_001 error`
  - Story with axe violation → `a11y-audit` (correct shard) fails with axe output
  - RTL-mirror story missing `rtlIconMirror` parameter → contract-assertion failure
  - Hand-perturbed XLIFF file that breaks round-trip → `xliff-roundtrip` fails
- Visual check: GitHub PR status-check UI shows all 8 required contexts (5 jobs, with `a11y-audit` sharded 4-way). Confirm the branch-protection rule blocks merge until all contexts pass.

### Ongoing Observability

- Weekly p95-drift report (auto-generated by a scheduled workflow post-Phase-1, out of scope for Plan 5 but named as a Plan 6 handoff)
- Baseline-drift review: every entry in `a11y-baseline.md` / `i18n-baseline.md` with a target date ≤ current month surfaces in monthly roadmap review
- Concurrency-burst re-test: run the 5-PR synthetic burst monthly; any > 5 min queue delay triggers a re-shard or job demotion

### Concrete CI-workflow YAML sketch

See Task 3 Step 1 above — the skeleton is load-bearing for the implementer and is intentionally duplicated here conceptually (don't copy-paste; implement the one authored under Task 3).

---

## Conditional sections

### Rollback Strategy

- **`a11y-audit` flake rate unacceptable:** Mark the job `continue-on-error: true` for 2 weeks; remove from required-status list. Phase 2 cannot start, but the gate remains informational. Re-promote after flake rate < 1% for 10 consecutive runs.
- **Analyzer error-severity promotion blocks too many in-flight PRs:** Revert `.editorconfig` to `severity = warning` for `SUNFISH_A11Y_001` only (keep `SUNFISH_I18N_001` / `SUNFISH_I18N_002` at error). Add a named baseline entry with a 2026-09-01 target. Phase 2 still starts, but `SUNFISH_A11Y_001` promotion is a Phase-2-exit deliverable.
- **Branch-protection apply fails or goes wrong:** `gh api -X DELETE /repos/{owner}/{repo}/branches/main/protection` reverts instantly. Re-run `apply-branch-protection.sh` after fixing the payload.
- **8-shard expansion still misses p95 budget:** Drop CVD × 3 simulation axis from per-commit a11y matrix; move to nightly. Reduces scenario count 3× immediately.

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Plan 4 harness not ready by Plan 5 start | Medium | High (blocks Plan 5 entirely) | Task 1 inventory gate; escalate if missing |
| Axe-core version drift between local dev and CI causes "green locally, red in CI" | Medium | Medium | Exact-version pin in `a11y-audit-runner/package.json`; document local-run invocation in README |
| Shard allocator becomes unbalanced as Plan 4 adds stories post-Phase-1 | Low | Low | Balance-ratio assertion in unit tests; test re-runs on every Plan 4 story addition |
| Branch-protection rule accidentally blocks admin bypasses needed for incident response | Low | High | `enforce_admins: false` in payload; document the admin-bypass path in `_shared/engineering/ci-quality-gates.md` |
| GitHub Actions minutes budget exhausted by 4-shard matrix × high-frequency PRs | Low-Medium | Medium | `concurrency: cancel-in-progress: true` prevents duplicate-run stacking; monitor monthly spend |

### Dependencies & Blockers

- **Depends on:** Plan 2 Task 1.4 (XLIFF round-trip tests), Plan 2 Task 3.2 (CLDR plural tests), Plan 2 Task 4.3 (`SUNFISH_I18N_001` analyzer), Plan 4 (full harness cascade + `SUNFISH_A11Y_001` analyzer, Storybook `parameters.a11y.sunfish` contract block per ADR 0034)
- **Blocks:** Plan 6 (Phase 2 cascade) — cannot begin until Plan 5 workflow is live on `main` with all gates passing on the Phase 1 surface (spec §8 line 893)
- **Does not block:** Plan 3 (Translator-Assist Phase 1 core) — Translator-Assist runs alongside Plan 5; its deliverables feed Plan 6 gate additions, not Plan 5
- **External dependency:** GitHub Actions uptime; `actions/setup-dotnet@v4` support for .NET 11 preview

### Delegation & Team Strategy

- **Solo-by-Claude for Tasks 1, 3, 4, 5:** Workflow authorship + branch protection + measurement is a sequential, state-dependent chain; subagent parallelism adds coordination overhead without speedup.
- **One subagent for Task 2 (a11y-audit-runner):** Narrow scope, pure TypeScript engineering, path-scoped commits under `tooling/a11y-audit-runner/` only. Subagent MUST use path-scoped `git add tooling/a11y-audit-runner/`; MUST NOT touch `.github/workflows/`. Reviewer-agent gate before merge.
- **No subagent for analyzer-severity promotion:** One-line config changes across three files; direct Claude edit is faster than subagent dispatch.

### Incremental Delivery

- **Day 1:** Task 1 inventory complete; Task 2 scaffolded (package.json + shard allocator + tests green).
- **Day 2:** Task 2 complete (runner + contract assertions + budget enforcer, all tests green). Task 3 workflow authored; dry-run on throwaway branch.
- **Day 3:** Task 4.1 analyzer-severity promotion committed and building clean. Task 4.2 branch-protection payload authored (not yet applied).
- **Day 4:** Task 4.2 applied during low-traffic window; Task 4.3 concurrency-burst test run.
- **Day 5:** Task 5 10-run measurement; exit-gate report authored; verdict recorded in `status.md`.

### Reference Library

- Parent spec §8: [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../specs/2026-04-24-global-first-ux-design.md#8-ci-gates--analyzer-package) (lines 758-902)
- Exit-gate condition: spec §8 "Phase 1 exit gate" (line 893)
- Runtime budget: spec §8 "Runtime budget" + `_shared/engineering/ci-quality-gates.md`
- Week-1 measurement data: [`waves/global-ux/week-1-runtime-measurement.md`](../../../waves/global-ux/week-1-runtime-measurement.md) (4-shard projection, 12-min estimate)
- Plan 1: [`2026-04-24-global-first-ux-phase-1-week-1-plan.md`](./2026-04-24-global-first-ux-phase-1-week-1-plan.md)
- Plan 2: [`2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md`](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md)
- [ADR 0034](../../adrs/0034-a11y-harness-per-adapter.md) — `parameters.a11y.sunfish` contract block shape (consumed by contract assertions in Task 2.2)
- [ADR 0035](../../adrs/0035-global-domain-types-as-separate-wave.md) — scope boundary for what Plan 5 does NOT gate
- GitHub branch protection API: https://docs.github.com/en/rest/branches/branch-protection
- Storybook test-runner: https://storybook.js.org/docs/writing-tests/integrations/test-runner
- `@axe-core/playwright`: https://github.com/dequelabs/axe-core-npm/tree/develop/packages/playwright

### Learning & Knowledge Capture

- Record in `waves/global-ux/decisions.md` any deviation from the 4-shard default (e.g., expansion to 8, demotion of a job from required, CVD-axis removal).
- End-of-Week-4 retrospective in `week-4-phase1-exit-gate-report.md`: what surprised us about runtime, which gate flaked most, which analyzer promotion was noisiest — all inputs to Plan 6's gate-expansion strategy.
- Post-Phase-1: the 10-run p95 baseline becomes the drift-alert threshold. Capture it in `ci-quality-gates.md` as the canonical number.

### Replanning Triggers

- Any Plan-2/3/4 input slips past 2026-05-15: Plan 5 re-schedules; escalate immediately — Plan 5 is pure wiring and cannot pre-build.
- Runtime p95 > 15 min after 8-shard expansion: trigger kill-trigger option (a), (b), or (c) from Success Criteria.
- Analyzer-severity promotion breaks > 5 legitimate (non-regression) consumers: revert `SUNFISH_A11Y_001` to warning; add baseline entry; keep Plan 5 on track otherwise.

### Completion Gate

Plan 5 is complete when ALL of these are true:
- ☐ `.github/workflows/global-ux-gate.yml` merged to `main`
- ☐ Branch-protection rule applied; all 8 required contexts listed
- ☐ `SUNFISH_I18N_001`, `SUNFISH_I18N_002`, `SUNFISH_A11Y_001` at error severity; full repo builds clean with `-warnaserror`
- ☐ 10-run p95 < 15 min across all shards
- ☐ `waves/global-ux/week-4-phase1-exit-gate-report.md` records PROCEED verdict
- ☐ `waves/global-ux/status.md` updated with Phase 1 → Plan 6 handoff

---

## Cold Start Test

A fresh agent walking into this plan should be able to execute Task 1 without further context by:
1. Reading this plan.
2. Reading Plan 2 for the upstream Plan-2 deliverables Plan 5 gates on.
3. Reading Plan 4's latest artifacts (harness cascade output) to confirm Task 1 inputs exist.
4. Reading spec §8 (lines 758-902) for the full CI-gate specification.
5. Reading `waves/global-ux/week-1-runtime-measurement.md` for the runtime-budget baseline.

No additional context should be required. If any step requires out-of-band knowledge not in one of those five documents, that is a plan-hygiene bug — file an issue and update this plan before executing.
