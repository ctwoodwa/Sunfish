# Plan 4B Status — UI Sensory Cascade (Phase 1 Weeks 3-6)

**Date:** 2026-04-25
**Source plan:** docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-3-6-ui-sensory-cascade-plan.md
**Reporter:** Wave 1 Subagent 1.D

## Per-task status

| Task | Description | Status | Evidence |
|---|---|---|---|
| §5.1 | CVD ΔE2000 audit on Week-1 palette (BINARY GATE) | DONE | `c4d8e344` feat(color-audit): Plan 4B Task §5.1 ΔE2000 + CVD audit; gate fired. Report at `waves/global-ux/week-2-cvd-palette-audit.md` (filename uses week-2 prefix) |
| §5.1a | Palette rework (only if §5.1 fails) | DONE | `295f39bd` fix(syncstate): adopt Tol vibrant palette (CVD audit iteration 4) — palette reworked to Tol vibrant after gate fired |
| §2.1 | Logical-property inventory + browserslist check | DONE | `ea4f076d` feat(css-audit): Plan 4B §2 — CSS logical-properties audit script + inventory. Inventory at `waves/global-ux/week-2-css-logical-audit.md` and `css-logical-audit-2026-04-25.json` |
| §2.2 | Codemod dry-run on 3 Week-1 pilots | IN-PROGRESS | `ea4f076d` adds audit script under `packages/ui-core/scripts/` (`export-a11y-contracts.mts` present; no `logical-property-sweep.mjs` codemod runner found). Sweep was performed in §2.3 commits but no separate dry-run script committed |
| §2.3 | Cascade logical-property sweep across `ui-core` | DONE | `6cc20102` refactor(css): Plan 4B §2 cascade — 6 high-density files cleared (60→0 findings); `08e2e0cd` refactor(css): Plan 4B §2 cascade — full sweep, audit now CLEAN (173→0) |
| §2.4 | RTL screenshot-diff smoke on full `ui-core` inventory | NOT-STARTED | no commits found touching `packages/ui-core/scripts/rtl-screenshot-diff.mjs` or `waves/global-ux/week-5-rtl-regression-report.md` |
| §5.2 | Cascade multimodal encoding to SyncState-surfacing components | IN-PROGRESS | `5dcc1cf4` feat(syncstate): Plan 4B §5 — FreshnessBadge + NodeHealthBar contract conformance; `9a4fa4d9` feat(syncstate): Plan 4B §5 — SunfishSyncStatusIndicator adopts ADR 0036 contract. Note: only `syncstate/`, `button/`, `dialog/` directories exist under `src/components/`; nominal `syncstate-badge`, `dashboard-sync-widget`, `toolbar-sync-pill`, `list-item-sync-dot` web-component dirs not present (cascade landed against .NET/Razor surfaces FreshnessBadge + NodeHealthBar instead) |
| §5.3 | Dark-mode palette + re-audit | NOT-STARTED | no commits found touching `packages/ui-core/src/styles/tokens.css` or `waves/global-ux/week-4-dark-mode-cvd-audit.md`; `src/styles/` directory does not exist |
| §2.5 | Directional icons — Lit directive + registry | NOT-STARTED | no commits found touching `packages/ui-core/src/directives/mirrored-icon.ts` or `.storybook/directional-icons.json`; `src/directives/` directory does not exist |
| §6.1 | Central motion tokens (`motion.css`) | NOT-STARTED | no commits found touching `packages/ui-core/src/styles/motion.css`; `src/styles/` directory not present |
| §6.2 | Motion inventory + per-component reduced-motion branch | NOT-STARTED | no commits found touching component `*.styles.ts` files for reduced-motion CSS branches; only authoring guide landed (see §6.3) |
| §6.3 | Reduced-motion harness hookup (preview.ts + test-runner.ts) | DONE | `b63621d4` feat(ui-core): Plan 4B §6 — reduced-motion Storybook toolbar + authoring guide. Files present: `.storybook/preview.ts`, `.storybook/test-runner.ts`, `.storybook/REDUCED-MOTION.md` |
| §6.4 | `expectReducedMotionRespected` assertion | NOT-STARTED | no commits found touching `packages/ui-core/src/test-helpers/expectReducedMotionRespected.ts`; `src/test-helpers/` directory does not exist |
| §6.5 | Reduced-motion stories per animated component | NOT-STARTED | no commits found touching `*.reduced-motion.stories.ts`; only the toolbar/authoring guide from §6.3 |
| §6.6 | Reduced-motion audit report | NOT-STARTED | no commits found touching `waves/global-ux/week-5-reduced-motion-audit-report.md` |
| §5.4 | SyncState SR audit | NOT-STARTED | no commits found touching `waves/global-ux/a11y-screen-reader-runbook.md` for SyncState section |
| §5.5 | `expectMultimodalEncoding` assertion | NOT-STARTED | no commits found touching `packages/ui-core/src/test-helpers/expectMultimodalEncoding.ts` |
| §5.6 | ADR 0036 — SyncState multimodal encoding | DONE | `449d8bfb` feat(i18n): locales.json + coordinators roster + ADR 0036 syncstate contract. File present at `docs/adrs/0036-syncstate-multimodal-encoding-contract.md` (filename adds `-contract` suffix vs plan) |
| §5.7 | SyncState cascade report | NOT-STARTED | no commits found touching `waves/global-ux/week-5-syncstate-cascade-report.md` |
| §2.6 | `stylelint-use-logical` wiring + runtime helper | IN-PROGRESS | `ea4f076d` + `5fac9571` chore(scripts): wire pnpm audit:css-logical scripts established a custom audit pipeline; no `.stylelintrc.json` and no `expectLogicalPropertiesOnly.ts` test-helper found. Spec calls for stylelint-plugin-based enforcement; project chose custom audit script instead |
| §2.7 | Logical-property sweep report | IN-PROGRESS | `waves/global-ux/week-2-css-logical-audit.md` exists from `ea4f076d`; no `week-4-logical-property-sweep-report.md` filed under nominal name |
| §2.8 | RTL regression finalization | NOT-STARTED | no commits found touching `waves/global-ux/week-5-rtl-regression-report.md` |
| §7.1 | Integrate variants into Plan 4's matrix | NOT-STARTED | no commits found wiring `prefers-reduced-motion` axis into matrix iteration in `.storybook/test-runner.ts` for animated-tagged stories |
| §7.2 | Week-6 matrix integration report + Plan 5 handoff | NOT-STARTED | no commits found touching `waves/global-ux/week-6-matrix-integration-report.md`; Plan 5 CI gate workflow `9f1b71e7 ci(global-ux): Plan 5 — Global-UX gate workflow + branch-protection JSON` did land but does not constitute the matrix-integration handoff |

## Overall verdict

**YELLOW** — Workstream §5 (CVD gate, palette rework, ADR 0036, partial cascade) and Workstream §2 cascade (full CSS audit clean, 173→0 findings) are landed and provably green. Workstream §6 has only the harness scaffolding (`b63621d4` toolbar + authoring guide) and is missing the core cascade work: motion tokens, per-component reduced-motion branches, the `expectReducedMotionRespected` assertion, per-component stories, and the audit report. Workstream §2 RTL screenshot-diff (§2.4 / §2.8) is entirely absent. Dark-mode palette (§5.3) and directional-icons directive (§2.5) are NOT-STARTED. Week-6 matrix integration (§7.1, §7.2) has not begun. Roughly 8 DONE + 3 IN-PROGRESS + 12 NOT-STARTED across 23 tasks — execution is on the early/mid side of the Week 3-6 window. Status warrants YELLOW (active, on-track gates green, but most cascade work outstanding) rather than RED (no critical-path failure detected; CVD gate passed and CSS sweep clean removes the two highest-risk items).

## Notable observations

- Plan 4B covers ui-core sensory cascade (§2 logical-properties + §5 SyncState multimodal + §6 reduced-motion). CVD gate fired (§5.1) and palette was reworked (§5.1a) — Tol vibrant adopted on iteration 4 per `295f39bd`.
- ADR 0036 landed (`449d8bfb`) under filename `docs/adrs/0036-syncstate-multimodal-encoding-contract.md` (plan called for `0036-syncstate-multimodal-encoding.md` — filename has `-contract` suffix).
- CSS logical-property cascade fully landed: `6cc20102` (60→0 in 6 high-density files) then `08e2e0cd` (full sweep, 173→0 findings) — the largest concrete deliverable.
- SyncState §5.2 cascade landed against .NET surfaces (`SunfishSyncStatusIndicator`, `FreshnessBadge`, `NodeHealthBar`) per `9a4fa4d9` + `5dcc1cf4`. The plan's nominal Lit-component targets (`syncstate-badge`, `dashboard-sync-widget`, `toolbar-sync-pill`, `list-item-sync-dot` directories) do not exist under `packages/ui-core/src/components/`; only `button/`, `dialog/`, `syncstate/` are present. Cascade is real but against a different surface set than the plan's file structure assumed.
- Reduced-motion §6.3 harness hookup landed (`b63621d4`) — toolbar global + authoring guide at `packages/ui-core/.storybook/REDUCED-MOTION.md` — but per-component cascade (§6.1, §6.2, §6.4, §6.5, §6.6) all NOT-STARTED.
- The plan's Week-3 CVD report file at `waves/global-ux/week-3-cvd-delta-e-audit.md` does not exist; the report is at `waves/global-ux/week-2-cvd-palette-audit.md`. Similarly the plan's `week-4-logical-property-sweep-report.md` is absent — `week-2-css-logical-audit.md` covers the inventory only.
- ADR 0036 is the Plan 4B follow-up flagged in `waves/global-ux/status.md`; it has landed.
- Stylelint plugin (§2.6) was substituted for a custom audit script (`audit:css-logical` pnpm scripts via `5fac9571`); no `.stylelintrc.json` or `stylelint-use-logical` plugin in the tree. This is a contract divergence to flag for Plan 5 CI gates.
- Plan 5 CI gate scaffold (`9f1b71e7`) landed ahead of Plan 4B completion — Plan 5 has the css-logical and CVD audits as required checks but no reduced-motion or RTL-regression checks yet (consistent with §6/§2.4 still being open).

**File written:** `C:\Projects\sunfish\waves\global-ux\week-3-plan-4b-status.md`
**Verdict:** YELLOW
