# Global-First UX — Phase 1 Weeks 3-6 UI Sensory Cascade

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the three `ui-core`-level sensory invariants mandated by the global-UX spec — §2 CSS logical-properties sweep (directional layout that survives `dir=rtl`), §5 SyncState multimodal encoding (color + shape + label + ARIA role, CVD-safe), §6 `prefers-reduced-motion` audit (instant fallback + ARIA live announcement) — across every `ui-core` component, and wire the resulting variants into the matrix Storybook harness that Plan 4 stands up at Week 4.

**Architecture:** Three independent workstreams running in parallel Weeks 3-5, converging into the full-matrix harness at Week 6. Workstream §2 (CSS logical properties) is a mechanical sweep of `ui-core` stylesheets with a codemod-first approach; Workstream §5 (SyncState multimodal cascade) extends the Week-1 pilot encoding to every surface that renders sync-state; Workstream §6 (reduced-motion audit) identifies every animated/transitioned element and adds the `prefers-reduced-motion: reduce` branch + ARIA live announcement. Week 6 integrates each workstream's variants into Plan 4's 36-scenario matrix, producing a ~72-scenario per-component harness (matrix × LTR/RTL × reduced-motion on/off for animated components).

**Tech stack:** Lit 3.2 (Web Components in `ui-core`), CSS logical properties (Baseline 2023), `@axe-core/playwright` color-contrast rule, `@csstools/postcss-logical` for automated rewriting-with-fallbacks (disabled once Baseline hits — this is a one-shot codemod, not a runtime shim), Chrome DevTools Protocol `Emulation.setEmulatedMedia` for reduced-motion simulation, `Emulation.setEmulatedVisionDeficiency` for CVD simulation, Delta-E 2000 (CIE ΔE2000) color-distance audit via `culori` npm package or equivalent, Playwright screenshot diffing for RTL regression, Storybook 8 toolbar globals for LTR/RTL and reduced-motion preview.

**Scope boundary:** This plan covers Phase 1 Weeks 3-6 ONLY (~20 business days, overlapping the tail of Plans 2/3 and the front of Plan 4). It does NOT cover:
- The Storybook a11y harness itself (production `postVisit` hook, matrix decorators, bUnit-to-axe bridge, `SUNFISH_A11Y_001` analyzer) — that is Plan 4.
- Localization of SyncState label strings into the 12 target locales — Plan 2 cascade touches the `.resx` entries; Plan 4B only guarantees the label slot exists with an English default and a translator comment.
- CI gate enforcement (required-status-check wiring, `stylelint-use-logical` enforcement rule, reduced-motion-coverage lint) — Plan 5 owns CI.
- Cascade of these invariants into `blocks-*`, `accelerators/anchor`, `accelerators/bridge`, or `apps/kitchen-sink` consumer code — Plan 6 Phase 2 owns downstream cascade. Plan 4B ends at the `ui-core` boundary.
- `ui-adapters-react` and `ui-adapters-blazor` surface — these adapters wrap the same Web Components, so the logical-property sweep in `ui-core` CSS is the fix; the adapters inherit. Any adapter-specific stylesheets (expected: rare) are flagged in Task §2.1 inventory and handled by the sweep.

**Parent spec:** [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../specs/2026-04-24-global-first-ux-design.md) §2 (lines 116-170), §5 (lines 441-561), §6 (lines 561-639)
**Predecessor plan:** [`2026-04-24-global-first-ux-phase-1-week-1-plan.md`](./2026-04-24-global-first-ux-phase-1-week-1-plan.md) — Week-1 SyncState pilot encoding at `packages/ui-core/src/components/syncstate/`
**Parallel plans (no file overlap):** [Plan 2 loc-infra](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md), [Plan 3 translator-assist], [Plan 4 a11y foundation](./2026-04-24-global-first-ux-phase-1-weeks-2-4-a11y-foundation-plan.md), [Plan 5 CI gates], [Plan 6 Phase-2 cascade]
**Depends on:** Plan 4's production `postVisit` hook + matrix decorators (Task 2.1, 2.2) must land by end of Week 4 for Week-6 integration. Plan 4B's §2/§5/§6 variants ride those decorators.
**Feeds:** Plan 5 CI gates — `stylelint-use-logical` enforcement, `prefers-reduced-motion` coverage metric, CVD ΔE2000 adjacency-threshold gate all become required status checks once Plan 4B lands.
**Follows into:** Plan 6 Phase 2 cascade — the `ui-core` invariants become the contract that `blocks-*` and apps must honor when they consume components.

---

## Context & Why

Week 1 landed one pilot component, `sunfish-syncstate-indicator`, with the multimodal encoding (color + shape + label + ARIA role) and proved the ΔE2000 CVD-safety audit in isolation. The spec mandates that encoding cascade to every Sunfish component that surfaces sync state, and further mandates that the entire `ui-core` palette of components respect three sensory invariants: (a) layout directionality via CSS logical properties so Arabic/Hebrew RTL works without per-locale overrides; (b) multimodal state encoding so CVD users, screen-reader users, and sighted users all get the signal; (c) reduced-motion respect so vestibular-disorder users get instant state changes with ARIA live announcements instead of animated transitions.

These three invariants are sensory-layer concerns that don't fit cleanly into any one Plan 2-6 workstream. Plan 2 (loc-infra) handles strings, not layout. Plan 4 (a11y foundation) builds the harness that enforces contracts but doesn't author the component-level sensory code. Plan 5 (CI gates) enforces but doesn't author. Plan 6 (Phase 2) cascades downstream. Plan 4B closes the gap between Plan 4's harness and Plan 6's downstream cascade — it is the `ui-core` implementation work that makes the three invariants real at the component level.

The three workstreams are fully parallelizable because they touch orthogonal file axes: §2 touches CSS files (`*.styles.ts`, `*.css`), §5 touches component `.ts` files and their stories (only for components that surface sync state — a subset), §6 touches component `.ts` + CSS (only for components with motion — a subset). No file is in the critical path of more than one workstream; subagent dispatch can fan out wide.

The CVD ΔE2000 audit is the highest-risk item. If the Week-1 pilot palette (Healthy #27ae60, Stale #3498db, Offline #7f8c8d, ConflictPending #e67e22, Quarantine #c0392b) fails its ΔE2000 adjacency threshold under one of the three CVD simulations, the palette must change — and that change ripples into every SyncState-surfacing component. The audit is scheduled as a Week-3 binary gate so that a palette rework, if needed, happens before cascade rather than during.

---

## Success Criteria

### PASSED — proceed to Plan 4 matrix integration at Week 6

- Every `ui-core` component stylesheet has been swept: zero physical-axis properties (`margin-left`, `padding-right`, `border-top-left-radius`, `left`/`right` positioning, `text-align: left|right`, `float: left|right`) remain except where explicitly allow-listed for non-directional reasons (documented per-case). `stylelint` run with `stylelint-use-logical` extension reports zero violations.
- Every `ui-core` component that surfaces sync state (inventory: syncstate-indicator, syncstate-badge, dashboard-sync-widget, toolbar-sync-pill, list-item-sync-dot, and any added during Plan 4 cascade — target ~6-8 components) implements the multimodal encoding per the Week-1 pilot contract: color + shape (icon) + text label + ARIA role (`status` for healthy/stale/offline; `alert` for conflict/quarantine).
- CVD ΔE2000 audit passes: every pair of adjacent states in the five-state palette (Healthy/Stale/Offline/ConflictPending/Quarantine) has ΔE2000 ≥ 25 under each of {deuteranopia, protanopia, tritanopia} simulations. Report published at `waves/global-ux/week-3-cvd-delta-e-audit.md`.
- Every `ui-core` component that declares a CSS `transition` or `animation` property has a `prefers-reduced-motion: reduce` branch that either sets `animation: none; transition: none` or reduces duration to ≤ 10 ms. Components with state-change animations also dispatch an ARIA live-region announcement on the change so the reduced-motion user still gets the signal.
- Dark-mode variants exist for the five-state SyncState palette and pass the same CVD ΔE2000 gate as light-mode.
- RTL regression: Playwright screenshot diff of every `ui-core` component under `dir=ltr` vs `dir=rtl` produces only expected mirror diffs (no broken layouts, no overflow, no clipped content). Diff results logged to `waves/global-ux/week-5-rtl-regression-report.md`.
- Week-6 integration: Plan 4's matrix harness consumes the new variants without modification. New scenario count per component: 72 (base 36 × 2 for reduced-motion-on/off on animated components). Full-matrix p95 wall time remains within Plan 4's 15-min CI budget (Task §6.3 re-measurement).

### FAILED — triggers a scope cut, not a Phase 1 abort

- CVD ΔE2000 audit fails for the Week-1 palette: the palette must change. Named fallback is a Week-3 palette-rework task (add +1 day to Week-3 timeline, re-seed the five hex codes, re-run the audit). If no palette achieving adjacency ≥ 25 under all three CVD modes can be found within 2 iterations, escalate to BDFL for a scope-cut decision — named options are (a) accept adjacency ≥ 20 with a shape-cue reinforcement; (b) drop tritanopia from the per-commit gate (rarest CVD; move to quarterly audit); (c) move to a 4-state palette by merging ConflictPending and Quarantine into a single "NeedsAttention" state.
- `stylelint-use-logical` reports violations that are intentional (e.g., truly non-directional `text-align: center` in a centered layout); codify as explicit allow-list entries in `.stylelintrc` with a comment citing Plan 4B; total allow-list size > 10 entries indicates the sweep is not deep enough — rerun with stricter codemod.
- Reduced-motion inventory reveals > 15 components with motion (Plan assumed ~8-12). Scope-cut: cascade reduced-motion variants to the 8 highest-traffic components in Week 5; defer the remaining components to Plan 6 Phase 2 with a debt entry in `waves/global-ux/a11y-debt-register.md`.
- RTL regression surfaces > 5 components with broken layouts that require component-source changes beyond CSS (e.g., imperative positioning in JS logic). Document as HARD-RTL-deferred list; Plan 6 picks up. Week-5 declared partial-success if ≥ 35 of ~40 `ui-core` components clean-RTL.
- Week-6 matrix integration breaks Plan 4's CI budget (p95 > 15 min on 4-shard). Fallback per Plan 4's spec §7: expand to 8-shard or move reduced-motion-on/off axis to nightly (halving per-commit scenario count).

### Kill trigger (30-day timeout)

If Plan 4B has not landed all PASSED criteria by **2026-06-04** (30 business days from Week-3 start on 2026-04-24), escalate to BDFL for scope cut: named options are (a) ship §2 (logical properties) + §6 (reduced motion) only; defer §5 (SyncState cascade) to Plan 6 — the Week-1 pilot component alone satisfies the spec floor; (b) ship §2 + §5, defer §6 to Plan 6 with a `SUNFISH_A11Y_REDUCED_MOTION_DEBT` entry in the debt register; (c) accept 4-shard + CVD-nightly fallback permanently.

---

## Assumptions & Validation

| Assumption | VALIDATE BY | IMPACT IF WRONG |
|---|---|---|
| CSS logical properties (`margin-inline-start`, `padding-block-end`, `border-start-start-radius`, etc.) are supported in all Sunfish target browsers without polyfill | Task §2.1 — browserslist check against `packages/ui-core/.browserslistrc`; spot-check MDN Baseline status | If a target browser lacks support (unlikely — Baseline 2023), add `@csstools/postcss-logical` as a build-time polyfill emitting physical fallbacks alongside logical properties |
| The Week-1 pilot palette (Healthy #27ae60, Stale #3498db, Offline #7f8c8d, ConflictPending #e67e22, Quarantine #c0392b) passes the ΔE2000 ≥ 25 adjacency threshold under all three CVD modes | Task §5.1 — CVD ΔE2000 audit script run on Week-3 day 1 | Palette rework — add +1 day to Week 3; re-seed hex codes; re-audit; if 2nd attempt fails, invoke BDFL scope-cut options above |
| `@csstools/postcss-logical` codemod produces correct rewrites for every Sunfish CSS pattern (including `border-*-*-radius`, `inset-inline`, `padding-block`) | Task §2.2 — run codemod on 3 Week-1 pilot components, hand-review diffs for correctness | If codemod miscompiles an edge case (e.g., `background-position: left 10px top` → ambiguous logical form), fall back to manual rewrite for the affected component, document the pattern in `decisions.md` |
| Every animated `ui-core` component can respect `prefers-reduced-motion` via CSS alone (no JS changes needed) | Task §6.2 — inventory pass over `ui-core` motion; identify any JS-driven animations (e.g., FLIP reorder, canvas draw loop) | If JS-driven animation exists, wrap the animation driver with a `matchMedia('(prefers-reduced-motion: reduce)').matches` check; add an ARIA live announcement to fill the sensory gap |
| Playwright screenshot diffing under `dir=rtl` produces stable images across runs (anti-aliasing, font rendering) | Task §2.4 — 3 pilot components × 10 runs each; hash the PNG bytes; tolerate ≤ 2% pixel diff via `pixelmatch` | If diffs flake, switch from pixel-diff to DOM-structural-diff: compare `getBoundingClientRect` of key descendants under LTR vs RTL; assert symmetric mirror |
| Chrome DevTools Protocol `Emulation.setEmulatedMedia({ features: [{name:'prefers-reduced-motion', value:'reduce'}] })` works reliably in Playwright 1.59.1 chromium | Task §6.3 — smoke test on 3 animated pilot components | If unreliable, fall back to setting `data-motion="reduced"` on `<html>` via page.evaluate before each story render; assertion reads that attribute instead of the media feature |
| ARIA live-region announcements (`role="status"` for non-urgent, `role="alert"` for urgent) are correctly surfaced by NVDA-2026.1 and VoiceOver-macOS15 when a SyncState badge transitions from Healthy → ConflictPending | Task §5.4 — screen-reader audit on the Week-1 pilot under both SR/browser pairings; include in Plan 4's SR runbook | If SR fails to announce, try `aria-live="polite"` / `aria-live="assertive"` attributes directly instead of role semantics; update the encoding contract |
| The 5-state palette survives when rendered in dark mode with the same ΔE2000 ≥ 25 adjacency threshold | Task §5.3 — re-run the ΔE2000 audit under dark-mode hex variants | Dark-mode palette rework; likely independent of light-mode palette choices, but doubles the audit surface |

---

## File Structure (Weeks 3-6 deliverables)

```
packages/ui-core/
  src/
    styles/
      tokens.css                                              ← Add light + dark SyncState palette tokens (Task §5.3)
      motion.css                                              ← Central motion tokens + reduced-motion overrides (Task §6.1)
    components/<each-of-~40-components>/
      <component>.styles.ts                                   ← Swept for logical properties (Task §2.3)
      <component>.ts                                          ← Reduced-motion branches for animated components (Task §6.2)
      <component>.reduced-motion.stories.ts                   ← Plan 4 Task 4.2 already schedules; Plan 4B authors content (Task §6.5)
    components/syncstate-indicator/                           ← Week-1 pilot, reused as template
    components/syncstate-badge/                               ← NEW or cascaded-from-pilot (Task §5.2)
    components/dashboard-sync-widget/                         ← NEW or cascaded-from-pilot (Task §5.2)
    components/toolbar-sync-pill/                             ← NEW or cascaded-from-pilot (Task §5.2)
    components/list-item-sync-dot/                            ← NEW or cascaded-from-pilot (Task §5.2)
    directives/
      mirrored-icon.ts                                        ← Lit directive for RTL icon mirroring (Task §2.5)
  scripts/
    cvd-delta-e-audit.mjs                                     ← CVD ΔE2000 audit script (Task §5.1)
    logical-property-sweep.mjs                                ← Codemod runner (Task §2.2)
  .stylelintrc.json                                           ← stylelint-use-logical extension wired (Task §2.6)
  test-helpers/
    expectLogicalPropertiesOnly.ts                            ← Linter-style runtime assertion (Task §2.6)
    expectMultimodalEncoding.ts                               ← SyncState contract assertion (Task §5.5)
    expectReducedMotionRespected.ts                           ← Plan 4 already schedules; Plan 4B uses (Task §6.4)

packages/ui-core/.storybook/
  preview.ts                                                  ← Add reduced-motion + LTR/RTL global toolbars (Task §6.5)
  directional-icons.json                                      ← Registry of mirror-vs-no-mirror icon rulings (Task §2.5)

docs/superpowers/specs/
  2026-04-24-global-first-ux-design.md                        ← Referenced, not modified

docs/adrs/
  0036-syncstate-multimodal-encoding.md                       ← NEW ADR for the 5-state palette + encoding contract (Task §5.6)

waves/global-ux/
  week-3-cvd-delta-e-audit.md                                 ← Task §5.1 output (binary gate)
  week-3-palette-rework-decision.md                           ← Only if §5.1 fails (decision log)
  week-4-logical-property-sweep-report.md                     ← Task §2.7 output
  week-5-syncstate-cascade-report.md                          ← Task §5.7 output
  week-5-reduced-motion-audit-report.md                       ← Task §6.6 output
  week-5-rtl-regression-report.md                             ← Task §2.8 output
  week-6-matrix-integration-report.md                         ← Task §7.2 output
```

---

## Week 3 — Workstream §5 Gate + Workstream §2 Kickoff

### Task §5.1: CVD ΔE2000 audit on Week-1 palette (BINARY GATE)

**Files:**
- Create: `packages/ui-core/scripts/cvd-delta-e-audit.mjs`
- Create: `waves/global-ux/week-3-cvd-delta-e-audit.md`

**Why:** The Week-1 pilot palette is unverified under the spec's adjacency threshold. If it fails, every downstream §5 cascade task is blocked until rework lands. Running the audit first-thing in Week 3 means a palette rework costs ≤ 1 day, not ≤ 1 week.

- [ ] **Step 1:** Author the audit script using `culori` npm package (or `chroma-js` as fallback). Inputs: the five hex codes from Week 1 (Healthy #27ae60, Stale #3498db, Offline #7f8c8d, ConflictPending #e67e22, Quarantine #c0392b). For each pair of the 10 adjacent-pairs (C(5,2)), compute ΔE2000 in Lab space.
- [ ] **Step 2:** Simulate each color under {deuteranopia, protanopia, tritanopia} using a CVD transformation matrix (culori's `differenceEuclidean` with CVD-adjusted space, or Brettel/Viénot/Mollon model). For each CVD mode, recompute all 10 pairwise ΔE2000 values.
- [ ] **Step 3:** Assert every pairwise ΔE2000 ≥ 25 under every CVD mode. If all pass, write `week-3-cvd-delta-e-audit.md` with PASS verdict + the 40-cell table (10 pairs × 4 modes = normal + 3 CVD). If any fail, write FAIL verdict with the specific failing pair(s) and CVD mode(s); block all Workstream §5 tasks; invoke Task §5.1a palette rework.
- [ ] **Step 4:** Audit script exit code: 0 on pass, non-zero on fail. Wired as a `package.json` script: `pnpm --filter @sunfish/ui-core audit:cvd`.

#### Task §5.1a: Palette rework (only if §5.1 fails)

**Files:**
- Modify: `packages/ui-core/src/styles/tokens.css` (the five SyncState CSS custom properties)
- Create: `waves/global-ux/week-3-palette-rework-decision.md`

- [ ] **Step 1:** Propose 2-3 replacement hex combinations. For each, run the audit. Record every combination tried and its pass/fail outcome.
- [ ] **Step 2:** If a combination passes, adopt it; update tokens.css; re-run audit; move to Task §5.2.
- [ ] **Step 3:** If 2 combinations fail, escalate to BDFL (scope-cut options: accept ΔE ≥ 20 with shape-cue reinforcement; drop tritanopia per-commit gate to quarterly; merge to 4-state palette). Log the scope cut in `decisions.md`.

### Task §2.1: Logical-property inventory + browserslist check

**Files:**
- Create: `waves/global-ux/week-3-logical-properties-inventory.md`

**Why:** Before the codemod runs, confirm the target browserset supports logical properties natively (no polyfill needed) and enumerate every physical-axis property in `ui-core` stylesheets.

- [ ] **Step 1:** Read `packages/ui-core/.browserslistrc`. Cross-reference against MDN Baseline: logical properties in CSS are Baseline 2023; all evergreen browsers support. If Sunfish target list includes pre-2023 IE/Edge Legacy (unlikely), add polyfill plan to `decisions.md`.
- [ ] **Step 2:** Enumerate physical-axis properties currently in use. Patterns: `margin-left|right|top|bottom`, `padding-left|right|top|bottom`, `border-top|bottom-left|right-radius`, `left|right|top|bottom` positioning, `text-align: left|right`, `float: left|right`, `background-position: left|right`, `transform-origin: left|right`, `inset` + any directional variants. For each, count occurrences across `packages/ui-core/src/components/*/`.
- [ ] **Step 3:** Produce inventory table: pattern, occurrence count, containing components. Any pattern with occurrence > 50 becomes a codemod-mandatory target; patterns with occurrence < 5 are manual-review candidates.

### Task §2.2: Codemod dry-run on 3 Week-1 pilots

**Files:**
- Create: `packages/ui-core/scripts/logical-property-sweep.mjs`

**Why:** Validate the codemod assumption before cascading it across ~40 components.

- [ ] **Step 1:** Script wraps `@csstools/postcss-logical` in a node CLI. Input: glob of `*.styles.ts` files; output: rewritten files in-place.
- [ ] **Step 2:** Dry-run on `sunfish-button`, `sunfish-dialog`, `sunfish-syncstate-indicator` (the Week-1 pilots). Capture before/after diffs.
- [ ] **Step 3:** Hand-review diffs. Look for: correct `margin-left` → `margin-inline-start`, correct `border-top-left-radius` → `border-start-start-radius`, correct `text-align: left` → `text-align: start`, no accidental changes to center/justify values, no regressions on `float` (flag as manual-rewrite — modern layouts shouldn't float anyway).
- [ ] **Step 4:** If codemod is clean, promote to Task §2.3 cascade. If dirty, document failing patterns and switch those to manual rewrites.

---

## Week 4 — Workstream §2 Cascade + Workstream §5 Cascade

### Task §2.3: Cascade logical-property sweep across `ui-core`

**Files:**
- Modify: every `packages/ui-core/src/components/<name>/<name>.styles.ts` (~40 files)

**Why:** This is the expensive Week-4 work for Workstream §2. Each component's stylesheet runs through the codemod; outputs hand-reviewed.

- [ ] **Step 1:** Dispatch subagents per the same cluster breakdown as Plan 4 Task 3.1 (primitives / overlays / forms / data-display / navigation) to keep Plan 4B and Plan 4 cluster-aligned. Each subagent runs the codemod on its cluster, hand-reviews diffs, commits with path-scoped `git add`.
- [ ] **Step 2:** For each component, post-codemod: run `pnpm --filter @sunfish/ui-core storybook` locally, switch to RTL via the Storybook toolbar global (Plan 4's Task 2.2 decorator), visually confirm the component mirrors correctly. No clipped content, no off-screen overflow, no broken gap/justify.
- [ ] **Step 3:** Reviewer-agent serial gate per cluster: verify (a) no physical-axis properties remain (stylelint run); (b) no regressions in LTR rendering (visual smoke via Storybook); (c) no changes outside `src/components/<cluster>/` or the central `src/styles/` tokens.

#### CSS-logical migration checklist (6+ before/after patterns)

The codemod targets these patterns. Any file in the sweep touching these must hit the right-hand-side form post-rewrite.

| # | Before (physical) | After (logical) | Notes |
|---|---|---|---|
| 1 | `margin-left: 8px; margin-right: 12px;` | `margin-inline-start: 8px; margin-inline-end: 12px;` | Standard case; `start`/`end` follow `dir` |
| 2 | `padding-left: 16px; padding-right: 16px;` | `padding-inline: 16px;` | Symmetric → shorthand |
| 3 | `padding-top: 4px; padding-bottom: 4px;` | `padding-block: 4px;` | Block axis, always vertical in `writing-mode: horizontal-tb` |
| 4 | `border-top-left-radius: 4px; border-top-right-radius: 4px;` | `border-start-start-radius: 4px; border-start-end-radius: 4px;` | Two-axis; `block-inline` naming |
| 5 | `text-align: left;` | `text-align: start;` | `left/right` → `start/end`; never translate `center`/`justify` |
| 6 | `left: 0; right: auto;` (absolute positioning) | `inset-inline-start: 0; inset-inline-end: auto;` | Positioning follows text direction |
| 7 | `float: left;` | manual rewrite — audit case-by-case | Rare in modern `ui-core`; flag for grid/flex refactor if found |
| 8 | `background-position: left 10px top 5px;` | `background-position-x: 10px; background-position-y: 5px;` (no logical equivalent) | No logical shorthand; document as allow-list if truly non-directional |
| 9 | `transform: translateX(-4px);` (icon shift) | use `mirrored-icon` Lit directive (Task §2.5); remove inline transform | Directional icons handled via directive |

### Task §2.4: RTL screenshot-diff smoke on full `ui-core` inventory

**Files:**
- Create: `packages/ui-core/scripts/rtl-screenshot-diff.mjs`
- Create: `waves/global-ux/week-5-rtl-regression-report.md` (populated incrementally; finalized Week 5)

**Why:** Catch RTL regressions that stylelint can't: broken layouts, overflow, clipping, missing mirror on directional icons.

- [ ] **Step 1:** Script launches Storybook, iterates every story, captures PNG under `dir=ltr` and `dir=rtl` (with CVD + theme held constant at defaults). Outputs to `artifacts/rtl-diff/<component>/<story>/{ltr,rtl}.png`.
- [ ] **Step 2:** For each LTR/RTL pair, assert structural mirror: use `pixelmatch` with horizontal-flip comparison (flip RTL image horizontally, compare to LTR; expect ≤ 2% diff for correctly mirrored components; > 5% flags asymmetric rendering).
- [ ] **Step 3:** Components failing the mirror assertion get an entry in `week-5-rtl-regression-report.md` with screenshots; triaged into (a) true bug (fix in Task §2.3 rework), (b) expected asymmetry (allow-list with justification), (c) HARD-RTL (defer to Plan 6).

### Task §5.2: Cascade multimodal encoding to SyncState-surfacing components

**Files:**
- Modify or Create: `packages/ui-core/src/components/syncstate-badge/`
- Modify or Create: `packages/ui-core/src/components/dashboard-sync-widget/`
- Modify or Create: `packages/ui-core/src/components/toolbar-sync-pill/`
- Modify or Create: `packages/ui-core/src/components/list-item-sync-dot/`

**Why:** The Week-1 pilot `sunfish-syncstate-indicator` proved the encoding. Every other component that surfaces sync state must implement the same contract.

- [ ] **Step 1:** Inventory SyncState-surfacing components. Baseline from spec §5: syncstate-indicator, syncstate-badge, dashboard-sync-widget, toolbar-sync-pill, list-item-sync-dot. Plan 4 Task 3.1 inventory may have added others — cross-check.
- [ ] **Step 2:** For each: implement the encoding contract — (a) color from the five-state CSS custom properties (`--sunfish-sync-healthy`, etc.); (b) shape via icon from the material-symbols set (Healthy=check_circle, Stale=schedule, Offline=cloud_off, ConflictPending=warning, Quarantine=block); (c) text label (English default, `<comment>` translator note referencing this ADR); (d) ARIA role (`status` for healthy/stale/offline, `alert` for conflict/quarantine).
- [ ] **Step 3:** Each component exports a `SyncState` Lit reactive property with union type `'healthy' | 'stale' | 'offline' | 'conflictPending' | 'quarantine'`. On state change, the host fires an ARIA live-region announcement via a shared utility at `src/test-helpers/announceStateChange.ts` (or equivalent `src/utils/`).
- [ ] **Step 4:** Per-cluster subagent dispatch; path-scoped commits; reviewer-agent gate.

### Task §5.3: Dark-mode palette + re-audit

**Files:**
- Modify: `packages/ui-core/src/styles/tokens.css` — add `@media (prefers-color-scheme: dark)` block with dark-mode hex variants
- Modify: `packages/ui-core/scripts/cvd-delta-e-audit.mjs` — add `--mode dark` flag
- Create: `waves/global-ux/week-4-dark-mode-cvd-audit.md`

- [ ] **Step 1:** Propose dark-mode hex variants. Starting point: lift luminance 20-30% on each state so contrast against dark backgrounds holds WCAG AA (4.5:1 for text, 3:1 for non-text). Example: Healthy #27ae60 (light) → #2ecc71 (dark).
- [ ] **Step 2:** Run CVD ΔE2000 audit on dark-mode palette. Same gate: every adjacent pair ≥ 25 under all three CVD modes.
- [ ] **Step 3:** If pass, commit. If fail, iterate per Task §5.1a. Document in `week-4-dark-mode-cvd-audit.md`.

### Task §2.5: Directional icons — Lit directive + registry

**Files:**
- Create: `packages/ui-core/src/directives/mirrored-icon.ts`
- Create: `packages/ui-core/.storybook/directional-icons.json`

**Why:** Not every icon mirrors under RTL. `call_split` does (arrow forks direction-aware). `check_circle` doesn't (circle is directionally neutral). Per-ADR decisions land in a registry; the directive reads the registry to decide.

- [ ] **Step 1:** Author `mirrored-icon.ts` Lit directive: wraps an icon element; in `dir=rtl` contexts, applies `transform: scaleX(-1)` iff the icon name is flagged as directional in `directional-icons.json`.
- [ ] **Step 2:** Seed `directional-icons.json` with ADR-reviewed rulings. Directional (mirror): `call_split`, `arrow_forward`, `arrow_back`, `chevron_right`, `chevron_left`, `keyboard_arrow_right`, `keyboard_arrow_left`, `undo`, `redo`, `reply`, `send`, `drive_file_move`. Non-directional (no mirror): `check_circle`, `warning`, `block`, `schedule`, `cloud_off`, `info`, `help`, `close`, `menu`, `more_vert`, `search`, `settings`.
- [ ] **Step 3:** Replace every ad-hoc `transform: translateX(-N)` pattern in `ui-core` components with the directive. Audit via grep.
- [ ] **Step 4:** Storybook story for the directive itself: render a 4-column × 2-row grid of directional-vs-non-directional icons under LTR/RTL; visual snapshot asserts correct mirroring.

---

## Week 5 — Workstream §6 Cascade + Workstream §2 Stragglers

### Task §6.1: Central motion tokens

**Files:**
- Create: `packages/ui-core/src/styles/motion.css`

**Why:** Every animated component should pull from a central set of motion tokens rather than hardcoding durations. Central tokens make the reduced-motion override one-file-edit, not forty-file-edit.

- [ ] **Step 1:** Define tokens: `--sunfish-motion-duration-fast: 150ms`, `--sunfish-motion-duration-default: 250ms`, `--sunfish-motion-duration-slow: 400ms`, `--sunfish-motion-easing-standard: cubic-bezier(0.2, 0, 0, 1)`, `--sunfish-motion-easing-emphasized: cubic-bezier(0.2, 0, 0, 1.4)`.
- [ ] **Step 2:** Add a `@media (prefers-reduced-motion: reduce)` block that overrides every duration to `0.01ms` (never `0` — some browsers treat `0` as "no transition event fires", breaking state machines that listen for `transitionend`). Keep easing untouched.
- [ ] **Step 3:** Import `motion.css` from the root styles barrel so every component inherits.

### Task §6.2: Motion inventory + per-component reduced-motion branch

**Files:**
- Modify: every `packages/ui-core/src/components/<name>/<name>.styles.ts` that declares `transition` or `animation` (~8-12 files expected)
- Modify: `waves/global-ux/week-5-reduced-motion-audit-report.md` (incremental)

**Why:** Even with central tokens, some components have bespoke motion (e.g., dialog entrance scale-up); each needs hand-review to ensure reduced-motion fallback is instant + correctly-announced.

- [ ] **Step 1:** Inventory: grep `ui-core/src/components/*/` for `transition:` and `animation:` declarations. Record each in the audit report.
- [ ] **Step 2:** For each, rewrite the CSS to reference `motion.css` tokens where possible. For bespoke motion (e.g., `@keyframes dialog-entrance`), wrap in `@media (prefers-reduced-motion: no-preference) { ... }` so reduced-motion path skips the keyframe entirely.
- [ ] **Step 3:** For any JS-driven animation (expected: rare; FLIP-reorder candidates), check `window.matchMedia('(prefers-reduced-motion: reduce)').matches` at the top of the driver; skip the animation; fire the terminal state change directly.
- [ ] **Step 4:** For every state change that used to be signalled by animation alone, add an ARIA live-region announcement (`role="status"` or `aria-live="polite"`) so reduced-motion users still get sensory confirmation.

### Task §6.3: Reduced-motion harness hookup

**Files:**
- Modify: `packages/ui-core/.storybook/preview.ts` — add `prefers-reduced-motion` global (Plan 4 Task 4.2 already schedules this decorator; Plan 4B content fills it out)
- Modify: `packages/ui-core/.storybook/test-runner.ts` — add `preVisit` hook that reads story's `parameters.globals.prefersReducedMotion` and calls CDP `Emulation.setEmulatedMedia` accordingly

**Why:** Integrates Plan 4B's variants into Plan 4's production harness. CDP emulation is the authoritative method.

- [ ] **Step 1:** In `preview.ts`, register a `globalTypes` entry: `prefersReducedMotion: { name: 'Reduced Motion', defaultValue: 'no-preference', toolbar: { items: ['no-preference', 'reduce'] } }`.
- [ ] **Step 2:** In `test-runner.ts` `preVisit`, read `context.globals.prefersReducedMotion`. Open a CDP session: `page.context().newCDPSession(page)`; call `Emulation.setEmulatedMedia({ features: [{ name: 'prefers-reduced-motion', value: 'reduce' | 'no-preference' }] })`.
- [ ] **Step 3:** Smoke test: run `pnpm --filter @sunfish/ui-core test:a11y` with a single story forced to reduced-motion; assert the story's `play` function sees `matchMedia('(prefers-reduced-motion: reduce)').matches === true`.

### Task §6.4: `expectReducedMotionRespected` assertion

**Files:**
- Create: `packages/ui-core/src/test-helpers/expectReducedMotionRespected.ts` (Plan 4 Task 4.2 already schedules the file; Plan 4B fills content since Plan 4 deferred it to this plan for the cascade)

**Why:** Runtime assertion that verifies an animated component under `prefers-reduced-motion: reduce` actually has no animation/transition active.

- [ ] **Step 1:** Signature: `expectReducedMotionRespected(locator: Locator): Promise<void>`.
- [ ] **Step 2:** Read `getComputedStyle` on the host + key descendants (per-component selectors passed as args). Assert `animationName === 'none'` OR `animationDuration <= 0.01s`; assert `transitionDuration <= 0.01s`.
- [ ] **Step 3:** Additionally assert that an ARIA live-region child (`[role="status"]`, `[role="alert"]`, or `[aria-live]`) exists within the component's tree, so the reduced-motion user isn't deprived of the signal.

### Task §6.5: Reduced-motion stories per animated component

**Files:**
- Create: `packages/ui-core/src/components/<animated-name>/<name>.reduced-motion.stories.ts` OR add `ReducedMotion` export to main stories (per animated component from §6.2 inventory)

**Why:** Plan 4 Task 4.2 plans these; Plan 4B authors content.

- [ ] **Step 1:** Each story sets `parameters.globals.prefersReducedMotion: 'reduce'`; exports a `play` function that invokes `expectReducedMotionRespected(...)`.
- [ ] **Step 2:** For components with state-change animations, the `play` function also triggers the state change (e.g., clicks the button that opens the dialog) and asserts the ARIA live-region announcement fires (listen for a custom event or inspect the live-region text content).
- [ ] **Step 3:** Subagent-cluster dispatch; path-scoped commits; reviewer-agent gate.

### Task §6.6: Reduced-motion audit report

**Files:**
- Create: `waves/global-ux/week-5-reduced-motion-audit-report.md`

- [ ] Cover: inventory of animated `ui-core` components, per-component reduced-motion implementation (CSS-only vs JS-check), ARIA live-region wiring verification, test coverage (story present + assertion green), any deferred components (HARD-motion list for Plan 6).

### Task §5.4: SyncState SR audit

**Files:**
- Modify: `waves/global-ux/a11y-screen-reader-runbook.md` (Plan 4 Task 4.1 already authors this runbook; Plan 4B adds SyncState section)

- [ ] **Step 1:** Re-audit `sunfish-syncstate-indicator` under NVDA-2026.1/Firefox-126 and VoiceOver-macOS15/Safari-17. Transition from Healthy → Stale → ConflictPending; record each announcement verbatim.
- [ ] **Step 2:** Confirm `role="status"` announcements are polite (don't interrupt); `role="alert"` for ConflictPending/Quarantine interrupt.
- [ ] **Step 3:** Update the story's `parameters.a11y.sunfish.screenReaderAudit` entry with fresh version strings + transcript hash.

### Task §5.5: `expectMultimodalEncoding` assertion

**Files:**
- Create: `packages/ui-core/src/test-helpers/expectMultimodalEncoding.ts`

**Why:** Codify the contract so every SyncState-surfacing component is tested identically.

- [ ] **Step 1:** Signature: `expectMultimodalEncoding(locator: Locator, state: SyncState): Promise<void>`.
- [ ] **Step 2:** Assertions: (a) color CSS custom property resolves to the expected token (`--sunfish-sync-healthy` etc., cross-checked against computed background or border); (b) an icon element exists with the expected material-symbols name; (c) a text-label element exists with non-empty localized text; (d) the host element has the expected ARIA role.
- [ ] **Step 3:** Used by every SyncState-surfacing component's `play` function.

### Task §5.6: ADR 0036 — SyncState multimodal encoding

**Files:**
- Create: `docs/adrs/0036-syncstate-multimodal-encoding.md`

**Why:** Durable record of the 5-state palette + contract so Plan 6 Phase 2 cascade reads it, not Plan 4B.

- [ ] Cover: decision (5-state palette + color/shape/label/role encoding), context (CVD safety, SR accessibility, no-color-alone), consequences (any component surfacing sync state must honor this; `blocks-*` using non-standard states must map back), alternatives considered (4-state merge, 7-state split), evidence (CVD ΔE2000 audit report link).

### Task §5.7: SyncState cascade report

**Files:**
- Create: `waves/global-ux/week-5-syncstate-cascade-report.md`

- [ ] Cover: per-component implementation status, screen-reader audit summary, dark-mode palette audit summary, any deferrals (Plan 6).

### Task §2.6: `stylelint-use-logical` wiring + runtime helper

**Files:**
- Modify: `packages/ui-core/.stylelintrc.json`
- Create: `packages/ui-core/src/test-helpers/expectLogicalPropertiesOnly.ts`

**Why:** Enforcement at the lint layer (authoring time) and runtime assertion layer (Storybook `play` or Vitest test time) so both paths catch regressions.

- [ ] **Step 1:** Install `stylelint` + `stylelint-use-logical` as devDependencies. Extend `.stylelintrc.json`: `{ "plugins": ["stylelint-use-logical"], "rules": { "csstools/use-logical": ["always", { "except": [] }] } }`.
- [ ] **Step 2:** Add `pnpm lint:css` script. Run on the cluster-complete outputs from Task §2.3. Zero violations is the gate.
- [ ] **Step 3:** `expectLogicalPropertiesOnly.ts` inspects a locator's `getComputedStyle` for any of the physical-axis long-hands; fails if any non-zero. Complementary to stylelint (which is static); runtime helper catches dynamic `style=""` regressions.

### Task §2.7: Logical-property sweep report

**Files:**
- Create: `waves/global-ux/week-4-logical-property-sweep-report.md`

- [ ] Cover: components swept, codemod vs manual-rewrite counts, allow-list entries (with justification each), remaining-risk analysis.

### Task §2.8: RTL regression finalization

**Files:**
- Modify: `waves/global-ux/week-5-rtl-regression-report.md` (finalize; was incrementally populated in Task §2.4)

- [ ] Finalize verdict: PASS with ≤ 5 HARD-RTL deferrals, or FAIL triggering scope-cut per Success Criteria.

---

## Week 6 — Matrix Integration + Gate Handoff

### Task §7.1: Integrate variants into Plan 4's matrix

**Files:**
- Modify: `packages/ui-core/.storybook/test-runner.ts` — ensure `prefers-reduced-motion` axis is included in the matrix iteration when a story declares motion-sensitive variants
- Modify: `packages/ui-core/.storybook/preview.ts` — final pass confirming LTR/RTL, theme, light/dark, CVD, reduced-motion all coexist

**Why:** Plan 4 builds the 36-scenario matrix (3 themes × 2 light/dark × 2 LTR/RTL × 3 CVD). Plan 4B adds the reduced-motion axis on animated components, doubling their scenario count (72 scenarios for animated, 36 for static).

- [ ] **Step 1:** Confirm Plan 4's matrix expansion config honors `parameters.globals.prefersReducedMotion`. If the expansion duplicates every scenario × {reduce, no-preference} regardless of whether the component animates, narrow: only animated-tagged stories include the reduced-motion axis. Tag via `parameters.a11y.sunfish.hasMotion: true`.
- [ ] **Step 2:** Full-matrix dry run: `pnpm --filter @sunfish/ui-core test:a11y`. Measure p95 per-scenario; project to full-`ui-core` wall time.
- [ ] **Step 3:** If wall time ≤ 15 min on 4-shard (Plan 4's budget), proceed. If > 15 min, invoke matrix-sharding fallback: 8-shard, OR move reduced-motion-on axis to nightly per-component suite.

### Task §7.2: Week-6 matrix integration report + Plan 5 handoff

**Files:**
- Create: `waves/global-ux/week-6-matrix-integration-report.md`
- Modify: `waves/global-ux/status.md` (end-of-Plan-4B update)

- [ ] **Step 1:** Compile all Week-3-to-Week-6 reports: CVD audit, dark-mode CVD audit, logical-property sweep, RTL regression, SyncState cascade, reduced-motion audit, matrix integration.
- [ ] **Step 2:** Score against Plan 4B success criteria. Each item PASS / FAIL / DEFERRED with evidence link.
- [ ] **Step 3:** Binary verdict: HANDOFF-TO-PLAN-5 (CI gates enforce these sweeps as required status checks) OR RE-PLAN with named fallback.
- [ ] **Step 4:** Plan 5 receives: the stylelint config, the runtime test-helper assertions, the ΔE2000 audit script, the RTL-regression script. Plan 5 wires them into `.github/workflows/`.

---

## Verification

### Automated

- `pnpm --filter @sunfish/ui-core audit:cvd` — ΔE2000 adjacency ≥ 25 under all three CVD modes, both light and dark palettes
- `pnpm --filter @sunfish/ui-core lint:css` — zero `stylelint-use-logical` violations (allow-list entries documented)
- `pnpm --filter @sunfish/ui-core test:a11y` — Plan 4's matrix green across all `ui-core` components; reduced-motion variants green; multimodal encoding assertions green on all SyncState-surfacing components
- `pnpm --filter @sunfish/ui-core test:rtl-screenshot` — ≤ 5 HARD-RTL-deferred components, all others clean
- Week-6 dry run: 4-shard wall time ≤ 15 min

### Manual

- Storybook dev server toolbar: LTR ↔ RTL toggle, reduced-motion toggle, CVD mode toggle — all three exposed simultaneously, compose cleanly, pilots render correctly in every combination
- Kitchen-sink settings: reduced-motion preference switcher; smoke-check dialog/toast/popover render without entrance animations
- NVDA re-audit of `sunfish-syncstate-indicator`: announces state transitions with polite vs assertive cadence per role
- VoiceOver re-audit of same: same
- Dark-mode palette visual check: five-state SyncState rendering under `prefers-color-scheme: dark` is distinguishable across all three CVD simulations

### Ongoing Observability

- CI metric: `stylelint-use-logical` violation count per PR; alert if non-zero slips past the `stylelint` gate (shouldn't — Plan 5 wires as blocking)
- CI metric: CVD ΔE2000 audit pass/fail on every PR touching `packages/ui-core/src/styles/tokens.css`; block merges on fail
- CI metric: reduced-motion coverage — for every `ui-core` component declaring `transition` or `animation`, assert a `ReducedMotion` story exists; block merges on missing coverage
- Post-Plan-5: weekly dashboard — new `ui-core` component lands without reduced-motion story → `SUNFISH_A11Y_001` analyzer warn (Plan 4's analyzer), escalate to Error if repeat offender

---

## Conditional sections

### Rollback Strategy

- **Palette rework failure (Task §5.1a fails after 2 attempts):** Invoke BDFL scope-cut. Named options: accept ΔE ≥ 20 + shape cue; drop tritanopia per-commit to quarterly; merge to 4-state. Timeline cost: 0-1 day; adds a debt entry if accepting ΔE ≥ 20.
- **Logical-property codemod miscompiles > 3 patterns:** Fall back to manual rewrites for affected patterns; document in `decisions.md`; extend timeline by ~1 day.
- **Reduced-motion JS-driver can't cleanly check `matchMedia` (e.g., an external library owns the animation):** Wrap the library call; if still intractable, defer the component to Plan 6 with a debt register entry.
- **Matrix integration breaks CI budget:** Per spec §7 fallback — 8-shard or reduced-motion-to-nightly. Named, precedent in Plan 4's rollback.
- **RTL screenshot-diff flakes > 2% of runs:** Switch from pixel-diff to DOM-structural-diff (bounding-rect symmetry check).

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Week-1 palette fails ΔE2000 adjacency under tritanopia | Medium | Medium-High (palette rework day cost) | Task §5.1 scheduled day 1 Week 3; rework path named |
| `@csstools/postcss-logical` codemod miscompiles a non-trivial pattern | Low-Medium | Low (manual rewrite; ~1 day) | Task §2.2 dry-run on 3 pilots before cascade |
| RTL screenshot diff flakes on anti-aliasing variance | Medium | Low-Medium | ≤ 2% pixel tolerance; DOM-structural-diff fallback |
| JS-driven animations can't be easily short-circuited under `prefers-reduced-motion` | Low | Medium (per-component workaround) | Task §6.2 inventory flags JS drivers; wrap at driver-call layer or defer |
| Dark-mode palette rework doubles the audit cost | Medium | Low | Task §5.3 scheduled in parallel with Task §5.2 cascade; independent failure path |
| Directional-icons registry (Task §2.5) misses an icon that should mirror | Low-Medium | Low (visual bug, not a11y failure) | Registry seeded conservatively; added as discovered; Storybook grid story catches omissions visually |
| Week-6 matrix runtime exceeds 15-min CI budget on 4-shard with 72 scenarios/animated-component | Medium | Medium | Plan 4's fallback path (8-shard, reduced-motion-nightly) named and tested |
| Subagent-driven cascade over-reaches commits | Medium | High (branch pollution) | Path-scoped `git add` mandatory; reviewer-agent gate per cluster (same guardrail as Plan 4 Task 3.2) |

### Dependencies & Blockers

- **Depends on:** Plan 1 complete (Week-1 SyncState pilot at `packages/ui-core/src/components/syncstate/`) ✅
- **Depends on:** Plan 4 Task 2.1 (production `postVisit` hook) + Task 2.2 (matrix decorators) + Task 4.2 (reduced-motion story infra scaffold) — must land by end of Week 4; Plan 4B Week-5 integration rides these
- **Blocks:** Plan 5 CI gates — Plan 5 wires the stylelint, ΔE2000 audit, and reduced-motion coverage into required status checks; Plan 4B is where the enforcement artifacts are authored
- **Blocks:** Plan 6 Phase 2 cascade — `blocks-*` and apps consuming `ui-core` inherit the sensory invariants only if `ui-core` lands them first
- **Parallel to:** Plan 2 (loc-infra) — zero file overlap (CSS ≠ .resx); can run same window
- **Parallel to:** Plan 3 (translator-assist) — zero file overlap
- **Parallel to:** Plan 4 (a11y foundation) — **file overlap:** both plans touch `.storybook/preview.ts` and `.storybook/test-runner.ts`. Coordination: Plan 4 lands scaffolding first (global toolbar stubs, hook skeleton); Plan 4B fills content via `Edit` operations that preserve Plan 4's scaffolding. Reviewer-agent serializes commits between Plan 4 and Plan 4B touching the same files.
- **External dependency:** `@csstools/postcss-logical` (npm, public), `culori` (npm, public), `stylelint-use-logical` (npm, public). No upstream version risk flagged.

### Delegation & Team Strategy

- **Solo-by-Claude for Week 3 gates:** CVD audit authoring + palette rework + logical-property inventory require careful reasoning about color spaces, browserslist semantics, and regex-pattern inventory — foreground Claude context beats subagent fan-out.
- **Subagent-fleet for Week 4 cascade:** Reuse Plan 4's cluster breakdown (primitives / overlays / forms / data-display / navigation) so Plan 4 and Plan 4B subagents align on cluster scope and don't collide on per-file ownership. One subagent per cluster per workstream. Path-scoped `git add` mandatory.
- **Subagent-fleet for Week 5 reduced-motion cascade:** Subset of Week 4's cluster set (only the ~8-12 components with motion). Reviewer-agent serial gate per cluster.
- **Solo-by-Claude for Week 6 integration:** Matrix tuning, shard projection, CI-budget analysis — foreground reasoning.

### Incremental Delivery

- **End of Week 3:** CVD audit PASS (or rework landed); logical-property codemod validated on 3 pilots; Workstream §2 ready to cascade.
- **End of Week 4:** Logical-property sweep done across all ~40 components; SyncState cascade done across all ~6-8 surfacing components; dark-mode palette landed; directional-icons registry + directive in place.
- **End of Week 5:** Reduced-motion tokens + per-component branches + stories landed; ARIA live-region announcements wired; stylelint config enforcing; RTL regression clean (≤ 5 deferrals); ADR 0036 accepted.
- **End of Week 6:** Matrix integration green; CI-budget headroom verified; Plan 5 has everything it needs for gate wiring.

### Reference Library

- [Spec §2, §5, §6 (lines 116-170, 441-561, 561-639)](../specs/2026-04-24-global-first-ux-design.md)
- [Plan 1 (Week 1 SyncState pilot)](./2026-04-24-global-first-ux-phase-1-week-1-plan.md)
- [Plan 2 (loc-infra — parallel)](./2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md)
- [Plan 4 (a11y foundation — parallel; file-overlap on `.storybook/`)](./2026-04-24-global-first-ux-phase-1-weeks-2-4-a11y-foundation-plan.md)
- [Plan 5 (CI gates — follow-on)]
- [Plan 6 (Phase 2 cascade — follow-on)]
- [ADR 0034 — A11y Harness Per Adapter](../../adrs/0034-a11y-harness-per-adapter.md)
- [ADR 0036 — SyncState multimodal encoding (authored by Task §5.6)](../../adrs/0036-syncstate-multimodal-encoding.md)
- [decisions.md (rollback log)](../../../waves/global-ux/decisions.md)
- MDN CSS logical properties: https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_logical_properties_and_values
- CSS Logical Properties and Values Baseline status: https://web.dev/baseline/
- `@csstools/postcss-logical`: https://github.com/csstools/postcss-plugins/tree/main/plugins/postcss-logical
- `stylelint-use-logical`: https://github.com/csstools/stylelint-use-logical
- ΔE2000 reference: https://en.wikipedia.org/wiki/Color_difference#CIEDE2000
- `culori` color space library: https://culorijs.org/
- Brettel/Viénot/Mollon CVD simulation model: https://pubmed.ncbi.nlm.nih.gov/9316278/
- WCAG 2.2 SC 1.4.11 (non-text contrast): https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html
- WCAG 2.2 SC 2.3.3 (animation from interactions): https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html
- Material Symbols icon names: https://fonts.google.com/icons

### Learning & Knowledge Capture

- Document in `waves/global-ux/decisions.md` on any palette rework, any stylelint allow-list entries (with justification), any HARD-RTL or HARD-motion deferrals to Plan 6.
- End-of-Week-6 retrospective: what surprised us (palette first-try failures? codemod edge cases? RTL regressions?), what the downstream cascade in Plan 6 should expect, what Plan 5's CI gates should be stricter about.

### Replanning Triggers

- Week-3 CVD audit fails AND rework Task §5.1a fails after 2 palette iterations: invoke BDFL scope-cut; re-scope Plan 4B with accepted compromise; add +1-2 days.
- Week-4 codemod requires manual rewrite on > 20% of components: escalate cluster cadence — 2 extra subagent waves; acceptable if Week-4 stays within timebox.
- Week-5 reduced-motion inventory > 15 components (assumed ~8-12): scope-cut to 8 highest-traffic; defer rest to Plan 6.
- Week-5 RTL regression surfaces > 5 HARD-RTL components: declare Week-5 partial; Plan 6 picks up; Plan 5 CI gate for RTL-regression scoped to the clean set.
- Week-6 matrix integration breaks CI budget beyond both 8-shard and CVD-nightly fallbacks: escalate to BDFL — options include moving RTL axis to nightly or dropping one theme from per-commit matrix.

### Completion Gate

Plan 4B is complete only when **all** of the following are true:

- [ ] CVD ΔE2000 audit PASS on both light and dark palettes under all three CVD simulations
- [ ] `stylelint-use-logical` zero violations across `packages/ui-core/src/components/*/` (allow-list entries ≤ 10, each justified in `.stylelintrc` comment)
- [ ] Every SyncState-surfacing `ui-core` component passes `expectMultimodalEncoding` for all five states
- [ ] Every animated `ui-core` component passes `expectReducedMotionRespected` under CDP reduced-motion emulation
- [ ] RTL screenshot-diff clean across `ui-core` (≤ 5 HARD-RTL deferrals; each with debt register entry)
- [ ] ADR 0036 accepted
- [ ] Week-6 matrix integration report verdict = HANDOFF-TO-PLAN-5
- [ ] `waves/global-ux/status.md` reflects Plan 4B complete

---

## Cold Start Test

A fresh agent walking into this plan should be able to execute Task §5.1 without further context by:
1. Reading this plan.
2. Reading the parent spec §2, §5, §6 (lines 116-170, 441-561, 561-639).
3. Reading the Week-1 SyncState pilot at `packages/ui-core/src/components/syncstate/`.
4. Reading Plan 4's Task 2.1, 2.2, 4.2 for the Storybook harness it integrates into.
5. Reading `waves/global-ux/decisions.md` for any prior palette / tokens decisions.

No additional context should be required. If any step requires out-of-band knowledge not in one of those five documents, that is a plan-hygiene bug — file an issue and update this plan before executing.
