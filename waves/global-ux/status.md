# Global-First UX — Wave Status

**Updated:** 2026-04-24 (Week 1 Days 1-4 complete — research + ADRs + code pilot all landed on main)
**Current phase:** Phase 1 Week 1 (Tooling Pilot)
**Current focus:** Ready to start Tasks 14-15 (localization wrapper — re-scoped to SmartFormat.NET per ICU4N pivot)

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

**Next wave — Tasks 14-15 (localization wrapper + smoke tests):**

**Re-scoped per ICU4N pivot (see decisions.md entry 2026-04-25).** Original plan used ICU4N
as the ICU MessageFormat backend behind `IStringLocalizer<T>`. Pivot is to **SmartFormat.NET**
(plural/select/message logic) + **.NET 8 System.Globalization in ICU mode** (number/date/
currency formatting).

- Task 14: Scaffold `packages/foundation/Localization/` with `ISunfishLocalizer<T>` and
  `SunfishLocalizer<T>`. Add `SmartFormat` NuGet package to `Directory.Packages.props` and
  `PackageReference` in `Sunfish.Foundation.csproj`. Public contract is unchanged from the
  original plan; implementation switches.
- Task 15: Three smoke tests (en/ar/ja). Tests verify:
  1. English one/other plural rule (1 item vs N items)
  2. Arabic six-form plural rule (zero/one/two/few/many/other)
  3. Japanese number formatting without plural inflection (Japanese has no grammatical plural)

**Commit strategy:** Feature branch `global-ux/localization` for Tasks 14-15.
Merge to main when green. Path-scoped `git add` only.

**Task 16 (Week 1 go/no-go gate):** End-of-week decision node consuming:
- Research memo verdicts (Tasks 2-4 — done)
- Accepted ADRs (Tasks 5-7 — done)
- Storybook + a11y harness pilots (Tasks 8-12 — done)
- Runtime measurement (Task 13 — done, verdict GREEN)
- SmartFormat.NET smoke tests (Tasks 14-15 — pending)
- Any blockers surfaced during the week (none so far)
