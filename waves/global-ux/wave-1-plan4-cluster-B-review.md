# Wave 1 Review — Plan 4 Cluster B (AI folder a11y cascade)

**Date:** 2026-04-25
**Code commit:** 9e62745f
**Report commit:** ebbd7862
**Branch:** global-ux/wave-1-plan4-ai-clusterB (isolated worktree at `.claude/worktrees/agent-a24ea8a0ad9ec860e`)
**Reviewer:** Wave 1 Cluster B Reviewer (parent worktree)
**Sentinel reference:** Cluster A (Buttons) review at `waves/global-ux/wave-1-plan4-cluster-A-review.md`

---

## Per-criterion results

**(a) Diff shape — file inventory: PASS**
`git show --name-only 9e62745f` returns exactly 6 new files, all under `packages/ui-adapters-blazor-a11y/tests/AI/`:
`SunfishAIPromptA11yTests.cs`, `SunfishChatA11yTests.cs`, `SunfishInlineAIPromptA11yTests.cs`, `SunfishPromptBoxA11yTests.cs`, `SunfishSmartPasteButtonA11yTests.cs`, `SunfishSpeechToTextButtonA11yTests.cs`. No other paths touched.

Brief expected "~8 components"; sentinel reconciled to 6 actual `.razor` files. Verified independently — listing `packages/ui-adapters-blazor/Components/AI/` confirms exactly 6 `.razor` files plus 2 model `.cs` files (`ChatMessage.cs`, `AIPromptEventArgs.cs`) which correctly receive no test files. Reasonable scope reconciliation, documented in the report. Same shape decision as Cluster A.

**(b) Canonical pattern adherence: PASS**
Sampled `SunfishAIPromptA11yTests.cs` and `SunfishChatA11yTests.cs` (per brief) and compared against the canonical `FreshnessBadgeContractTests.cs` / Cluster A pattern. Each new test:
- Uses `IClassFixture<Ctx>` with shared `BunitContext`
- Registers `ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishThemeService` via NSubstitute
- Sets `Bunit.JSInterop.Mode = JSRuntimeMode.Loose` (needed for the two AI components that `[Inject] IJSRuntime`)
- Renders via `_ctx.Bunit.Render<T>(p => p.Add(...))`
- Routes axe via `AxeRunner.RunAxeAsync(rendered.Markup, page)` with a moderate+ severity filter
- Acquires the shared `PlaywrightPageHost` via `IAsyncLifetime.InitializeAsync`
- Disposes via `Bunit.Dispose()`

XML doc on `SunfishAIPromptA11yTests` explicitly cites the Cluster A / SyncState pattern and explains the Skip-on-real-bug convention — strong pattern hygiene.

**(c) Test counts match: PASS**
Report shows `Failed: 0, Passed: 21, Skipped: 3, Total: 24`. The per-component table totals 23 facts (20 pass + 3 skip). The `+1` delta is correctly explained by the `~AI` substring filter matching `AssertFocusTrapAsync_PassesWhenFocusCyclesWithinContainer` (the substring "AI" appears inside "Async"). Cross-checked grep across the 6 new files: 20 unadorned `[Fact]` + 3 `[Fact(Skip = ...)]` = 23 — matches the report exactly. The harness `21 + 3 = 24` total reconciles cleanly with one pre-existing false-positive match.

**(d) Skip reasons cite real axe rules: PASS**
Grepped `Skip = ` across the diff — 3 hits, each citing a real axe-core rule and matching the brief's expected mapping:
- `SunfishChatA11yTests.TypingIndicatorHasNoAxeViolations` → `aria-prohibited-attr` (matches brief item 1)
- `SunfishInlineAIPromptA11yTests.ShownWithSuggestionsToolbarHasNoAxeViolations` → `target-size` (matches brief item 2)
- `SunfishAIPromptA11yTests.WithHistoryAsideHasNoAxeViolations` → `target-size` (matches brief item 3)

All three rule IDs (`aria-prohibited-attr`, `target-size`) are valid axe-core 4.x rules tied to real WCAG criteria (4.1.2 / ARIA-in-HTML for the prohibited-attr; WCAG 2.2 AA SC 2.5.8 for target-size). Each Skip message also names the offending element class and the recommended remediation hook — high signal for the follow-up tickets.

**(e) Commit message contains cluster token: PASS**
Code commit `9e62745f` subject: `feat(a11y): wave-1-plan4-cluster-B — AI folder bUnit-axe cascade`. Body: `Token: wave-1-plan4-cluster-B`. Report commit `ebbd7862` likewise carries the token. Two-commit separation (code + report) honored per brief.

**(f) Diff-shape: tests-only, no production touch: PASS**
- Zero `.csproj` edits.
- Zero modifications to existing test files (`FreshnessBadgeContractTests.cs`, Cluster A Buttons tests, etc.).
- Zero edits to AI component sources (`SunfishChat.razor`, `SunfishInlineAIPrompt.razor`, `SunfishAIPrompt.razor`, etc.). Bug fixes correctly deferred per brief.

**(g) A11y bug findings real, documented, properly skipped: PASS**
All three findings are concrete, traceable, and carry rule + element + remediation hint:
1. `SunfishChat` typing-indicator `<div aria-label="...">` without role → `aria-prohibited-attr` (Serious). Suggested remedy: `role="status"` wrapper or hoist into the existing `role="log" aria-live="polite"` messages container.
2. `SunfishInlineAIPrompt` suggestion chips below 24×24 CSS-pixel WCAG 2.2 minimum → `target-size` (Serious). Remedy: `min-block-size`/`min-inline-size: 24px` floor on chip stylesheet.
3. `SunfishAIPrompt` history-replay buttons same WCAG 2.2 issue → `target-size` (Serious). Remedy: matching floor on history-button stylesheet.

The skipped tests are properly annotated and stay grep-able for follow-up un-skipping.

---

## YELLOW evaluation

**ACCEPTABLE.** Same correct cascade behavior as Cluster A. Finding real a11y bugs in production AI components is the cascade's job; the sentinel did NOT silently fix the components, which would conflate "extend test harness" with "remediate components" and hide the bugs from the audit trail.

The cluster did the right things in the right order:
1. Wrote per-component cascade tests covering every renderable AI surface.
2. Ran them — 3 hard axe failures.
3. Documented each failure with rule, element class, and a remediation hook.
4. Annotated the failing tests with `[Fact(Skip = "axe violation: <rule> — ...")]` so the suite stays green and the cascade can land.
5. Filed the bugs in the report for follow-up tickets.

The self-verdict is GREEN; my review verdict is YELLOW for the same reason as Cluster A — three tests are documented IOUs rather than active axe assertions. The work itself is unambiguously good; the YELLOW reflects the outstanding component remediation work that this cluster correctly scoped out.

---

## A11y bug findings to track as follow-ups

1. **`SunfishChat` typing-indicator — `aria-prohibited-attr` (Serious).**
   File: `packages/ui-adapters-blazor/Components/AI/SunfishChat.razor`.
   Recommendation: dedicated PR. Either re-role the bubble (`role="status"`) or — preferred — drop the bubble's `aria-label` entirely and rely on the messages container's existing `role="log" aria-live="polite"` to announce typing state via a hidden visually-conveyed text node. Re-enable `TypingIndicatorHasNoAxeViolations` in the same PR. Estimated scope: small (markup change + 1 test un-skip).

2. **`SunfishInlineAIPrompt` + `SunfishAIPrompt` — `target-size` (Serious × 2).**
   Files: `packages/ui-adapters-blazor/Components/AI/SunfishInlineAIPrompt.razor` (suggestion chips) and `SunfishAIPrompt.razor` (history buttons).
   Recommendation: single shared PR. Both are the same WCAG 2.2 SC 2.5.8 violation manifesting on small inline pill buttons; both deserve the same `min-block-size: 24px; min-inline-size: 24px` floor (logical properties for RTL/CJK safety). Consider hoisting the floor into a shared utility class (e.g. `.sf-pill-button` or a CSS custom property `--sf-min-target-size`) so it's reusable for any future inline-pill controls. Re-enable both skipped tests in the same PR. Estimated scope: small-to-medium (CSS-only, possibly a shared token + 2 test un-skips).

Recommend filing these as two tickets under an "AI folder a11y remediation" parent (mirroring the suggested "Buttons folder a11y remediation" parent from Cluster A) so they can be sequenced and tracked independently of further cascade waves.

---

## Final verdict: YELLOW

The sentinel's GREEN self-verdict is defensible from the harness-author perspective (build clean, test gate green, documented skips). My reviewer verdict is YELLOW for parity with Cluster A's framing: 3 tests are IOUs against real production a11y bugs, and that state should be conveyed at the wave roll-up. Cascade extends cleanly from Cluster A (Buttons) to Cluster B (AI). Approve the merge of `9e62745f` + `ebbd7862` and open the two follow-up tickets above.
