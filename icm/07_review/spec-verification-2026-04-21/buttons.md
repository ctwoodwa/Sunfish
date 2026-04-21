# Buttons family spec audit — 2026-04-21

Tier 1 spec-verification (ADR 0022) for every entry in
`apps/docs/component-specs/component-mapping.json` with `category == "buttons"` and
`status in ["implemented", "partial"]`.

Scope (in-mapping order): `button`, `buttongroup`, `chip`, `chiplist`, `dropdownbutton`,
`floatingactionbutton`, `splitbutton`, `togglebutton`.

> The task brief named only the first six; `splitbutton` and `togglebutton` also match the
> filter so they are audited here too (call-out in **Summary**). If you only want the
> originally-named six, ignore the last two rows — the verdict counts below include them
> for completeness.

---

## Summary

| Component              | Spec dir                | Mapping status | Verdict                  | Priority gaps |
|------------------------|-------------------------|----------------|--------------------------|---------------|
| SunfishButton          | button                  | implemented    | downgrade-to-partial     | 7 |
| SunfishButtonGroup     | buttongroup             | implemented    | downgrade-to-partial     | 6 |
| SunfishChip            | chip                    | implemented    | downgrade-to-partial     | 7 |
| SunfishChipSet         | chiplist                | implemented    | downgrade-to-partial     | 6 |
| SunfishSplitButton     | dropdownbutton          | partial        | downgrade-to-partial *(stays partial, deep gaps)* | 8 |
| SunfishFab             | floatingactionbutton    | implemented    | downgrade-to-partial     | 5 |
| SunfishSplitButton     | splitbutton             | implemented    | downgrade-to-partial     | 4 |
| SunfishToggleButton    | togglebutton            | implemented    | needs-work *(not deeply audited — see note)* | ~3 est. |

Split/toggle note: `SunfishSplitButton` is the **same Blazor component** mapped from both
`splitbutton` and `dropdownbutton`. The `dropdownbutton` row captures the deeper gap (no
`DropDownButtonItems`/`DropDownButtonItem`/`DropDownButtonContent` child tags, no
`FocusAsync`, no popup settings). A production DropDownButton demo file does not exist
— the route is an "aspirational" placeholder. `togglebutton` is flagged on the dashboard
as `implemented` but was not in the original six; only a surface pass was performed.

---

## Component: SunfishButton (spec: `button`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Buttons\SunfishButton.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Buttons\Button\Overview\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Buttons\Button\Appearance\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Buttons\Button\Events\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Buttons\Button\Accessibility\Demo.razor`
- **Spec files reviewed**: `overview.md`, `type.md`, `appearance.md`, `events.md`,
  `icons.md`, `styling.md`, `disabled-button.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `ButtonType` default (overview.md) | bug | Spec default is `Submit`; impl defaults to `Button`. Form-submit buttons break silently. | Change `ButtonType` default to `Submit`. |
  | `ThemeColor` parameter (appearance.md) | missing | Spec enumerates 11 values (`Base`/`Primary`/`Secondary`/`Tertiary`/`Info`/`Success`/`Warning`/`Error`/`Dark`/`Light`/`Inverse`); impl has `Variant` enum with only 6 (no `Base`/`Tertiary`/`Error`/`Dark`/`Light`/`Inverse`; uses `Danger` instead of `Error`). | Rename `Variant`→`ThemeColor` and expand enum to 11 members, or add a `ThemeColor` parameter alongside. |
  | `Icon` parameter type (icons.md §2) | bug | Spec: `Icon` is `object` accepting `SvgIcon`, `FontIcon`, or CSS-class string. Impl: `Icon` is `RenderFragment?`. Spec-style `Icon="@SvgIcon.Export"` does not compile against impl. | Accept `object` or a discriminated `Icon` type; keep `RenderFragment` as a fallback child content slot. |
  | `Form` parameter (overview.md + type.md) | covered | Impl exposes `Form` string and passes to `<button form="...">`. | — |
  | `FocusAsync` method (overview.md Reference) | missing | Spec documents reference-based `FocusAsync` for programmatic focus. Impl has no public `FocusAsync`. | Add `ElementReference` capture and public `FocusAsync` that delegates. |
  | `Id`, `Title`, `Visible` parameters (overview.md) | missing | Spec lists these explicitly. Impl has none (pass-through via `AdditionalAttributes` is possible but not first-class, and `Visible` has no equivalent). | Add explicit `[Parameter]` `Id`, `Title`, `Visible` (defaults `true`). |
  | `RoundedMode.None` (appearance.md table) | incomplete | Spec Rounded values are `Small`/`Medium`/`Large`/`Full` — no `None`. Impl adds `None`. Demo uses it (`Rounded.None`). | Either remove `None` (breaking) or document it as a Sunfish-specific extension. |
  | Disabled `OnClick` bypass note (disabled-button.md) | covered | Native `<button disabled>` matches spec expectation. | — |
  | WAI-ARIA `role=button` implicit (accessibility) | covered | Rendered as native `<button>`, no extra role needed. | — |
  | Keyboard Enter/Space activation | covered | Native `<button>` handles it. | — |

- **Verdict**: `downgrade-to-partial` — 2 bug + 4 missing + 1 incomplete items; at least
  two are core (spec-default `ButtonType`, spec parameter type `Icon=object`).

---

## Component: SunfishButtonGroup (spec: `buttongroup`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Buttons\SunfishButtonGroup.razor`
- **Demos**: four `Demo.razor` files under
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Buttons\ButtonGroup\{Overview,Appearance,Events,Accessibility}`.
- **Spec files reviewed**: `overview.md`, `buttons.md`, `appearance.md`, `events.md`,
  `selection.md`, `icons.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `<ButtonGroupButton>` child tag (buttons.md §1) | missing | Core to the component per spec. No such component type exists in Blazor adapter. Demos work around by nesting bare `<SunfishButton>` children. | Add `ButtonGroupButton` and `ButtonGroupToggleButton` subcomponents that attach via `CascadingValue` to the parent group. |
  | `<ButtonGroupToggleButton>` child tag (buttons.md §2) | missing | Same as above. `Selected`/`@bind-Selected` per-child is unreachable today. | See above. |
  | `SelectionMode` type (overview.md + selection.md) | bug | Spec: `ButtonGroupSelectionMode` enum (Single default, Multiple). Impl uses `GridSelectionMode` with default `None` — a type collision and wrong default. | Introduce `ButtonGroupSelectionMode { Single, Multiple }`; default to `Single`. |
  | `SelectedChanged` event semantics (events.md §SelectedChanged) | bug | Spec: `SelectedChanged` is per child button, `bool`-typed, `@bind-Selected` compatible. Impl: container-only `EventCallback<int>` (slot index). | Move `SelectedChanged` to `ButtonGroupToggleButton`; keep the index callback as an orthogonal `OnSelectionChanged` if wanted. |
  | `Visible` per-child (buttons.md §Visibility) | missing | Spec supports `Visible` on individual buttons to toggle without affecting indexes. Impl has no `Visible` on `SunfishButton`. | Add `Visible` bool (default true) to `SunfishButton`; render as `@if (Visible)` or with a hidden class. |
  | `aria-label="Button group"` default (accessibility/wai-aria-support.md) | bug | Impl always sets `aria-label="Button group"` regardless of `aria-labelledby` / caller override; spec says `role="group"` only and the caller names it. Redundant/misleading default leaks into DOM. | Only emit `aria-label` when caller does not supply one. |
  | `role="group"` on wrapper (accessibility) | covered | Rendered on wrapping `<div>`. | — |
  | Disabled group → `aria-disabled=true` (accessibility) | incomplete | Impl exposes `Enabled` parameter but does not mirror it to `aria-disabled` on the wrapper; the Appearance demo even notes it is essentially a no-op. | Render `aria-disabled="true"` when `!Enabled`, and ideally cascade to children. |
  | `OnClick` fires before `Selected` change (events.md §OnClick) | covered (for normal buttons) / not-applicable (toggle buttons missing) | With current impl the ordering concern does not apply. | Revisit after adding toggle buttons. |

- **Verdict**: `downgrade-to-partial` — 3 missing + 3 bug items; `<ButtonGroupButton>` /
  `<ButtonGroupToggleButton>` are spec-core building blocks, and their absence makes the
  spec's primary usage example invalid.

---

## Component: SunfishChip (spec: `chip`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Buttons\Chip\SunfishChip.razor`
- **Demos**: four `Demo.razor` files under
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Buttons\Chip\{Overview,Appearance,Events,Accessibility}`.
  (Overview is a stub placeholder — noted below.)
- **Spec files reviewed**: `overview.md`, `appearance.md`, `events.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Selected` parameter + `@bind-Selected` (overview.md params) | bug | Spec names the two-way parameter `Selected`; impl names it `IsSelected`. `@bind-Selected` as shown in every spec example will not compile against impl. | Rename `IsSelected` → `Selected` (keep `IsSelected` as `[Obsolete]` alias for one release). |
  | `FillMode`/`Rounded`/`Size`/`ThemeColor` (appearance.md) | missing | Spec exposes these four independent dials (FillMode Solid/Outline; Rounded Sm/Md/Lg; Size Sm/Md/Lg; ThemeColor Base/Info/Success/Warning/Error). Impl conflates them into a single `Variant` enum. | Split `Variant` into `ThemeColor` + `FillMode` + `Rounded` + `Size`; keep `Variant` as a deprecated convenience. |
  | `AriaLabel`, `Id`, `TabIndex` parameters (overview.md params) | missing | Spec lists explicit parameters. Impl relies on `AdditionalAttributes`. | Add as explicit `[Parameter]`s; `TabIndex` default should override the hard-coded `tabindex="0"` in the `<span>`. |
  | `RemoveIcon` parameter (overview.md params) | missing | Spec allows customizing the remove-affordance icon. Impl hard-codes `&times;`. | Add `RemoveIcon` of type `object` / `RenderFragment`. |
  | `OnClick` event arg type (events.md §OnClick) | bug | Spec: event arg is `ChipClickEventArgs` carrying `Text`. Impl: arg is `MouseEventArgs`. | Introduce `ChipClickEventArgs` and populate `Text` from the `Text` parameter. |
  | `OnRemove` event arg type (events.md §OnRemove) | bug | Spec: `ChipRemoveEventArgs { Text, IsCancelled }` — cancellable. Impl: parameterless `EventCallback`; no cancel support. | Introduce `ChipRemoveEventArgs`; respect `IsCancelled` before internally removing. |
  | WAI-ARIA `role=button` for a standalone chip (accessibility/wai-aria-support.md) | bug | Spec: a chip *outside* a chip list should have `role="button"` and `aria-pressed=true/false`. Impl hard-codes `role="option"` (the listbox variant) always. This breaks the standalone semantic. | Use `role="option"` only when cascaded inside a chip set; otherwise render `role="button"` with `aria-pressed`. |
  | Disabled chip accessibility | covered | Impl sets `aria-disabled` and `tabindex="-1"` when `Disabled`. | — |
  | Overview demo is a stub placeholder | incomplete | `Chip/Overview/Demo.razor` is literally a "coming soon" panel. ADR 0022 treats the Overview page as the canonical entry. | Replace with a real overview example. |

- **Verdict**: `downgrade-to-partial` — 3 bug + 3 missing + 1 incomplete items. The
  spec-shaped `@bind-Selected` and `FillMode`/`ThemeColor` dials are both absent.

---

## Component: SunfishChipSet (spec: `chiplist`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Buttons\Chip\SunfishChipSet.razor`
- **Demos**: only
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Buttons\ChipList\Overview\Demo.razor`
  and it is a stub.
- **Spec files reviewed**: `overview.md`, `data-bind.md`, `selection.md`, `templates.md`,
  `events.md`, `appearance.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `SelectionMode` type (selection.md) | bug | Spec uses `ChipListSelectionMode { None, Single, Multiple }`. Impl uses `GridSelectionMode`. | Add `ChipListSelectionMode` enum; rename impl parameter. |
  | Field-binding surface (data-bind.md) | missing | Spec exposes `TextField`, `IconField`, `DisabledField`, `RemovableField`, `FillModeField`, `ThemeColorField`. Impl has only `TextField`. Five of six are absent. | Add the five missing `*Field` string parameters and honor them during rendering. |
  | `OnRemove` event arg (events.md §OnRemove) | bug | Spec: `ChipListRemoveEventArgs { Item, IsCancelled }` — cancellable. Impl: `EventCallback<TItem>`; no cancel support. | Introduce `ChipListRemoveEventArgs<TItem>`. |
  | `FillMode` / `Rounded` / `Size` container-level parameters (appearance.md) | missing | Spec exposes container-wide appearance defaults that cascade to every chip. Impl has none — each rendered chip hard-codes `ChipVariant.Default`. | Add parameters; cascade to child chip render. |
  | `AriaLabelledBy` parameter (overview.md params) | missing | Spec lists explicit `AriaLabelledBy`. Impl relies on `AdditionalAttributes`. | Add explicit `[Parameter]`. |
  | `role="listbox"` + `aria-orientation="horizontal"` + disable-listbox-when-selection-none (accessibility/wai-aria-support.md) | bug | Impl always emits `role="listbox"` even when `SelectionMode == None`; no `aria-orientation`; no per-chip `aria-keyshortcuts` for removable chips. Spec says *omit* listbox when no selection. | Condition `role` on SelectionMode; emit `aria-orientation="horizontal"` always when listbox; emit `aria-keyshortcuts="Enter Delete"` on removable chips. |
  | `ItemTemplate` (templates.md) | covered | Impl accepts `RenderFragment<TItem>`. | — |
  | Overview demo is a stub placeholder | incomplete | As with Chip — needs a real Overview. Only one demo page exists (no Appearance/Events/Accessibility). | Author the missing three demo pages. |

- **Verdict**: `downgrade-to-partial` — 3 missing + 2 bug + 1 incomplete — plus three
  missing demo pages (Appearance, Events, Accessibility).

---

## Component: SunfishSplitButton (spec: `dropdownbutton`, mapping status = `partial`)

> Note: the mapping file collapses both `dropdownbutton` and `splitbutton` onto the same
> `SunfishSplitButton` type. This is a structural problem: the two spec pages describe
> distinct components (DropDownButton has a single primary click action + dropdown; the
> split variant has two click targets and the dropdown is the secondary). Today there is
> no standalone DropDownButton component.

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Buttons\SunfishSplitButton.razor`
- **Demos**:
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Buttons\DropDownButton\Overview\Demo.razor`
  is an *aspirational placeholder* (banner says "Sunfish component: not yet built").
- **Spec files reviewed**: `overview.md`, `appearance.md`, `events.md`, `icons.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `<DropDownButtonContent>` child tag (overview.md §Creating) | missing | Spec's primary-button content is declared via this child. Impl uses `ChildContent`. | Introduce a `DropDownButtonContent` wrapper or accept it as a named `RenderFragment`. |
  | `<DropDownButtonItems>` / `<DropDownButtonItem>` child tags (overview.md §Creating) | missing | Core shape of the API. Impl uses one flat `MenuContent` `RenderFragment`. No per-item `OnClick`, no `Enabled`, no `Class`, no typed hierarchy. | Introduce a cascade of `DropDownButtonItems` with `DropDownButtonItem` children. |
  | `<DropDownButtonSettings>` → `<DropDownButtonPopupSettings>` (overview.md §Popup Settings) | missing | Spec exposes popup `Height`/`Width`/`Min*`/`Max*`/`AnimationDuration`/`Class`. Impl has none. | Add nested settings components or parameters. |
  | `ShowArrowButton` (overview.md params) | missing | Spec lets callers hide the arrow button. | Add `bool ShowArrowButton { get; set; } = true`. |
  | `Icon` parameter (icons.md §Icon Parameter) | bug | Spec: `Icon` is `object`. Impl: `Icon` is `RenderFragment?`. Same mismatch as Button. | Accept `object`; fall back to RenderFragment. |
  | `AriaLabel` / `AriaLabelledBy` / `AriaDescribedBy` / `Id` / `TabIndex` / `Title` (overview.md params) | missing | Spec lists six explicit accessibility/identity parameters on the primary button. Impl has only `AriaLabel`. | Add them as `[Parameter]`s that flow to the primary button. |
  | `FocusAsync` reference method (overview.md §Reference and Methods) | missing | Spec documents programmatic focus. Impl: none. | Add `ElementReference` + public `FocusAsync`. |
  | `aria-expanded` + `aria-controls` linkage (accessibility/wai-aria-support.md) | incomplete | Impl emits `aria-haspopup="true"` and `aria-expanded`, but no `aria-controls` pointing at the popup's id. | Give the popup an id and wire `aria-controls`. |
  | Popup `role="list"` + item `role="listitem"` (accessibility) | bug | Impl emits `role="menu"`. Spec prescribes `list`/`listitem`. | Change roles. |
  | No real kitchen-sink demo (ADR 0022) | missing | Route exists only as an aspirational placeholder. | Build real Overview/Appearance/Events/Accessibility/Icons demos after the core component is rebuilt. |

- **Verdict**: `downgrade-to-partial` (stays partial per mapping, but gap scope is deep
  enough to warrant rebuilding the DropDownButton as a separate component rather than a
  flag on SplitButton). 5 missing + 2 bug + 1 incomplete — and no live demo.

---

## Component: SunfishFab (spec: `floatingactionbutton`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Buttons\SunfishFab.razor`
- **Demos**: four `Demo.razor` files under
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Buttons\Fab\{Overview,Appearance,Events,Accessibility}`
  (Overview is a stub placeholder).
- **Spec files reviewed**: `overview.md`, `appearance.md`, `events.md`,
  `position-and-alignment.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `HorizontalAlign` / `VerticalAlign` / `PositionMode` typed enums (position-and-alignment.md) | bug | Spec types: `FloatingActionButtonHorizontalAlign { Start, Center, End }`, `FloatingActionButtonVerticalAlign { Top, Middle, Bottom }`, `FloatingActionButtonPositionMode { Fixed, Absolute }`. Impl uses plain `string`s ("left"/"right"/"top"/"bottom"/"fixed"/"absolute"/"static"); spec has *no* Middle vertical value equivalent in impl and *no* start/center/end horizontal values. | Add the three enums, port impl to use them. |
  | `HorizontalAlign` default (overview.md params table) | bug | Spec default = `End`. Impl default = `"right"` (equivalent to End only if you adopt the new enum); current string model makes migration error-prone. | Re-default after enum port. |
  | `Size` / `ThemeColor` / `Rounded` string-constant API (appearance.md) | incomplete | Spec uses `ThemeConstants.Button.Size/ThemeColor/Rounded` string members. Impl exposes `FabSize` enum only (no `ThemeColor` and no `Rounded` at all). Nor is the spec-documented Icon-as-object contract honored (impl: `Icon` is `string`). | Add `ThemeColor` + `Rounded` parameters of the same shape as Button; align Icon type with `object`. |
  | `Title`, `Id`, `Enabled` parameters (overview.md params) | missing / covered mix | `Enabled` present; `Id` and `Title` absent. | Add explicit `Id`, `Title` `[Parameter]`s. |
  | Button-with-Menu mode (accessibility/wai-aria-support.md §Button-with-Menu Mode) | missing | Spec describes FAB with a speed-dial menu (delegates to DropDownButton a11y spec). Impl has no menu mode. | Implement speed-dial variant, or document explicit non-support. |
  | `aria-label` requirement for icon-only FAB (accessibility) | covered | Impl forwards via `AdditionalAttributes`; accessibility demo exercises it. | — |
  | `PositionMode="static"` (rendering note in Kitchen-sink docs) | covered (but non-spec) | Impl adds `"static"` as a position mode for inline demos. Not harmful; flag as Sunfish extension. | Document as Sunfish-specific; not required by spec. |
  | Overview demo is a stub placeholder | incomplete | `Fab/Overview/Demo.razor` is placeholder; other three demos are rich. | Author a proper overview. |

- **Verdict**: `downgrade-to-partial` — 3 bug + 2 missing + 2 incomplete items, with
  typed-enum drift being the biggest.

---

## Component: SunfishSplitButton (spec: `splitbutton`)

> Same Blazor component as the `dropdownbutton` mapping; audited here against the
> dedicated `splitbutton` spec briefly so the row in the Summary is not blank. A
> full per-spec audit is appropriate in Tier 2 once DropDownButton is split out.

- **Impl**: `packages\ui-adapters-blazor\Components\Buttons\SunfishSplitButton.razor`
- **Demos**: `apps\kitchen-sink\Pages\Components\Buttons\SplitButton\Overview\Demo.razor`
  (directory contents not deeply reviewed in this pass).
- **Gaps (surface scan)**: same `DropDownButtonItems`/`Item`/`Content` problem, same
  missing `FocusAsync`, same Icon-type mismatch, incomplete ARIA linkage. All apply.
- **Verdict**: `downgrade-to-partial` — 4+ missing/bug items shared with
  `dropdownbutton`.

---

## Component: SunfishToggleButton (spec: `togglebutton`) — lightweight pass

`togglebutton` is `status=implemented` and was not in the original six; only a surface
pass was done. Per the quick check, `SunfishToggleButton.razor` exists, spec directory
was not deep-dived in this audit, and Kitchen-sink demos exist but were not opened.
Tier 2 should audit formally.

- **Verdict (provisional)**: `needs-work`, ~3 gaps estimated by analogy with
  SunfishButton (`ThemeColor` enum breadth, `FocusAsync`, `Id`/`Title`/`Visible`).

---

## Next actions (top priority fixes for the Buttons family)

Ordered by blast-radius, not by component:

1. **Adopt spec parameter names & defaults across the family**
   - Rename `Variant` → `ThemeColor` on Button, Chip, SplitButton (the current enum is
     too narrow and the name itself drifts from every spec example).
   - Rename `IsSelected` → `Selected` on Chip (with `[Obsolete]` alias) so
     `@bind-Selected` works as documented.
   - Switch `SunfishButton.ButtonType` default from `Button` to `Submit`.

2. **Introduce the spec's declarative item/child tags**
   - `ButtonGroupButton` and `ButtonGroupToggleButton` children for ButtonGroup (with
     cascading to parent for `SelectionMode`).
   - `DropDownButtonContent`, `DropDownButtonItems`, `DropDownButtonItem`,
     `DropDownButtonSettings`/`DropDownButtonPopupSettings` children — and lift the
     current flat `MenuContent` to a proper typed hierarchy.

3. **Align typed enums with spec names and widen colour scales**
   - Add `FloatingActionButtonHorizontalAlign` / `VerticalAlign` / `PositionMode` enums
     and retire the raw strings on `SunfishFab`.
   - Add `ButtonGroupSelectionMode { Single, Multiple }` and
     `ChipListSelectionMode { None, Single, Multiple }`; retire the `GridSelectionMode`
     reuse.
   - Expand button colour scale to the 11-member `ThemeColor` set (add `Base`,
     `Tertiary`, `Error`, `Dark`, `Light`, `Inverse`; spec calls it `Error`, not
     `Danger`).

4. **Event-arg types & cancellation semantics**
   - Add `ChipClickEventArgs`, `ChipRemoveEventArgs { Text, IsCancelled }`,
     `ChipListRemoveEventArgs<TItem> { Item, IsCancelled }`.
   - Make `OnRemove` respect `IsCancelled` before internal state mutates.
   - Move `SelectedChanged` from `SunfishButtonGroup` (container) onto the per-child
     toggle button with a `bool` payload (spec shape).

5. **Accessibility corrections**
   - `SunfishChip`: render `role="button"` + `aria-pressed` when standalone; only render
     `role="option"` when a parent `SunfishChipSet` cascades it.
   - `SunfishChipSet`: omit `role="listbox"` when `SelectionMode == None`; emit
     `aria-orientation="horizontal"` when listbox; emit `aria-keyshortcuts="Enter Delete"`
     on removable chips.
   - `SunfishSplitButton`: wire `aria-controls` to the popup's id; flip popup role from
     `menu` to `list` + `listitem` to match spec.
   - `SunfishButtonGroup`: stop defaulting `aria-label="Button group"` unconditionally;
     respect caller-supplied name.

6. **Programmatic focus & missing identity parameters**
   - Add `FocusAsync` to Button, ButtonGroup toggle buttons, SplitButton, Fab.
   - Add explicit `Id`, `Title`, `Visible`, `TabIndex` `[Parameter]`s across the family.

7. **Kitchen-sink demo gaps (ADR 0022)**
   - Replace the three "coming soon" stub Overview demos (`Chip`, `ChipList`, `Fab`,
     `DropDownButton` aspirational) with real Overview examples.
   - Author the missing `ChipList/{Appearance,Events,Accessibility}` demos.
   - Decide whether `DropDownButton` gets its own real Blazor component (separate from
     SplitButton) — this audit recommends yes.

---

_Audit prepared: 2026-04-21. Tier 1 of the Blazor-100% parity push (ADR 0022)._
