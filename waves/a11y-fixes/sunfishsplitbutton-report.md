# SunfishSplitButton a11y fix — Wave 1 Plan 4 Buttons cascade

**Verdict:** GREEN
**Branch:** `fix/a11y-sunfishsplitbutton-button-name`
**Code commit:** `3308842ae643f75bd94af07122617a80c947640b`
**Token:** `fix-a11y-sunfishsplitbutton`

---

## Bug

`axe button-name (Critical)` — the chevron-only secondary button rendered
by `SunfishSplitButton` had no accessible name. Screen readers announced it
as an unlabeled "button", giving users no signal that it opens a menu.

Caught by the Wave 1 Plan 4 Buttons cascade. The two axe tests in
`SunfishSplitButtonA11yTests.cs` were `[Fact(Skip = …)]`-disabled to keep
the cascade green while the fix was queued.

## Fix

`packages/ui-adapters-blazor/Components/Buttons/SunfishSplitButton.razor`

1. Added a new public parameter:

   ```csharp
   [Parameter] public string DropdownAriaLabel { get; set; } = "More options";
   ```

2. Applied it as `aria-label` on the chevron-only secondary `<button>`:

   ```razor
   <button …
           aria-label="@DropdownAriaLabel"
           aria-haspopup="true"
           aria-expanded="@_menuOpen.ToString().ToLowerInvariant()"
           @onclick="ToggleMenu">
       @IconProvider.GetIconMarkup("chevron-down", IconSize.Small)
   </button>
   ```

The default `"More options"` is the conventional name for an icon-only
dropdown trigger paired with a primary action; consumers override per
context (e.g. `"Save options"`, `"More export formats"`).

The primary button keeps its existing `aria-label="@AriaLabel"` binding
unchanged; the two are independent so the dropdown trigger gets a name
even when consumers don't set one on the primary action.

## Test re-enable

`packages/ui-adapters-blazor-a11y/tests/Buttons/SunfishSplitButtonA11yTests.cs`

Removed `Skip = "axe violation: button-name on dropdown trigger — see report"`
from both:

- `SunfishSplitButton_Default_HasNoAxeViolations`
- `SunfishSplitButton_Disabled_HasNoAxeViolations`

Updated the trailing comment on each so future readers see the resolution.

## Verification

- `dotnet build packages/ui-adapters-blazor/Sunfish.UIAdapters.Blazor.csproj`
  → **Build succeeded. 0 Error(s).**
- `dotnet test packages/ui-adapters-blazor-a11y/tests/tests.csproj --filter "FullyQualifiedName~SunfishSplitButton"`
  → **Passed! Failed: 0, Passed: 2, Skipped: 0, Total: 2.**

Both previously-skipped axe runs now pass — the chevron button has an
accessible name in both enabled and disabled states.

## Diff shape

Exactly two files modified, both inside the trusted boundary:

```
packages/ui-adapters-blazor/Components/Buttons/SunfishSplitButton.razor   (+2 −1)
packages/ui-adapters-blazor-a11y/tests/Buttons/SunfishSplitButtonA11yTests.cs (+6 −6)
```

No `.csproj` touched. No sibling components touched.
