# Global-First UX — Wave Status

**Updated:** 2026-04-24 end of Phase 1 Week 1 + full Phase 1/2 planning landed
**Current phase:** Phase 1 Week 2 execution (in flight)
**Current focus:** Plan 2 Tasks 1.1-1.3 (XLIFF tooling) landed on main; Plan 3 Task 1.1 (MADLAD CLI scaffold) landed on main

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

## In progress
- (none)

## Blocked
- (none)

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
| [Plan 2](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md) | Loc-Infra cascade (§3A) | 2-4 | 448 | Ready to execute |
| [Plan 3](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md) | Translator-Assist (§3B) | 2-4 (parallel) | 531 | Ready to execute |
| [Plan 4](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-a11y-foundation-plan.md) | A11y Foundation cascade (§7) | 2-4 (parallel) | 540 | Ready to execute |
| [Plan 4B](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-3-6-ui-sensory-cascade-plan.md) | ui-core sensory cascade (§2 + §5 + §6) | 3-6 (parallel) | 554 | Ready to execute |
| [Plan 5](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-week-6-ci-gates-plan.md) | CI Gates (§8) — Phase 1 exit gate | Week 6 | 459 | Ready — depends on Plans 2/3/4/4B output |
| [Plan 6](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-2-cascade-plan.md) | Phase 2 cascade (kitchen-sink + Bridge + Anchor + docs) | 5-12 | 648 | Ready — blocked on Plan 5 exit gate |

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
