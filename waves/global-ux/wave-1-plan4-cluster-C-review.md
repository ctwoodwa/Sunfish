# Wave 1 Review — Plan 4 Cluster C (Forms + Overlays)

**Date:** 2026-04-25
**Code commit:** e4e0f511
**Report commit:** 46b946eb
**Branch:** global-ux/wave-1-plan4-forms-overlays-clusterC

## Per-criterion results

- **(a) Diff scope — 7 new test files in correct dirs:** PASS. `git show --name-only e4e0f511` returns exactly 3 files under `packages/ui-adapters-blazor-a11y/tests/Forms/` (`SunfishLabelA11yTests.cs`, `SunfishFieldA11yTests.cs`, `SunfishTextBoxA11yTests.cs`) and 4 files under `packages/ui-adapters-blazor-a11y/tests/Overlays/` (`SunfishPopupA11yTests.cs`, `SunfishWindowA11yTests.cs`, `WindowActionButtonA11yTests.cs`, `WindowActionsA11yTests.cs`). No other paths.
- **(b) Pattern mirrors sentinel:** PASS. Read `SunfishLabelA11yTests.cs` and `SunfishWindowA11yTests.cs`. Both use the canonical `IClassFixture<Ctx>` pattern with shared `BunitContext` + `PlaywrightPageHost`, three NSubstitute service stubs (`ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishThemeService`), `AxeRunner.RunAxeAsync` against the rendered markup, and Moderate+ violation filter — identical structure to the sentinel `PilotMatrixTests` / `FreshnessBadgeContractTests`. Naming convention, namespace shape, and assertion message format all match.
- **(c) Fact count 7 (6 pass + 1 skip + 0 fail):** PASS. Grep across all 7 files: 6 unadorned `[Fact]` + 1 `[Fact(Skip = ...)]` = 7. Report's xUnit log confirms `Failed: 0, Passed: 6, Skipped: 1, Total: 7`.
- **(d) Skip on aria-dialog-name / SunfishPopup / FocusTrap=true:** PASS. `SunfishPopupA11yTests.cs` line 39: `[Fact(Skip = "axe violation: aria-dialog-name — popup root needs aria-label or aria-labelledby")]`. The fact body sets `FocusTrap=true` (line 44) and the preceding comment explicitly cites `role="dialog" + aria-modal="true"` without an accessible name and labels it "a real component bug; per Cluster C brief we mark it Skip and DO NOT fix here."
- **(e) Commit message contains `wave-1-plan4-cluster-C` token:** PASS. Subject: `feat(a11y): wave-1-plan4-cluster-C — Forms + Overlays bUnit-axe cascade`; body: `Token: wave-1-plan4-cluster-C`.
- **(f) Diff shape — tests-only, no csproj or component edits:** PASS. All 7 paths sit under the two test subdirectories. No csproj, no modifications to existing tests, no component changes. (Pre-existing `InternalsVisibleTo("DynamicProxyGenAssembly2")` in `Sunfish.UIAdapters.Blazor.csproj:32` is untouched and load-bearing — see (g).)
- **(g) Reflection-based substitution for internal interop:** PASS (and ACCEPTABLE — see below). `SunfishWindowA11yTests.Ctx` calls `RegisterInternalInteropService` for `IElementMeasurementService`, `IDragService`, and `IResizeInteractionService` via `typeof(SunfishWindow).Assembly.GetType(fullTypeName, throwOnError: true)` + `NSubstitute.Substitute.For(Type[], object[])`, then registers the proxy under the resolved interface type. The pre-existing `InternalsVisibleTo("DynamicProxyGenAssembly2")` lets DynamicProxy implement the internal interface without IVT to the test assembly.
- **(h) The 1 a11y bug finding is real, documented, and properly skipped:** PASS. `aria-dialog-name` (WCAG 4.1.2, Serious) on a `role="dialog" + aria-modal="true"` root with no `aria-label`/`aria-labelledby` is a textbook screen-reader-confounding violation. Skip annotation is grep-able, cites the rule, and explains the missing remediation surface. Same pattern as Cluster A and B.

## Reflection-substitution evaluation (Forms/Overlays-specific)

**ACCEPTABLE** — with one caveat worth tracking.

The brief forbade csproj edits, and the alternatives were worse: (i) a parallel test-only IVT line specifically for `Sunfish.UIAdapters.Blazor.A11y.Tests` (out of scope per brief), (ii) extracting `IDragService`/`IResizeInteractionService`/`IElementMeasurementService` to `public` (over-architecting for a test concern), or (iii) skipping all four Overlays tests (defeats the cluster goal). The chosen approach exploits the already-load-bearing `InternalsVisibleTo("DynamicProxyGenAssembly2")` that DynamicProxy needs anyway — a clean, documented, single-purpose escape hatch with a multi-line comment explaining why it exists.

**Caveat to track:** if any of the three internal interop interface FQNs change (rename, namespace move), the reflection lookup will throw at fixture init with `TypeLoadException` rather than at compile time. Recommend adding a small assertion-style smoke test (or, on the next opportunity to touch the csproj, a proper test-assembly IVT) so the maintenance hazard surfaces as a clear test failure rather than as opaque NSubstitute init noise. Not a blocker for Cluster C.

## A11y bug to track as follow-up

- **SunfishPopup with `FocusTrap=true` — `aria-dialog-name` (WCAG 4.1.2, Serious).** Popup emits `role="dialog"` + `aria-modal="true"` but no `aria-label` / `aria-labelledby` on the root. Recommended fix: add a `PopupAriaLabel` parameter (mirroring the `MenuAriaLabel` pattern proposed in Cluster A's SunfishMenu finding) defaulting to a localizable canonical phrase, and emit `aria-labelledby` when the popup hosts a discoverable header. Re-enable `SunfishPopup_VisibleWithFocusTrap_ZeroAxeViolations` in the same PR. Estimated scope: small (one component param + i18n string + 1 test un-skip).

## GREEN-vs-YELLOW evaluation

Verdict: **YELLOW**, for parity with Cluster A and B's framing. The sentinel's GREEN is defensible from a harness-author perspective (build green, test gate green, documented skip), but Cluster A and B were both YELLOW with skipped IOUs of equal severity-class — A had 5 skips, B had 3 skips, C has 1. The threshold is not "1 skip" or "N skips" — it's "are there outstanding component a11y bugs that the wave roll-up should convey?" The answer is yes. Holding C to the same bar avoids a confusing wave-level signal where C reads cleaner than A/B despite carrying the same kind of debt.

## Final verdict: YELLOW
