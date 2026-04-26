# Wave 1 Plan 4 — Cluster B (AI folder a11y harness)

**Token:** `wave-1-plan4-cluster-B`
**Branch:** `global-ux/wave-1-plan4-ai-clusterB`
**Date:** 2026-04-25
**Self-verdict:** GREEN (build + tests pass; 3 documented skips for real component bugs)

## Scope

Extend the bUnit-axe a11y harness (proven by Cluster A on Buttons and by the
SyncState contract tests) to every `.razor` component in
`packages/ui-adapters-blazor/Components/AI/`.

## Inventory reconciliation

The brief expected **~8** components ("plus variations"). The actual folder
inventory is **6** `.razor` components plus 2 `.cs` model/event-args files
(`ChatMessage.cs`, `AIPromptEventArgs.cs`). No variation suffixes exist
(no `*Compact`, `*Mobile`, etc.). Cluster B therefore covers the full set of
6 razor components — every renderable AI surface gets a per-component a11y
test file.

```
packages/ui-adapters-blazor/Components/AI/
  AIPromptEventArgs.cs                 (model — no test)
  ChatMessage.cs                       (model — no test)
  SunfishAIPrompt.razor                ✅
  SunfishChat.razor                    ✅
  SunfishInlineAIPrompt.razor          ✅
  SunfishPromptBox.razor               ✅
  SunfishSmartPasteButton.razor        ✅
  SunfishSpeechToTextButton.razor      ✅
```

## Files added

All under `packages/ui-adapters-blazor-a11y/tests/AI/` — diff-shape preserved
(no csproj edits, no component edits, no other folders touched):

| File | Tests | Pass | Skip | Component scenarios covered |
|---|---|---|---|---|
| `SunfishAIPromptA11yTests.cs` | 5 | 4 | 1 | default, model-picker, streaming, history (skip: target-size), disabled |
| `SunfishChatA11yTests.cs` | 4 | 3 | 1 | empty, messages (3 roles), typing (skip: aria-prohibited-attr), disabled |
| `SunfishInlineAIPromptA11yTests.cs` | 4 | 3 | 1 | shown-with-text, with-suggestions (skip: target-size), shown-empty, hidden |
| `SunfishPromptBoxA11yTests.cs` | 4 | 4 | 0 | default-empty, seed-value, history-closed, disabled |
| `SunfishSmartPasteButtonA11yTests.cs` | 3 | 3 | 0 | default, custom-label, disabled |
| `SunfishSpeechToTextButtonA11yTests.cs` | 3 | 3 | 0 | idle, non-default-language (ar-SA + Continuous), disabled |
| **Cluster-B AI total** | **23** | **20** | **3** | |

The harness summary (`dotnet test --filter "FullyQualifiedName~AI"`) reports
`Passed: 21, Skipped: 3, Total: 24`. The substring filter `~AI` is broad
enough to match one pre-existing non-AI test
(`AssertionTests.AssertFocusTrapAsync_PassesWhenFocusCyclesWithinContainer`
contains the substring "AI" inside "Async"), accounting for the `+1`
between the per-component table and the harness total. All 23 actual AI
tests are accounted for above.

## Pattern

Each test file mirrors `SyncStatusIndicatorContractTests` + `PilotMatrixTests`:

- One `IClassFixture<Ctx>` per file.
- `Ctx` constructs a `BunitContext` with the three `SunfishComponentBase`
  injects (`ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishThemeService`)
  substituted via NSubstitute, plus `JSInterop.Mode = JSRuntimeMode.Loose` for
  the two components that `[Inject] IJSRuntime`.
- `Ctx` implements `IAsyncLifetime` to lazily acquire the shared
  `PlaywrightPageHost` so all six fixtures share one chromium instance.
- Each `[Fact]` renders the component with bUnit, then ships
  `rendered.Markup` through `AxeRunner.RunAxeAsync` with the default
  `AxeOptions` (WCAG 2.0 A/AA + 2.1 A/AA + 2.2 AA + best-practice).
- Assertion: zero `AxeImpact.Moderate`-or-greater violations. Failure
  message lists the rule ids so the bridge run is debuggable.

## A11y bugs found

The harness uncovered three real moderate-or-greater axe violations in the
production AI components. Per the cluster-B brief these are documented via
`[Fact(Skip = "axe violation: <rule>")]` rather than fixed:

1. **`SunfishChat.TypingIndicator` → `aria-prohibited-attr`**
   The typing-indicator bubble (`<div class="sf-chat__bubble--typing"
   aria-label="Assistant is typing">`) applies `aria-label` to a
   non-interactive `<div>` with no role. axe-core forbids `aria-label` on
   such elements (WCAG 4.1.2 / ARIA-in-HTML). Suggested remedy: wrap or
   re-role as `role="status"` / move the live region up to the messages
   container which is already `role="log" aria-live="polite"`.

2. **`SunfishInlineAIPrompt` (with `Suggestions`) → `target-size`**
   The suggestion-chip buttons (`<button class="sf-inline-aiprompt__chip">`)
   render below the WCAG 2.2 24×24 CSS-pixel minimum target size with
   default styling. Suggested remedy: enforce
   `min-block-size: 24px; min-inline-size: 24px` in the chip stylesheet.

3. **`SunfishAIPrompt` (with `History`) → `target-size`**
   Same WCAG 2.2 issue on the history-replay buttons
   (`<button class="sf-aiprompt__history-button">`). Suggested remedy:
   matching `min-block-size`/`min-inline-size` floor on the history-button
   stylesheet.

All three are component-level CSS / markup bugs and out of scope for this
cluster. The skip messages name the rule and the remediation hook so a
follow-up ticket can flip them back on after the fix lands.

## Build + test gate

```text
$ dotnet build packages/ui-adapters-blazor-a11y/Sunfish.UIAdapters.Blazor.A11y.csproj
  Sunfish.UIAdapters.Blazor.A11y -> ...\bin\Debug\net11.0\Sunfish.UIAdapters.Blazor.A11y.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:00.74
```

```text
$ dotnet test packages/ui-adapters-blazor-a11y/tests/tests.csproj --filter "FullyQualifiedName~AI"
[xUnit.net 00:00:01.11]     ...SunfishChatA11yTests.TypingIndicatorHasNoAxeViolations [SKIP]
[xUnit.net 00:00:01.57]     ...SunfishInlineAIPromptA11yTests.ShownWithSuggestionsToolbarHasNoAxeViolations [SKIP]
[xUnit.net 00:00:02.31]     ...SunfishAIPromptA11yTests.WithHistoryAsideHasNoAxeViolations [SKIP]

Passed!  - Failed: 0, Passed: 21, Skipped: 3, Total: 24, Duration: 2 s
```

Build is clean (0 warnings, 0 errors on the a11y csproj). Test filter
`AI` returns 24 tests: 21 pass, 3 documented skips, 0 failures.

## Diff-shape verification

```text
$ git status --short
?? packages/ui-adapters-blazor-a11y/tests/AI/SunfishAIPromptA11yTests.cs
?? packages/ui-adapters-blazor-a11y/tests/AI/SunfishChatA11yTests.cs
?? packages/ui-adapters-blazor-a11y/tests/AI/SunfishInlineAIPromptA11yTests.cs
?? packages/ui-adapters-blazor-a11y/tests/AI/SunfishPromptBoxA11yTests.cs
?? packages/ui-adapters-blazor-a11y/tests/AI/SunfishSmartPasteButtonA11yTests.cs
?? packages/ui-adapters-blazor-a11y/tests/AI/SunfishSpeechToTextButtonA11yTests.cs
?? waves/global-ux/wave-1-plan4-cluster-B-report.md
```

No csproj edits. No modifications to existing tests. No touching the AI
components themselves. No edits outside `packages/ui-adapters-blazor-a11y/tests/AI/`
for the code commit; the report lives under `waves/global-ux/` as a
separate commit per the brief's "SEPARATELY commit the report" instruction.

## Self-verdict: GREEN

- Build gate clean (0 errors, 0 warnings on the a11y csproj).
- Test filter `~AI` returns 24 tests, 21 pass, 3 documented skips, 0 failures.
- Three real a11y bugs surfaced and documented (not fixed) per brief.
- Diff is path-scoped to the cluster-B target folder.
- Two commits (code + report) separated per brief.
