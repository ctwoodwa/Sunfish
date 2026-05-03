# Wave 1 Review — Plan 4 Buttons a11y cascade (sentinel)

**Date:** 2026-04-25
**Code commit:** c17e708a
**Report commit:** d688242c
**Branch:** global-ux/wave-1-plan4-buttons-a11y
**Reviewer:** Wave 1 Reviewer (parent worktree)

---

## Per-criterion results

**(a) Diff shape — file inventory: PASS**
`git show --name-only c17e708a` returns exactly 11 new files, all under `packages/ui-adapters-blazor-a11y/tests/Buttons/`:
`ButtonGroupButtonA11yTests.cs`, `ButtonGroupToggleButtonA11yTests.cs`, `SunfishButtonA11yTests.cs`, `SunfishButtonGroupA11yTests.cs`, `SunfishChipA11yTests.cs`, `SunfishChipSetA11yTests.cs`, `SunfishFabA11yTests.cs`, `SunfishIconButtonA11yTests.cs`, `SunfishSegmentedControlA11yTests.cs`, `SunfishSplitButtonA11yTests.cs`, `SunfishToggleButtonA11yTests.cs`. No other paths touched.

Note: brief specified "15 components"; sentinel reconciled this to 11 actual `.razor` files (the 15-count was inflated by `.razor.cs` partials). All 11 production components are covered — cascade is complete for the real surface. Reasonable scope reconciliation, documented in the report.

**(b) Canonical pattern adherence: PASS**
Sampled 3 files (`SunfishButtonA11yTests.cs`, `SunfishSplitButtonA11yTests.cs`, `SunfishChipA11yTests.cs`) and compared against canonical `FreshnessBadgeContractTests.cs`. Each new test:
- Uses `IClassFixture<Ctx>` with shared `BunitContext`
- Registers `ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishThemeService` via NSubstitute
- Renders via `_ctx.Bunit.Render<T>(p => p.Add(...).AddChildContent(...))`
- Routes axe via `AxeRunner.RunAxeAsync(rendered.Markup, page)` with moderate+ severity filter
- Disposes via `IDisposable.Dispose() => Bunit.Dispose()`
- Uses `await PlaywrightPageHost.GetAsync()` for shared Playwright host

Structural divergence from contract tests (markup-axe vs ARIA-attribute assertions) is intentional and documented — these are two complementary layers.

**(c) Build clean: PASS (sentinel-attested, best-effort)**
Sentinel report cites a clean build (`0 Warning(s) / 0 Error(s)`) on `Sunfish.UIAdapters.Blazor.A11y.csproj`. The wave-1 branch is locked in an isolated worktree; per the brief I trust the sentinel's evidence rather than reconstructing the build state. Code review confirms no `csproj` changes that would affect compilation.

**(d) Test counts match: PASS**
Report shows `Failed: 0, Passed: 23, Skipped: 5, Total: 28`. Skip count (5) decomposes correctly: 2 SplitButton + 3 Chip = 5. Confirmed by counting `[Fact(Skip = ...)]` annotations in the diff (5 occurrences exactly).

**(e) Skip reasons cite real axe rules: PASS**
Grepped `[Fact(Skip = ...)]` across the diff — 5 hits, each citing a real axe rule:
- `axe violation: button-name on dropdown trigger` (SplitButton × 2)
- `axe violation: aria-required-parent (role=option without role=listbox)` (Chip default + selected)
- `axe violations: aria-required-parent + nested-interactive + target-size` (Chip removable)

All four cited rules (`button-name`, `aria-required-parent`, `nested-interactive`, `target-size`) are valid axe-core 4.x rule IDs from real WCAG/ARIA specs.

**(f) Commit message contains sentinel token: PASS**
Commit `c17e708a` body: `Token: wave-1-plan4-buttons-sentinel`. Subject also embeds `wave-1-plan4-buttons-sentinel`. Report commit `d688242c` likewise carries the token.

**(g) Diff-shape: tests-only, no production touch: PASS**
- Zero `.csproj` edits.
- Zero modifications to existing test files (e.g., `FreshnessBadgeContractTests.cs`, `NodeHealthBarContractTests.cs`).
- Zero edits to Buttons component sources (`SunfishSplitButton.razor`, `SunfishChip.razor`, etc.). Bug fixes correctly deferred per brief.

**(h) A11y bug findings real, documented, properly skipped: PASS**
Both findings are concrete and traceable:
1. `SunfishSplitButton` chevron-only secondary button → axe `button-name` (Critical). Affected tests properly `[Fact(Skip = ...)]` with rule citation.
2. `SunfishChip` triple violation: `aria-required-parent` (Critical) + `nested-interactive` (Serious) + `target-size` (Serious). Affected tests properly skipped, with the most severe test (Removable) annotated with all three violations.

Sentinel verified the bugs are real (not false positives) by capturing the pre-Skip run: `Failed: 5, Passed: 23, Skipped: 0, Total: 28` — same five tests, hard fails with axe rule IDs, impact levels, and dequeuniversity helpUrls. Bugs are not fixed in this commit (correct per brief).

---

## YELLOW evaluation

**ACCEPTABLE.** This is the textbook correct behavior for a cascade sentinel.

The cascade's purpose is to surface a11y bugs in production components by extending the proven harness across more surface area. When real bugs are found, the sentinel must NOT silently fix the production components — that would conflate "extend test harness" with "remediate components" in a single commit, hide the bug from the audit trail, and short-circuit the design conversations needed for proper fixes (the SunfishChip role="option" issue, for example, has architectural choices: cascade the role from ChipSet vs enforce parent-child).

The sentinel did the right things in the right order:
1. Wrote the cascade tests against the real components.
2. Ran them — got 5 failures.
3. Documented each failure with rule, impact, file, defect, and a fix recommendation.
4. Annotated the failing tests with `[Fact(Skip = "axe violation: <rule> — see report")]` so the suite stays green and the cascade can land.
5. Filed the bugs in the report for follow-up tickets.

This preserves the cascade pattern, keeps CI green, makes the bugs visible (Skip annotations are loud and grep-able), and respects scope boundaries. A REJECTION here would set a precedent that sentinels mix harness and remediation work — the opposite of what we want.

The verdict is correctly self-assessed as YELLOW (not GREEN) because 5 tests are not actually exercising axe assertions — they're documented IOUs. That accurately conveys the "cascade landed; component bugs outstanding" state.

---

## A11y bug findings to track as follow-ups

1. **`SunfishSplitButton` — `button-name` (Critical)** on the chevron-only dropdown-trigger button.
   File: `packages/ui-adapters-blazor/Components/Buttons/SunfishSplitButton.razor`.
   Recommendation: dedicated PR. Add `MenuAriaLabel` parameter (mirroring `SunfishIconButton`'s required-name pattern) defaulting to a localizable canonical phrase ("Open menu"). Re-enable the 2 skipped tests in the same PR. Estimated scope: small (one component + i18n string + 2 test un-skips).

2. **`SunfishChip` — triple violation: `aria-required-parent` (Critical) + `nested-interactive` (Serious) + `target-size` (Serious).**
   File: `packages/ui-adapters-blazor/Components/Buttons/Chip/SunfishChip.razor`.
   Recommendation: dedicated PR — these three are entangled and need a single architectural fix, not three patches. Likely shape:
   - Cascade `role` from `SunfishChipSet` (drop hard-coded `role="option"` on the chip itself).
   - Convert outer interactive `<span>` to a real `<button>` (kills `nested-interactive`); restructure remove control as a sibling button or move it outside the chip's interactive surface.
   - Add 24×24 minimum hit-target sizing to the remove button per WCAG 2.2.
   Re-enable 3 skipped tests in same PR. Estimated scope: medium (component refactor + ChipSet cascade + CSS + 3 test un-skips). Consider a brief design note before coding — multiple valid approaches.

Recommend filing both as separate tickets under a "Buttons folder a11y remediation" parent so they can be sequenced and tracked independently of further cascade waves.

---

## Final verdict: YELLOW

The sentinel's YELLOW is correct, well-evidenced, and properly scoped. Cascade extends cleanly from 2 SyncState pilots to 11 Buttons components. The 5 skipped tests are valid IOUs against real production a11y bugs, not pattern failures. Approve the merge of `c17e708a` + `d688242c` and open the two follow-up tickets above.
