# Plan 4 Status — A11y Foundation Cascade (Phase 1 Weeks 2-4)

**Date:** 2026-04-25
**Source plan:** docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-a11y-foundation-plan.md
**Reporter:** Wave 1 Subagent 1.C

## Per-task status

| Task | Description | Status | Evidence |
|---|---|---|---|
| 1.1 | Scaffold bridge project (`Sunfish.UIAdapters.Blazor.A11y.csproj`) | DONE | `dec4bbc3` (csproj + scaffold). Note: actual path is `packages/ui-adapters-blazor-a11y/` (hyphen, not dot — minor naming drift from plan). |
| 1.2 | Playwright page host | DONE | `c882b5ea` (PlaywrightPageHost.cs + AxeRunner) |
| 1.3 | bUnit markup determinism gate | DONE | `1f1d5618` — 3 fixtures × 100 renders, byte-identical hashes; PASSED verdict logged in `week-2-bunit-bridge-report.md` |
| 1.4 | Core bridge implementation (`BunitToAxeBridge.cs`) | DONE (renamed) | `c882b5ea` — implemented as `AxeRunner.cs` (functional equivalent: `RunAxeAsync(markup, page, options)`); plan name `BunitToAxeBridge.cs` not in repo. |
| 1.5 | Sunfish-specific assertions | DONE | `da0245cc` (SunfishA11yAssertions.cs + SunfishA11yContract.cs + AssertionTests.cs) |
| 1.6 | Contract reader + Node-side export | DONE | `73bcd278` (ContractReader.cs + `packages/ui-core/scripts/export-a11y-contracts.mts`) |
| 1.7 | Pilot 36-scenario matrix | DONE-WITH-CAVEAT | `28663d78` (`PilotMatrixTests.cs`) — 36 fixture scenarios green at ~7 s wall; uses Task-1.3 fixtures, real Razor pilots not yet authored (documented in commit message + bridge-ready report). |
| 1.8 | Bridge-ready report + verdict | DONE | `28663d78` — verdict BRIDGE-READY at `waves/global-ux/week-2-bridge-ready-report.md`; ADR 0034 Option B not invoked. |
| 2.1 | `@storybook/test-runner` postVisit hook | DONE | `fdee0e25` (`packages/ui-core/.storybook/test-runner.ts` — axe + focus.initial / focus.trap / keyboardMap / directionalIcons assertions; pilots smoke-gate). |
| 2.2 | Matrix decorators (theme × light/dark × LTR/RTL) | PARTIAL | `62d8dd69` lands a11y addon + RTL toggle in `.storybook/main.ts`; `b63621d4` adds reduced-motion toolbar. No commit explicitly adds 3-theme × light/dark × ltr/rtl decorator matrix per spec; `preview.ts` exists but full 12-variant matrix not evidenced post-2026-04-23. |
| 2.3 | Test-helper assertions (`expectFocusTrapped`, `expectKeyboardMap`, `expectIconMirroredInRtl`) | NOT-STARTED | No files at `packages/ui-core/src/test-helpers/` (Glob returns empty). Plan 4 inlined the helpers into `test-runner.ts` (`fdee0e25`) but the standalone, exportable test-helpers barrel mandated by the plan does not exist. |
| 2.4 | Production-axe runtime re-measurement | NOT-STARTED | No `waves/global-ux/week-2-production-axe-hook-report.md` file in repo. |
| 2.5 | CVD emulation via CDP | PARTIAL (Blazor side) | `28663d78` wires CDP CVD emulation (None / Deuteranopia / Protanopia) into the bridge matrix; no commit lands CVD into `ui-core/.storybook/test-runner.ts` `preVisit` hook. CVD palette work landed separately on syncstate (`c4d8e344`, `295f39bd`). |
| 2.6 | Production hook report | NOT-STARTED | No `week-2-production-axe-hook-report.md` in `waves/global-ux/`. |
| 3.1 | Inventory `ui-core` components | NOT-STARTED | No `waves/global-ux/week-3-cascade-inventory.md` found. |
| 3.2 | Cascade story authoring on `ui-core` | NOT-STARTED | Only the 3 Week-1 pilot stories exist (`cc37a9b8` / `91fbe41a` / `12b3c674` — all pre-2026-04-23 baseline). No new `*.stories.ts` commits since plan kickoff. |
| 3.3 | HARD-component workshop | NOT-STARTED | No commits; depends on 3.1 inventory. |
| 3.4 | Cascade to `ui-adapters-react` | NOT-STARTED | No commits touching `packages/ui-adapters-react` since 2026-04-23. No `.storybook/test-runner.ts`, no `src/test-helpers/`. |
| 3.5 | Cascade coverage report | NOT-STARTED | No `week-3-cascade-coverage-report.md`. |
| 4.1 | Screen-reader audit runbook | NOT-STARTED | No `waves/global-ux/a11y-screen-reader-runbook.md` found. |
| 4.2 | Reduced-motion variants + helper | PARTIAL | `b63621d4` lands the Storybook toolbar + `REDUCED-MOTION.md` authoring guide; commit explicitly notes test-runner doesn't enforce yet. `expectReducedMotionRespected.ts` helper not present. No per-component `*.reduced-motion.stories.ts` files. |
| 4.3 | `SUNFISH_A11Y_001` analyzer (Roslyn + ts-eslint) | NOT-STARTED | No `packages/analyzers/accessibility/` directory; no `tooling/eslint-plugin-sunfish-a11y/`. (Sibling i18n analyzer at `packages/analyzers/loc-comments/` exists but is Plan 2's `SUNFISH_I18N_001`, not this one.) |
| 4.4 | Three-adapter E2E integration test | NOT-STARTED | No `week-4-three-adapter-e2e-report.md`. |
| 4.5 | Integration report + Plan-5 go/no-go | NOT-STARTED | No `week-4-integration-report.md`. |

## Overall verdict

**YELLOW** — Workstream A (the highest-risk bUnit-to-axe bridge) shipped end-to-end and earned a BRIDGE-READY verdict. Workstream B's production `postVisit` hook also landed with real bug-finding power (caught dialog focus + syncstate contrast). However, the Week-3 cascade (Tasks 3.1–3.5), the Week-4 polish (4.1, 4.3–4.5), and several Week-2 sub-deliverables (2.3 standalone helpers, 2.4/2.6 runtime re-measurement reports, full 2.2 matrix decorators) are **not started**. Story coverage is still 3 of ~40 components — far below the ≥35-component cascade target. With the 2026-05-24 kill-trigger 29 days out and the cascade not yet begun, this is materially at-risk but not RED: the highest-risk item is de-risked, and the remaining work is mostly authoring-volume rather than novel engineering.

## Notable observations

- The bridge project lives at `packages/ui-adapters-blazor-a11y/` (hyphen) rather than `packages/ui-adapters-blazor.A11y/` (dot) as specified in the plan — minor path drift, not a substantive deviation.
- Task 1.4's `BunitToAxeBridge.cs` was implemented as `AxeRunner.cs`. Functional equivalence is fine, but plan-name traceability is lost.
- The production test-runner hook (`fdee0e25`) inlines focus/keyboard/icon assertions rather than importing from `src/test-helpers/`. This works for `ui-core` but blocks the React adapter's plan to re-export helpers (Task 3.4) — a refactor will be needed before cascade.
- Real pilot bugs caught by the harness (dialog auto-focus, syncstate contrast at serious impact) were fixed in `729e9c39`; this is a real proof-of-value for the harness investment.
- ADR 0034 Amendment 1 landed (`bc5020f4`) wiring the Node→.NET contract bridge, supporting Task 1.6's pipeline.
- Plan 4B (a separate stream — sensory cascade §2/§5/§6) made significant progress (`08e2e0cd`, `6cc20102`, `c4d8e344`, `b63621d4`, `5dcc1cf4`, `9a4fa4d9`) but is not part of Plan 4's task list.
- The kill trigger fires 2026-05-24; with the cascade unstarted, scoping discussion (named fallbacks: drop CVD-per-commit, defer reduced-motion, drop analyzer) may need to be held proactively.
