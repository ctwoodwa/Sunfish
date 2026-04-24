# Global-First UX — Wave Status

**Updated:** 2026-04-24 (Week 1 Day 1 complete — all research + ADRs landed)
**Current phase:** Phase 1 Week 1 (Tooling Pilot)
**Current focus:** Ready to start code pilot (Tasks 8-13)

## Completed this week
- Task 1: Scaffold `waves/global-ux/` tracking directory (`9f14858b`)
- Task 2: ICU4N health check memo — verdict **PIVOT TO FALLBACK** (SmartFormat.NET + .NET 8 System.Globalization). See decisions.md entry 2026-04-25. (`cb286789`)
- Task 3: Weblate vs Crowdin memo — default (Weblate self-hosted) confirmed. AGPL flagged as open legal question for future commercial hosting. (`512c0aae`)
- Task 4: XLIFF tool ecosystem memo — recommendation **BUILD** (~5.5 days). MAT EOL Oct 2025; no maintained .NET XLIFF 2.0 library exists. (`74ed1e94`)
- Task 5: ADR 0034 (A11y Harness per Adapter) — Proposed (`f33baeba`)
- Task 6: ADR 0035 (Global Domain Types as Separate Wave) — Proposed (`7e7ccb71`)
- Task 7: ADRs 0034 + 0035 flipped to Accepted (`972e2f6b`)

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

## Next agent handoff context

**Next wave — Tasks 8-13 (code pilot):**
- Task 8: pnpm workspace config + `packages/ui-core/package.json`
- Task 9: Storybook 8 config (`.storybook/main.ts` + `preview.ts`) with a11y addon
- Tasks 10-12: Three Lit Web Component pilots (button, dialog, syncstate-indicator) with a11y contracts
- Task 13: CI runtime measurement per component

**Commit strategy:** Feature branch `global-ux/code-pilot` for Tasks 8-13 (code work).
Merge to main when green. Path-scoped `git add` on every commit — never `git add .`.

**Tasks 14-15 (localization wrapper + smoke tests):** Re-scoped per ICU4N pivot above;
implementation uses SmartFormat.NET.

**Task 16 (Week 1 go/no-go gate):** End-of-week decision node.
