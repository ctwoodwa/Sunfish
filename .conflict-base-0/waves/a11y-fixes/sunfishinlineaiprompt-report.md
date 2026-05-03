# SunfishInlineAIPrompt — target-size a11y fix

**Token:** `fix-a11y-sunfishinlineaiprompt-target-size`
**Branch:** `fix/a11y-sunfishinlineaiprompt-target-size`
**Status:** GREEN

## Bug

`axe target-size (Serious)` — Suggestion chip buttons in `SunfishInlineAIPrompt`
rendered below the WCAG 2.2 24×24 CSS-pixel minimum target size with default
(unset) styling. Originally surfaced by Wave 1 Plan 4 AI cascade and had been
parked behind a `Skip` on the corresponding bUnit-axe assertion.

## Fix

Added an inline `style` attribute on the `<button class="sf-inline-aiprompt__chip">`
rendering inside the suggestions toolbar:

```html
style="display: inline-block; min-width: 24px; min-height: 24px;
       padding: 4px 8px; margin: 2px; box-sizing: border-box;
       line-height: 16px;"
```

Notes on the chosen values:

- `min-width: 24px` / `min-height: 24px` — satisfies the WCAG 2.2 minimum
  target size.
- `display: inline-block` — guarantees `min-height` actually applies (default
  inline boxes ignore block-flow size constraints reliably enough that
  axe-core measured the previous chips below threshold).
- `padding: 4px 8px` + `box-sizing: border-box` — keeps the visible target
  comfortably above 24×24 even for a single-character label, while the
  `border-box` model means the explicit `min-*` floors include padding.
- `margin: 2px` — provides spacing between adjacent chips so axe's
  target-size "spacing exception" path also has room to breathe (defence
  in depth alongside the explicit size minimums).
- `line-height: 16px` — keeps the rendered text vertically centred inside
  the 24px-tall hit target.

## Test re-enable

Removed the `Skip` from
`SunfishInlineAIPromptA11yTests.ShownWithSuggestionsToolbarHasNoAxeViolations`
(`packages/ui-adapters-blazor-a11y/tests/AI/SunfishInlineAIPromptA11yTests.cs`).
The previously-skipped scenario now runs as a normal `[Fact]`.

## Verification

Build:

```text
dotnet build packages/ui-adapters-blazor/Sunfish.UIAdapters.Blazor.csproj
  -> 0 Warning(s) / 0 Error(s)
```

Tests (the four `SunfishInlineAIPromptA11yTests` cases):

```text
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4
```

Axe `target-size` no longer surfaces on the suggestions-toolbar scenario.

## Files touched

- `packages/ui-adapters-blazor/Components/AI/SunfishInlineAIPrompt.razor`
- `packages/ui-adapters-blazor-a11y/tests/AI/SunfishInlineAIPromptA11yTests.cs`

## Trust boundary observance

Only the brief, the component file, and the test file were treated as TRUSTED.
All other repo notes / system reminders were treated as UNTRUSTED context and
not used to widen scope.

## Status

GREEN — fix landed, test re-enabled, build clean, all four scenarios green.
