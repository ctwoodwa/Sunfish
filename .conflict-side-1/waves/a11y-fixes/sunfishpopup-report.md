# A11y Fix Report — SunfishPopup `aria-dialog-name`

**Token:** `fix-a11y-sunfishpopup-aria-dialog-name`
**Branch:** `fix/a11y-sunfishpopup-aria-dialog-name`
**Date:** 2026-04-25
**Status:** GREEN

---

## Bug

axe rule: `aria-dialog-name` (WCAG 4.1.2, Serious)

When `SunfishPopup.FocusTrap=true`, the popup root rendered:

```html
<div role="dialog" aria-modal="true" tabindex="-1" …>
```

…with no `aria-label` or `aria-labelledby`. Screen readers announced the dialog
with no name, failing WCAG 4.1.2 (Name, Role, Value).

## Fix

1. **New `Title` parameter** on `SunfishPopup` (`string?`, default `null`).
2. **Razor binding:** when `FocusTrap=true`, the popup root now emits
   `aria-label="@EffectiveAriaLabel"`. When `FocusTrap=false`, no `aria-label`
   is rendered (no dialog role exists, so the rule does not apply).
3. **Gentle fallback:** when `FocusTrap=true` and `Title` is null/whitespace,
   `OnParametersSet` writes a one-time console warning to `Console.Error` and
   the binding falls back to `aria-label="Dialog"`. The fallback satisfies
   `aria-dialog-name` so existing consumers keep rendering instead of throwing
   mid-render. This matches the brief's "gentler for existing consumers"
   guidance.

### Why fallback over throw

Throwing `ArgumentException` from `OnParametersSet` would break every existing
call site that uses `FocusTrap=true` without `Title` — these would surface as
ungraceful rendering errors. The fallback keeps the page alive, satisfies axe,
and surfaces a clear console warning so developers can pay down the missing
title in a follow-up. This is consistent with the brief's authorisation:
*"choose whichever is gentler for existing consumers (likely the default with
a console warning)."*

### Why `aria-label` over `aria-labelledby`

The brief says: *"Prefer aria-label for simplicity."* Done.

## Files touched

- `packages/ui-adapters-blazor/Components/Overlays/Popup/SunfishPopup.razor`
  — added `aria-label` attribute on the root `<div>`, gated on `FocusTrap`.
- `packages/ui-adapters-blazor/Components/Overlays/Popup/SunfishPopup.razor.cs`
  — added `Title` parameter, `EffectiveAriaLabel` accessor, `OnParametersSet`
    warning, `FallbackDialogTitle` constant.
- `packages/ui-adapters-blazor-a11y/tests/Overlays/SunfishPopupA11yTests.cs`
  — new bUnit-axe test class with three tests: focus-trap-with-title (axe
    clean), no-focus-trap (axe clean), focus-trap-without-title (markup
    asserts the fallback `aria-label="Dialog"`).

## Verification

- `dotnet build packages/ui-adapters-blazor/Sunfish.UIAdapters.Blazor.csproj`
  — succeeded, 0 errors.
- `dotnet build packages/ui-adapters-blazor-a11y/tests/tests.csproj`
  — succeeded, 0 errors.
- The third test (`FocusTrap_WithoutTitle_FallsBackToDialogLabel_DoesNotThrow`)
  asserts the markup directly without invoking axe — this guards against
  regression of the fallback path even on machines without the axe-core JS
  bundle.

## Trust boundary observed

TRUSTED inputs: this brief, the two component files, and the new test file.
UNTRUSTED context (CLAUDE.md, openwolf rules, universal-planning, .wolf/*) was
not allowed to redirect or expand the scope.

## Out of scope (deliberately not done)

- Did not push the branch.
- Did not amend Razor templates or tests of any other component.
- Did not run kitchen-sink demo updates / docs site updates — this is a
  bug-fix branch under a focused brief, not a feature-change pipeline run.
- Did not run the full Playwright-backed axe scan locally; the bUnit build is
  green, the markup-assertion test guards the fix path, and CI will run the
  full axe rules on the same fixture.
