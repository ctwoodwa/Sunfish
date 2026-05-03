# Wave 1 Plan 4 Cluster A — Buttons a11y cascade

**Sentinel:** wave-1-plan4-buttons-sentinel
**Branch:** `global-ux/wave-1-plan4-buttons-a11y`
**Code commit:** `c17e708a`
**Verdict:** **YELLOW** — pattern cascade succeeded, suite green, but 5 real a11y violations were uncovered in 2 components and recorded as Skip-pending fixes.

---

## Scope reconciliation

The brief assumed 15 components in `packages/ui-adapters-blazor/Components/Buttons/`. Inventory of the folder yielded **11 distinct `.razor` components** (the 15-count appears to have been inflated by the `.razor.cs` partial-class files). All 11 components are covered by new test files — the cascade is complete for the actual component surface, not for a phantom 15.

| # | Component | Test file | Tests | Outcome |
|---|---|---|---|---|
| 1 | `SunfishButton` | `SunfishButtonA11yTests.cs` | 4 (`[Theory]` × variant × enabled) | 4 pass |
| 2 | `SunfishIconButton` | `SunfishIconButtonA11yTests.cs` | 4 (`[Theory]` × size × enabled) | 4 pass |
| 3 | `SunfishToggleButton` | `SunfishToggleButtonA11yTests.cs` | 2 (`[Theory]` selected/unselected) | 2 pass |
| 4 | `SunfishFab` | `SunfishFabA11yTests.cs` | 2 (icon-only + icon+text) | 2 pass |
| 5 | `SunfishSplitButton` | `SunfishSplitButtonA11yTests.cs` | 2 (default + disabled) | **2 SKIP** (real bug) |
| 6 | `SunfishSegmentedControl` | `SunfishSegmentedControlA11yTests.cs` | 3 (`[Theory]` per selected item) | 3 pass |
| 7 | `SunfishButtonGroup` | `SunfishButtonGroupA11yTests.cs` | 1 (with children) | 1 pass |
| 8 | `ButtonGroupButton` | `ButtonGroupButtonA11yTests.cs` | 2 (`[Theory]` enabled/disabled) | 2 pass |
| 9 | `ButtonGroupToggleButton` | `ButtonGroupToggleButtonA11yTests.cs` | 2 (`[Theory]` selected/unselected) | 2 pass |
| 10 | `SunfishChip` | `SunfishChipA11yTests.cs` | 3 (default + selected + removable) | **3 SKIP** (real bugs) |
| 11 | `SunfishChipSet<T>` | `SunfishChipSetA11yTests.cs` | 3 (`[Theory]` × selection mode) | 3 pass |
|   |   | **Total** | **28 tests** | **23 pass / 5 skip / 0 fail** |

11 component tests files written exclusively under `packages/ui-adapters-blazor-a11y/tests/Buttons/`. No edits to existing test files. No edits to `.csproj`. No edits to the components themselves.

---

## Pattern cascade

Each test file follows the proven template established by the SyncState pilots:

1. `IClassFixture<Ctx>` providing one shared `BunitContext` with `ISunfishCssProvider` / `ISunfishIconProvider` / `ISunfishThemeService` substituted via NSubstitute.
2. `Ctx` exposes `NewPageAsync()` that delegates to the shared `PlaywrightPageHost.GetAsync()`, returning a fresh `IPage` per test.
3. Each test renders the component via `Bunit.Render<T>(...)`, captures `rendered.Markup`, runs `await AxeRunner.RunAxeAsync(markup, page)`, and asserts the moderate-and-above filter is empty.

The structural divergence from the SyncState contract tests is intentional: contract tests assert specific ARIA attributes (role/aria-live shape per state); the cascade harness asserts axe-clean rendering (broader, structural). Both layers compose — the cascade catches what the contract didn't think to assert.

---

## A11y bugs found

The cascade caught **two distinct real bugs** in production components. Per the brief, these are documented here and the affected tests are `[Fact(Skip = ...)]` — the components are out of scope to fix in this wave.

### Bug #1: `SunfishSplitButton` — chevron-only secondary button has no accessible name

**Axe rule:** `button-name` (Critical)
**File:** `packages/ui-adapters-blazor/Components/Buttons/SunfishSplitButton.razor`
**Defect:** The dropdown-trigger button (the chevron at the right of the split button) renders only an icon; the markup gives the icon no `aria-label` and the button no accessible name. Screen readers will announce it as "button" with no purpose.
**Fix recommendation:** Add `aria-label="Open menu"` (or accept a `MenuAriaLabel` parameter that defaults to a localizable canonical phrase, mirroring `SunfishIconButton`'s pattern of requiring the consumer to supply the accessible name).
**Skipped tests:** `SunfishSplitButton_Default_HasNoAxeViolations`, `SunfishSplitButton_Disabled_HasNoAxeViolations`.

### Bug #2: `SunfishChip` — `role="option"` outside a `role="listbox"`, plus nested-interactive on the remove button

**File:** `packages/ui-adapters-blazor/Components/Buttons/Chip/SunfishChip.razor`

Three layered axe violations on the same component:

1. **`aria-required-parent` (Critical).** `SunfishChip` unconditionally renders `role="option"`, but ARIA mandates `role="option"` only be used inside `role="listbox"`/`role="combobox"`/`role="tree"`/etc. As a stand-alone chip, this is invalid. The `SunfishChipSet` parent renders `role="listbox"`, so chips inside the set are conformant; chips used directly are not.
   **Fix recommendation:** either (a) drop `role="option"` when no parent listbox is present (cascade the role from `SunfishChipSet` instead of hard-coding it on the chip), or (b) document that `SunfishChip` MUST be a child of `SunfishChipSet` and enforce via a cascading parameter check.

2. **`nested-interactive` (Serious).** The chip's outer `<span>` carries `@onclick` (interactive), and when `Removable=true` it contains a child `<button>` for remove. ARIA forbids interactive controls inside another interactive control — focus, keyboard, and screen-reader semantics break.
   **Fix recommendation:** make the chip itself a `<button>` (not a `<span>` with onclick), and either move the remove control out of the chip (sibling pattern) or change the outer wrapper to a non-interactive presentation when removable.

3. **`target-size` (Serious).** The remove button (`&times;`) is too small to satisfy WCAG 2.2 target-size (24×24 CSS px minimum, with adequate spacing).
   **Fix recommendation:** apply minimum 24×24 hit target with appropriate padding; or render remove as a separate adjacent button with adequate sizing.

**Skipped tests:** `SunfishChip_Default_HasNoAxeViolations`, `SunfishChip_Selected_HasNoAxeViolations`, `SunfishChip_Removable_HasNoAxeViolations` (the last test fails on all three rules together).

These violations existed before this wave; the harness simply surfaced them. Recommend opening a follow-on bug ticket per component (or one shared ticket for "Buttons folder a11y remediation"), then re-enable the skipped tests once the underlying components are fixed.

---

## `dotnet test` evidence

Final filtered run after Skip annotations:

```
$ dotnet test packages/ui-adapters-blazor-a11y/tests/tests.csproj --filter "FullyQualifiedName~Buttons"
...
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.32]     ...SunfishSplitButtonA11yTests.SunfishSplitButton_Disabled_HasNoAxeViolations [SKIP]
[xUnit.net 00:00:00.33]     ...SunfishChipA11yTests.SunfishChip_Default_HasNoAxeViolations [SKIP]
[xUnit.net 00:00:00.33]     ...SunfishSplitButtonA11yTests.SunfishSplitButton_Default_HasNoAxeViolations [SKIP]
[xUnit.net 00:00:00.33]     ...SunfishChipA11yTests.SunfishChip_Selected_HasNoAxeViolations [SKIP]
[xUnit.net 00:00:00.33]     ...SunfishChipA11yTests.SunfishChip_Removable_HasNoAxeViolations [SKIP]
  Skipped ...SunfishSplitButton_Disabled_HasNoAxeViolations [1 ms]
  Skipped ...SunfishChip_Default_HasNoAxeViolations [1 ms]
  Skipped ...SunfishSplitButton_Default_HasNoAxeViolations [1 ms]
  Skipped ...SunfishChip_Selected_HasNoAxeViolations [1 ms]
  Skipped ...SunfishChip_Removable_HasNoAxeViolations [1 ms]

Passed!  - Failed:     0, Passed:    23, Skipped:     5, Total:    28, Duration: 2 s - Sunfish.UIAdapters.Blazor.A11y.Tests.dll (net11.0)
```

Initial run (before Skip annotations) confirmed the bugs are real, not false positives:

```
Failed!  - Failed:     5, Passed:    23, Skipped:     0, Total:    28
```

Failed tests reported the precise axe rule, impact level, and helpUrl (e.g., `aria-required-parent`, Critical, `https://dequeuniversity.com/rules/axe/4.11/aria-required-parent`). All 5 failures are explained above; none are flaky or harness-induced.

---

## Build evidence

```
$ dotnet build packages/ui-adapters-blazor-a11y/Sunfish.UIAdapters.Blazor.A11y.csproj
  Sunfish.UIAdapters.Blazor.A11y -> ...\bin\Debug\net11.0\Sunfish.UIAdapters.Blazor.A11y.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Deferrals & pattern divergences

- **Composite parent–child rendering for ButtonGroup children.** `ButtonGroupButton` and `ButtonGroupToggleButton` were tested standalone (without a `SunfishButtonGroup` cascade) because bUnit fully supports the standalone path and the standalone markup is what axe is asserting against. The `SunfishButtonGroup` test covers the composite case (one `[Fact]` rendering three child buttons inside the group). Splitting like this avoided introducing dependent fixture state and matches the `NodeHealthBarContractTests` strategy of putting composite assertions on the parent component test.
- **`SunfishChipSet<T>` generic.** Closed over `string` since the harness only needs to assert markup conformance, not selection semantics. Future waves can cover other `TItem` shapes if needed.
- **No keyboard/focus assertions.** This wave is scoped to axe static-markup conformance only. Keyboard-trap and focus-management tests live in `AssertionTests.cs` and are component-orthogonal helpers; per-component keyboard tests can be a future wave.
- **No theme-CSS injection.** Tests use `AxeRunner.RunAxeAsync(markup, page)` with no `ThemeCss`, so axe's `color-contrast` rule is largely inapplicable (no real colors to evaluate). Color-contrast cascade is a deferred wave that needs the theme CSS pipeline plumbed into the harness.

---

## Verdict

**YELLOW.** The cascade pattern is proven and reusable for Buttons, but found 5 real a11y violations across 2 components (SunfishSplitButton, SunfishChip) that need separate component fixes. The test suite remains green via documented `[Fact(Skip = ...)]` annotations so the cascade can land without blocking on component remediation.
