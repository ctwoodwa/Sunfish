# Global-First UX — Wave Status

**Updated:** 2026-04-25 — Wave 4 close-out: cascade coverage report + Plan 5 entry verdict
**Current phase:** Phase 1 Weeks 2-4 COMPLETE for Plan 2 cascade (17 packages); Plans 3 / 4 / 4B continue independently; Plan 5 entry GREEN
**Current focus:** Plan 5 dispatchable (Wk-6 CI Gates); blocks-workflow DI follow-up (~5-line standalone PR); Plans 3 / 4 / 4B advancement decisions await human owner

## Completed this week
- Task 1: Scaffold `waves/global-ux/` tracking directory (`9f14858b`)
- Task 2: ICU4N health check memo — verdict **PIVOT TO FALLBACK** (SmartFormat.NET + .NET 8 System.Globalization). See decisions.md entry 2026-04-25. (`cb286789`)
- Task 3: Weblate vs Crowdin memo — default (Weblate self-hosted) confirmed. AGPL flagged as open legal question for future commercial hosting. (`512c0aae`)
- Task 4: XLIFF tool ecosystem memo — recommendation **BUILD** (~5.5 days). MAT EOL Oct 2025; no maintained .NET XLIFF 2.0 library exists. (`74ed1e94`)
- Task 5: ADR 0034 (A11y Harness per Adapter) — Proposed (`f33baeba`)
- Task 6: ADR 0035 (Global Domain Types as Separate Wave) — Proposed (`7e7ccb71`)
- Task 7: ADRs 0034 + 0035 flipped to Accepted (`972e2f6b`)
- Task 8: pnpm workspace + `@sunfish/ui-core` package.json; 1102 packages installed (`5f198e82`)
- Task 9: Storybook 8 config with a11y addon + RTL toggle; tsconfig strict mode; typecheck clean (`62d8dd69`)
- Task 10: `sunfish-button` pilot with a11y contract (`cc37a9b8`)
- Task 11: `sunfish-dialog` pilot with focus trap + aria-modal + composedOf (`91fbe41a`)
- Task 12: `sunfish-syncstate-indicator` pilot with multimodal encoding (color + shape + label + role) (`12b3c674`)
- Task 13: Runtime measurement — 8.99s build, ~0.75s/story smoke. Verdict **GREEN** at 4 shards. (`4e82f8bc`)
- Feature branch `global-ux/code-pilot` fast-forward-merged to main.
- Task 14: `ISunfishLocalizer<T>` + `SunfishLocalizer<T>` SmartFormat.NET wrapper; foundation builds clean. (`fa5f6750`)
- Task 15: Three CLDR plural-rule smoke tests (en/ar/ja) — **3/3 green in 80ms**. (`96a6ad54`)

## Go/No-Go gate assessment

| Gate | Status | Evidence |
|---|---|---|
| **Localization wrapper** (Tasks 14-15 — smoke tests) | ✅ **PASS** | 3 CLDR tests green via SmartFormat.NET. Arabic six-form plural (zero/one/two/few/many/other) and Japanese single-form both resolve correctly. ICU4N pivot validated. |
| **Storybook a11y harness** (Tasks 9-12) | ✅ **PASS** | 3 pilot components render under Storybook 8; 5 stories pass axe smoke (0 violations). Build clean in 8.99s. |
| **Shadow-DOM traversal** (`@axe-core/playwright` reaches shadow roots) | ✅ **PASS** | test-storybook + axe-playwright reaches Lit open shadow roots successfully across all 3 pilots. |
| **CI runtime budget** (<15 min p95 overall) | ✅ **PASS** | 0.75s/story smoke; projected 12 min on 4 shards with production axe config. Fits the 15-min p95 budget. Per-component time in full matrix = ~18s at 4 shards (above the 15s/component line but within overall budget). |
| **Weblate legal review** (AGPL concerns) | ⚠️ **YELLOW** | Internal use confirmed no AGPL trigger. Commercial managed-hosting would trigger §13 — counsel review required before that product line. Spec default (Weblate self-hosted) confirmed; fallback named (Crowdin Business). Not a rollback trigger for Week 1. |

## Verdict

**GO — proceed to Phase 1 Weeks 2-4.**

All five gates are GREEN except Weblate legal which is YELLOW (not a rollback trigger per spec Section 1). One material pivot recorded: ICU4N → SmartFormat.NET + .NET 8 System.Globalization (see `decisions.md` 2026-04-25). Pivot is validated by passing smoke tests; downstream Tasks 14-15 re-scope landed cleanly in ~1 hour wall clock.

## In progress (2026-04-25 — from Wave 1 sub-reports)

- **Plan 2 (Loc-Infra cascade)** — verdict YELLOW. Workstream A (XLIFF tooling, 1.1-1.5) DONE; Week 4 polish (4.1 hot-reload, 4.2 ProblemDetailsFactory, 4.3 analyzer) DONE on main via PR #66 + cascading commits; Workstream B run-time tasks (2.2 / 2.4 / 2.5) gated on a running Weblate VM (IN-PROGRESS or NOT-STARTED). 4.4 Arabic E2E IN-PROGRESS (XLIFF MSBuild wiring on PR #76 not yet on main). Detail: `waves/global-ux/week-3-plan-2-status.md`.
- **Plan 4 (A11y Foundation)** — verdict YELLOW. Workstream A (bUnit-to-axe bridge) DONE end-to-end with BRIDGE-READY verdict (`28663d78`); 36-scenario matrix green ~7s; Workstream B production `postVisit` hook landed (`fdee0e25`) and caught real pilot bugs which were fixed (`729e9c39`). Detail: `waves/global-ux/week-3-plan-4-status.md`.
- **Plan 4B (UI Sensory Cascade)** — verdict YELLOW. 8 DONE / 3 IN-PROGRESS / 12 NOT-STARTED across 23 tasks. §5 SyncState gate landed (CVD audit `c4d8e344`, palette rework `295f39bd`, ADR 0036 accepted `449d8bfb`); §2 cascade clean (`6cc20102`, `08e2e0cd` drove audit 173→0). §6 motion only has harness scaffolding. Detail: `waves/global-ux/week-3-plan-4b-status.md`.

## Blocked / RED (2026-04-25)

- **Plan 3 (Translator-Assist)** — verdict **RED**. 1 of 19 tasks DONE (Task 1.1 LocExtraction CLI scaffold `52c9941e`); 17 NOT-STARTED. No extractor body, no Husky hook, no LocQuality tool, no Weblate plugins, no MADLAD draft generator, no `madlad-smoke.yml` / `loc-quality.yml`, no `docs/i18n/` (recruitment runbook + review guide absent). Plan 3 dependencies on Plan 2 are largely landed, so the gating constraint is execution bandwidth, not infra. Detail: `waves/global-ux/week-3-plan-3-status.md`.

## Plan 4 30-day kill-trigger watch

Plan 4's kill trigger fires **2026-05-24** (29 days from today). Week 3 cascade unstarted means a scope-cut conversation may be needed proactively — flagged for human owner.

## Wave 0 reconciliation outcome (2026-04-25)

Wave 0 of the reconciliation+cascade loop ([plan](../../docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md)) verified that the local Plan 2 Task 4.x cascade work and PR #66's cascade work converged byte-identical across all 11 overlap files. Diff memo at `waves/global-ux/reconciliation-pr66-diff-memo.md`. Shipped via PR [#79](https://github.com/ctwoodwa/Sunfish/pull/79) (squash-merged as `ca621bb5`). Plan v1.1 hardening shipped via PR [#80](https://github.com/ctwoodwa/Sunfish/pull/80). Plan v1.2 council remediation in PR [#81](https://github.com/ctwoodwa/Sunfish/pull/81) (auto-merge armed).

## Plan 5 entry verdict (2026-04-25 — Wave 4 close-out)

✅ **READY-TO-DISPATCH** — both Plan 2 Task 3.6 binary gates met:
- `find packages/blocks-* -name SharedResource.resx | wc -l` = **14** (target 14)
- `grep -r 'AddLocalization()' packages/ apps/ accelerators/` ≥ 3 hits (target ≥ 3; actual ≥ 9)

Wave 2 cascade landed via PR [#84](https://github.com/ctwoodwa/Sunfish/pull/84) — 5 cluster commits, 17 packages, 62 files, ~3,500 lines. Sentinel + canary + 4-cluster fan-out + 4 parallel reviewers + diff-shape automated check + human spot-check + pre-merge SHA check. Per-cluster reports and reviews preserved at `waves/global-ux/wave-2-cluster-*-report.md` and `waves/global-ux/wave-3-cluster-*-review.md`. Coverage report at `waves/global-ux/week-3-cascade-coverage-report.md`.

**Plan 5 entry conditions met:**
- Plan 2 cascade infrastructure landed (skeletons + DI for Pattern A; consumer-side wiring for Pattern B and composition root)
- Architectural finding (Cluster A sentinel ratification): blocks/adapters use `TryAddSingleton(typeof(ISunfishLocalizer<>), ...)` only; consumers (apps, accelerators) own `services.AddLocalization()`. Bridge precedent confirmed.
- Foundation, anchor, bridge already shipped via Wave 0 reconciliation (PR #79 squash-merge `ca621bb5`).

**Plan 5 entry conditions NOT met (carried forward as inputs to Plan 5 itself, not blockers):**
- Plan 3 RED — translator-assist tooling 1 of 19 tasks done; bandwidth-limited (acknowledged in Wave 1 re-prioritization gate; user chose `proceed`).
- Plan 4 cascade NOT-STARTED — bUnit-axe bridge done; ui-core a11y harness cascade not begun. 30-day kill trigger fires 2026-05-24 (29 days out).
- Plan 4B 12 of 23 tasks NOT-STARTED — §6 motion only scaffolded.

Plan 5 should add CI gates that surface these gaps automatically:
- Permanent Plan-2 gate (`.resx` count + DI grep)
- (Per v1.3 Seat-2 P5 deferral) RESX `<comment>` HTML-metacharacter scan
- (Per Wave 1 finding) gate that fails build if Plans 3/4/4B fall further behind YELLOW for >7 days

## Wave 4 follow-up (tracked, not blocking)

- **blocks-workflow DI line** — `.csproj` lacks foundation `ProjectReference`; v1.3 Seat-2 P1 forbade `.csproj` edits in cluster commits. Standalone follow-up PR (~5 lines: 1 ProjectReference + 1 using + 1 TryAddSingleton + 1 cosmetic doc-cref restore). Tracked here.
- **GitButler workspace recovery** — `gitbutler/workspace` has direct commits requiring `but teardown` to recover before GitButler-native workflow can resume. Out-of-band; affects future sessions, not Plan 5 entry.

## Wave 1 re-prioritization gate (Task 1.F, v1.2)

Per loop plan v1.2 decision matrix:
- **Any RED** → halt loop with `Halt reason: wave-1-reprioritize-needed`; surface to human owner with named recommendation.

**Recommendation to human owner:** Plan 3 RED — recommend pausing Wave 2 cluster cascade (600-800k token spend) and instead advancing Plan 3's most critical NOT-STARTED tasks (Husky pre-commit hook for placeholder validation, LocQuality CLI scaffold, MADLAD draft generator). Plan 3 is the only translator-facing system; without it, the cascade infra Wave 2 builds has no end-to-end validator.

**Alternatives the human owner may select:**
1. **proceed** — accept Plan 3 RED; continue Wave 2 cluster cascade as planned. Plan 3 advances in parallel (separate plan/loop).
2. **pivot-to-plan-3** — halt this plan; author a new loop plan for Plan 3 advancement; resume this plan after Plan 3 reaches YELLOW.
3. **scope-cut** — proceed Wave 2 with reduced scope (e.g., skip Cluster D1 = ui-core + ui-adapters-blazor; only do A/B/C/E).

Human owner records decision via `user-reprioritization-decision: proceed | pivot-to-plan-3 | scope-cut` in tracker.

## Decision ripple — ICU4N pivot
- **Task 14** (scaffold ICU4N wrapper) needs re-scoping to use SmartFormat.NET behind
  `ISunfishLocalizer`. Public contract unchanged; implementation pivots.
- **Task 15** (three ICU smoke tests) tests SmartFormat.NET behaviour rather than ICU4N.
- Spec Section 3A needs a revision note pointing to `decisions.md`. Defer spec edit until
  Week 1 go/no-go gate decides whether the pivot holds.

## Plans authored (all Phase 1 + Phase 2)

| Plan | Scope | Weeks | Lines | Status |
|---|---|---|---|---|
| [Plan 1](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-week-1-plan.md) | Tooling pilot | Week 1 | 1548 | Complete (GO verdict) |
| [Plan 2](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md) | Loc-Infra cascade (§3A) | 2-4 | 448 | **GREEN — COMPLETE for Plan 5 entry purposes**; Wk 3 cascade landed via Wave 2 (PR [#84](https://github.com/ctwoodwa/Sunfish/pull/84)); 14 of 14 blocks-* + ui-core + ui-adapters-blazor + kitchen-sink. blocks-workflow DI follow-up tracked. Workstream B run-time (Weblate VM) still gated. Coverage: `week-3-cascade-coverage-report.md` |
| [Plan 3](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md) | Translator-Assist (§3B) | 2-4 (parallel) | 531 | **RED** — 1 of 19 tasks DONE; bandwidth-limited not infra-limited. Detail: `week-3-plan-3-status.md` |
| [Plan 4](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-a11y-foundation-plan.md) | A11y Foundation cascade (§7) | 2-4 (parallel) | 540 | **YELLOW** — Workstream A (bUnit-axe bridge) DONE end-to-end; Wk 3 cascade NOT-STARTED; 30-day kill trigger fires 2026-05-24. Detail: `week-3-plan-4-status.md` |
| [Plan 4B](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-3-6-ui-sensory-cascade-plan.md) | ui-core sensory cascade (§2 + §5 + §6) | 3-6 (parallel) | 554 | **YELLOW** — 8 DONE / 3 IN-PROGRESS / 12 NOT-STARTED; §5 SyncState gate landed; §2 cascade clean; §6 motion only scaffolded. Detail: `week-3-plan-4b-status.md` |
| [Plan 5](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-week-6-ci-gates-plan.md) | CI Gates (§8) — Phase 1 exit gate | Week 6 | 459 | **READY-TO-DISPATCH** — Plan 2 entry conditions met (PR #84). Plan 5 should also gate Plans 3/4/4B per cross-plan health checks (see Plan 5 entry verdict above). |
| [Plan 6](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-2-cascade-plan.md) | Phase 2 cascade (kitchen-sink + Bridge + Anchor + docs) | 5-12 | 648 | Ready — blocked on Plan 5 exit gate (transitively blocked) |

**Planning totals:** 7 plans, 4,728 lines across Phase 1 + Phase 2 (~20 weeks of scoped work, with translator-vendor budget of ~$745k envelope per Plan 6).

**Corrections already applied during plan landing:**
- Plan 2 diagnostic IDs (`SUN_LOC_*` → spec-canonical `SUNFISH_I18N_*`).
- Plan 5 timeline (Week 4 → Week 6) and filename rename.
- Plan 2 blocks-* count (15 → 14 confirmed per repo inventory).

**Follow-ups flagged by parallel plan agents (not yet actioned):**
- ADR 0034 addendum for the Node→.NET `a11y-contracts.json` bridge (Plan 4 surface).
- ADR 0036 for the SyncState 5-state palette contract (proposed by Plan 4B).
- AGPL §13 counsel review (Plan 3 dependency; needed by Week 3).
- Translator-vendor contracting (Plan 6 precondition; 4 paid locales + 8 volunteer onboarding).
- Docs-site translation-volume cost ($750k of 772k source words) is the Plan 6 kill-trigger candidate.

## Next agent handoff context

**Planning phase COMPLETE.** Forward motion options (awaiting user priority):

- **Plan 2 — Loc-Infra cascade (Weeks 2-4):** SmartFormat wrapper cascades to all `packages/*`
  that currently string-format user-facing copy; XLIFF 2.0 MSBuild task build (~5.5 days per
  Task 4 memo); Weblate self-hosted instance stood up; 12-locale `.resx` skeletons scaffolded
  with translator workflow.
- **Plan 3 — Translator-Assist core (Weeks 2-4, parallel):** MADLAD-400-3B-MT GGUF via
  llama.cpp wired to Weblate OpenAI-compatible MT backend for pre-publish translation
  suggestions; quality gate heuristics.
- **Plan 4 — A11y Foundation cascade (Weeks 2-4, parallel):** ADR 0034 harness pattern
  extends from 3 pilots to the remaining `ui-core` component inventory + `ui-adapters-react`
  + bUnit-to-axe bridge for `ui-adapters-blazor`.
- **Plan 5 — CI Gates (Week 4):** WCAG 2.2 AA gate + RTL-regression gate + CLDR plural-test
  gate wired to the branch-protection rules so none of the cascades can regress.
- **Plan 6 — Phase 2 Cascade (Weeks 5-12):** Application into blocks + apps (kitchen-sink,
  bridge, anchor).

**Spec Section 3A revision note** landed in commit 279ab364.

**Week 2 follow-ups** (carried forward from Week-1 — now absorbed into Plan 4/4B/5):
- Jest haste-map ignores (low priority; noise only).
- Production `@axe-core/playwright` hook in `.storybook/test-runner.ts` → Plan 4 Task 1.x.
- bUnit-to-axe bridge project → Plan 4 Workstream A (highest-risk; Thursday-Week-2 gate).

**Commit strategy going forward:** Phase 1 Week 1 used 12+ commits across main + 2 feature
branches. Planning wave added ~10 commits to main. Execution waves should use one feature
branch per plan; merge via fast-forward when gates pass. Path-scoped `git add` remains
mandatory. Plan-4-vs-Plan-4B file-overlap on `.storybook/preview.ts` / `test-runner.ts`
requires serial-commit reviewer-agent gating.
