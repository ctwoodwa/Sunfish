# Forms family (half A — simple inputs & pickers) spec audit — 2026-04-21

Tier 1 spec-verification (ADR 0022) for every entry in
`apps/docs/component-specs/component-mapping.json` with `category == "forms"` and
`status in ["implemented", "partial"]`, restricted to the "half A" subset of simple
inputs and pickers (AutoComplete through TextBox/TextArea; excludes ComboBox,
DropDownTree, MultiColumnComboBox, MultiSelect, Signature, Slider, Switch, TimePicker,
Upload, Editor, Signature — those are Forms-B).

Scope (16 components, in-mapping order): `autocomplete`, `checkbox`, `dateinput`,
`datepicker`, `daterangepicker`, `datetimepicker`, `dropdownlist`, `fileselect`,
`floatinglabel`, `form`, `maskedtextbox`, `numerictextbox`, `radiogroup`, `rangeslider`,
`rating`, `textarea`, `textbox`.

> `dropdowntree` is in the Forms category but `status == "planned"` with
> `sunfish == null`, so it is correctly excluded from this Tier 1 pass.

---

## Summary

| Component                | Spec dir             | Mapping status | Verdict                  | Priority gaps |
|--------------------------|----------------------|----------------|--------------------------|---------------|
| SunfishAutocomplete      | autocomplete         | implemented    | needs-work               | 5 |
| SunfishCheckbox          | checkbox             | implemented    | downgrade-to-partial     | 4 |
| SunfishDateInput         | dateinput            | implemented    | downgrade-to-partial     | 12 |
| SunfishDatePicker        | datepicker           | implemented    | downgrade-to-partial     | 10 |
| SunfishDateRangePicker   | daterangepicker      | implemented    | verified                 | 2 |
| SunfishDateTimePicker    | datetimepicker       | implemented    | needs-work               | 4 |
| SunfishDropDownList      | dropdownlist         | implemented    | needs-work               | 7 |
| SunfishFileUpload        | fileselect           | implemented    | needs-work               | 5 |
| SunfishLabel             | floatinglabel        | implemented    | downgrade-to-partial     | 6 |
| SunfishForm              | form                 | implemented    | downgrade-to-partial     | 9 |
| SunfishMaskedInput       | maskedtextbox        | implemented    | downgrade-to-partial     | 8 |
| SunfishNumericInput      | numerictextbox       | implemented    | downgrade-to-partial     | 8 |
| SunfishRadioGroup        | radiogroup           | implemented    | downgrade-to-partial     | 6 |
| SunfishRangeSlider       | rangeslider          | implemented    | downgrade-to-partial     | 5 |
| SunfishRating            | rating               | implemented    | downgrade-to-partial     | 7 |
| SunfishTextArea          | textarea             | implemented    | needs-work               | 6 |
| SunfishTextBox           | textbox              | implemented    | needs-work               | 7 |

Demo-coverage note: Only seven of the sixteen components have all four canonical ADR
0022 demo pages (Overview + Accessibility + Appearance + Events): Autocomplete,
Checkbox, DatePicker, DropDownList, Rating, TextArea, TextBox. The other nine have
Overview-only demos and need three additional demo pages each.

---

## Component: SunfishAutocomplete (spec: `autocomplete`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishAutocomplete.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\Autocomplete\Overview\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\Autocomplete\Accessibility\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\Autocomplete\Appearance\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\Autocomplete\Events\Demo.razor`
- **Spec files reviewed**: `overview.md`, `events.md`, `appearance.md`, `templates.md`,
  `virtualization.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Imperative API (`OpenAsync`/`CloseAsync`/`Refresh`/`FocusAsync`) | missing | Spec lists four public reference methods. Impl exposes none. | Capture `ElementReference`; add public async methods that toggle internal `_isOpen` and invoke `FocusAsync()`. |
  | `Id`, `TabIndex`, `Title`, `InputMode`, `Name` parameters | missing | Spec lists explicit identity/input-shaping parameters. Impl relies on `AdditionalAttributes`. | Add explicit `[Parameter]`s; flow to the inner `<input>`. |
  | `LoaderShowDelay` (virtualization.md) | missing | Spec configures when the loading spinner shows during async paging. Impl has no loader delay. | Add `int LoaderShowDelay = 100` and gate the loader render behind a timer. |
  | `PopupSettings` child (overview.md §Popup Settings) | missing | Spec supports a nested `<AutocompletePopupSettings>` with `Height`/`Width`/`Class`/`AnimationDuration`. Impl hard-codes popup sizing. | Add a settings cascade. |
  | `ValueChanged` vs `ValueChange` semantics (events.md) | covered | Impl matches the spec two-way binding contract. | — |
  | Virtualization (`Virtual`, `ItemHeight`, `PageSize`) | covered | Impl supports virtualization (see `_virtualize`). | — |
  | ARIA `role="combobox"` + `aria-expanded`/`aria-controls`/`aria-activedescendant` | covered | Impl renders ARIA plumbing. | — |

- **Verdict**: `needs-work` — 4 missing parameter/method items plus one missing child
  component. No spec-typed bugs, but the imperative surface is absent and several
  spec-listed parameters are not first-class.

---

## Component: SunfishCheckbox (spec: `checkbox`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishCheckbox.razor`
- **Demos**: four `Demo.razor` files under
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\Checkbox\{Overview,Accessibility,Appearance,Events}`.
- **Spec files reviewed**: `overview.md`, `events.md`, `appearance.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Value` parameter + `@bind-Value` | bug | Spec's two-way parameter is named `Value` (bool). Impl names it `Checked`; `@bind-Value` in every spec example will not compile. | Rename `Checked`→`Value` (keep `Checked` as `[Obsolete]` alias for one release). |
  | `Id`, `TabIndex`, `Name` parameters | missing | Spec lists these explicitly. Impl has none as first-class. | Add `[Parameter]`s; flow to inner `<input type="checkbox">`. |
  | `Size` / `Rounded` / `ThemeColor` (appearance.md) | missing | Spec exposes the usual appearance dials. Impl has none. | Add parameters; render via `sf-checkbox--size-*` etc. |
  | `OnChange` vs `ValueChanged` semantics (events.md) | incomplete | Spec distinguishes: `ValueChanged` is the two-way callback; `OnChange` fires only on user gesture (not on programmatic set). Impl only has `CheckedChanged`. | Add a separate `EventCallback<ChangeEventArgs> OnChange`. |
  | `Indeterminate` + `IndeterminateChanged` (overview.md §Indeterminate) | covered | Impl renders the tri-state toggle. | — |
  | Keyboard Space activation, ARIA `role=checkbox`, `aria-checked="mixed"` | covered | Native `<input type="checkbox">` handles most of this; indeterminate renders `aria-checked="mixed"`. | — |

- **Verdict**: `downgrade-to-partial` — 1 bug (spec-mandated `Value` parameter name) +
  2 missing groups. `@bind-Value` compatibility is a documented spec contract and not
  optional.

---

## Component: SunfishDateInput (spec: `dateinput`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishDateInput.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\DateInput\Overview\Demo.razor`
    (Overview-only; missing Accessibility/Appearance/Events)
- **Spec files reviewed**: `overview.md`, `typing.md`, `formats.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Segment-based typing (typing.md entire section) | missing | Spec: DateInput renders *segmented* MM/DD/YYYY sub-fields with auto-switch, AutoSwitchKeys, AutoSwitchParts, TwoDigitYearMax. Impl uses a plain `<input type="date">` wrapper — zero segment awareness, no auto-switch, no key-steps. | Build a segmented-input primitive or render `<input type="text">` with caret + segment logic in JS interop. |
  | `Format` + `FormatPlaceholder` | missing | Spec lets the caller supply a display mask (e.g., `MM/dd/yyyy`) plus per-segment placeholder strings. Impl uses native browser date format only. | Add `Format` + `FormatPlaceholder` parameters and the render path. |
  | `DebounceDelay` | missing | Spec lists explicit `DebounceDelay` (int, default 150). Impl raises `ValueChanged` synchronously. | Add a timer-based debounce on typing. |
  | Arrow-key step (`ArrowStep`, `PickerStep`) | missing | Spec: up/down arrows increment the focused segment by configurable step. Impl relies on native browser behavior only. | Implement arrow-key handlers keyed to focused segment. |
  | `Id`, `TabIndex`, `Name`, `Title`, `AutoComplete`, `InputMode`, `AriaLabel`, `AriaLabelledBy` | missing | Spec lists eight explicit identity/accessibility parameters. Impl has none. | Add explicit `[Parameter]`s. |
  | `ShowClearButton` rendering | missing | Spec lets callers show a clear affordance. Impl has no clear button. | Add an inline clear button when `ShowClearButton && Value.HasValue`. |
  | `FocusAsync` reference method | missing | Spec documents programmatic focus. Impl has no `ElementReference` capture. | Capture `ElementReference` + public `FocusAsync`. |
  | `ValidateOn` (spec enum Input/Blur/Change) | missing | Spec exposes validation trigger. Impl has none. | Add enum + plumbing. |
  | `ReadOnly` vs `Disabled` semantics | incomplete | Impl has a `ReadOnly` parameter but doesn't render `readonly` on the underlying `<input>` in all modes. | Flow `ReadOnly` through; verify render. |
  | Min/Max boundary enforcement | covered | `Min`/`Max` exist. | — |
  | `role="spinbutton"` per segment (accessibility/wai-aria-support.md) | missing | Spec's segmented model requires per-segment ARIA `role="spinbutton"` with `aria-valuenow`/`aria-valuemin`/`aria-valuemax`. Impl: none (native date input). | After segmentation lands, emit per-segment ARIA. |
  | Overview-only demo (ADR 0022) | incomplete | No Accessibility/Appearance/Events demos exist. | Author the three missing demo pages. |

- **Verdict**: `downgrade-to-partial` — 9 missing + 1 incomplete + 1 missing-demo
  cluster. This is one of the two deepest regressions in Forms-A: the impl is
  essentially a native-input wrapper and shares almost none of the spec's segmented
  behavior.

---

## Component: SunfishDatePicker (spec: `datepicker`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishDatePicker.razor`
- **Demos**: four `Demo.razor` files under
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\DatePicker\{Overview,Accessibility,Appearance,Events}`.
- **Spec files reviewed**: `overview.md`, `views.md`, `events.md`, `typing.md`,
  `formats.md`, `templates.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Value` type (`DateTime?`) | bug | Spec signature: `DateTime? Value`. Impl uses `DateOnly?`. Breaking type mismatch that invalidates every spec example. | Change parameter type to `DateTime?`; convert internally. |
  | `View` / `BottomView` navigation (views.md) | missing | Spec: picker supports Month/Year/Decade/Century navigation (`CalendarView` enum). Impl hard-codes the Month view only. | Add `View` + `BottomView` parameters and render month/year/decade/century grids. |
  | Typing into the input (typing.md) | missing | Spec: users can type directly into the text input, not only pick via calendar. Impl renders `readonly="true"` on the input (line 15). | Remove `readonly`; add typing + parse pipeline. |
  | `Format` / `FormatPlaceholder` / `AutoComplete` | incomplete | `Format` exists but is display-only; no placeholder segments, no autocomplete hint. | Extend typing branch. |
  | `DebounceDelay` | missing | Spec parameter. Impl: none. | Add debounce on typing. |
  | Imperative API (`Open`, `Close`, `NavigateTo`, `Refresh`, `FocusAsync`) | missing | Spec lists five public reference methods. Impl exposes none. | Add them; flip `_isCalendarOpen`; expose view state. |
  | `ShowClearButton` rendering | bug | Parameter exists but no clear button is rendered anywhere in the markup. Dead parameter. | Render an inline clear button when `ShowClearButton && Value.HasValue`. |
  | `Id`, `TabIndex`, `Name`, `Title`, `AriaLabel`, `AriaLabelledBy` | missing | Spec lists them explicitly. Impl: none (except pass-through). | Add explicit `[Parameter]`s. |
  | `CellRender` / `MonthCellTemplate` / `YearCellTemplate` (templates.md) | missing | Spec exposes render customization for every view. Impl offers no templates. | Add `RenderFragment<CalendarCellRenderEventArgs>` templates per view. |
  | `ValidateOn` (events.md §ValidateOn) | missing | Spec enum Input/Blur/Change. | Add. |
  | Calendar ARIA (`role="grid"`, `aria-labelledby`, `aria-activedescendant`) | incomplete | Impl renders `role="dialog"` on calendar wrapper (line 22) and `aria-selected` on days; missing grid/row/gridcell semantics. | Use `role="grid"`; add row/gridcell; implement roving tabindex. |

- **Verdict**: `downgrade-to-partial` — 1 type bug (DateOnly vs DateTime) + 7 missing +
  2 incomplete items. The `DateOnly` vs `DateTime` type mismatch alone is spec-breaking.

---

## Component: SunfishDateRangePicker (spec: `daterangepicker`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishDateRangePicker.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\DateRangePicker\Overview\Demo.razor`
    (Overview-only; missing Accessibility/Appearance/Events)
- **Spec files reviewed**: `overview.md`, `events.md`, `views.md`, `templates.md`,
  `formats.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Imperative API (`Open`, `Close`, `NavigateTo`, `Refresh`, `FocusStartAsync`, `FocusEndAsync`) | covered | All six spec methods are implemented. | — |
  | `View` / `BottomView` navigation | covered | Impl exposes both and renders month/year/decade/century. | — |
  | `AllowReverse` / `StartId`/`EndId` / `TabIndex` / `PopupClass` / `DebounceDelay` | covered | All present. | — |
  | `HeaderTemplate` (templates.md) | covered | Impl accepts a `RenderFragment<>`. | — |
  | `CellRender` (templates.md) | incomplete | Spec exposes a `CellRender` template for each view. Impl accepts the header template only. | Add `CellRender` `RenderFragment<CalendarCellRenderEventArgs>`. |
  | Overview-only demo (ADR 0022) | incomplete | No Accessibility/Appearance/Events demos exist. | Author the three missing demo pages. |

- **Verdict**: `verified` — only two minor gaps, both incomplete rather than bug/missing.
  This is the most complete component in Forms-A.

---

## Component: SunfishDateTimePicker (spec: `datetimepicker`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishDateTimePicker.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\DateTimePicker\Overview\Demo.razor`
    (Overview-only; missing Accessibility/Appearance/Events)
- **Spec files reviewed**: `overview.md`, `events.md`, `views.md`, `templates.md`,
  `formats.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `View` / `BottomView` parameters (views.md) | missing | Spec exposes Month/Year/Decade/Century navigation the same as DatePicker. Impl hard-codes Month view. | Add `View`/`BottomView` parameters and render per-view calendar grids. |
  | `NavigateTo` imperative method | missing | Spec documents a public `NavigateTo(DateTime, CalendarView)` reference method. Impl has `Open`/`Close` only. | Add it. |
  | `Refresh` imperative method | missing | Spec lists `Refresh` alongside Open/Close. Impl: none. | Add it. |
  | `CellRender` / `MonthCellTemplate` templates (templates.md) | missing | Spec exposes view-specific render customization. Impl offers no templates. | Add `RenderFragment<CalendarCellRenderEventArgs>` templates. |
  | `Open`/`Close` imperative methods | covered | Public methods exist. | — |
  | Time-tumbler (hour/minute/second, `ShowSeconds`, `HourStep`/`MinuteStep`/`SecondStep`) | covered | All present. | — |
  | `Format` parsing + `AutoComplete` | covered | Spec's typing pipeline is implemented. | — |
  | `Id`, `TabIndex`, `InputMode` | covered | Explicit parameters are present. | — |
  | `Now` button (overview.md) | covered | Rendered. | — |
  | ARIA `role="grid"` + active descendant | covered | Impl emits grid/row/gridcell roles. | — |
  | Overview-only demo (ADR 0022) | incomplete | Accessibility/Appearance/Events demos missing. | Author them. |

- **Verdict**: `needs-work` — 3 missing (`View`/`BottomView`, `NavigateTo`, `Refresh`,
  templates) + 1 missing-demo cluster. No spec-breaking type bugs; the core typing
  and tumbler pipelines are solid.

---

## Component: SunfishDropDownList (spec: `dropdownlist`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishDropDownList.razor`
- **Demos**: four `Demo.razor` files under
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\DropDownList\{Overview,Accessibility,Appearance,Events}`.
- **Spec files reviewed**: `overview.md`, `filtering.md`, `grouping.md`, `events.md`,
  `templates.md`, `virtualization.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `FilterOperator` enum (filtering.md) | missing | Spec enumerates `StringFilterOperator` (Contains/StartsWith/EndsWith/EqualTo etc.). Impl accepts `Filterable` bool plus a string filter; no enum. | Add `StringFilterOperator` enum + `FilterOperator` parameter. |
  | `FilterPlaceholder`, `FilterDebounceDelay` | missing | Spec parameters. Impl: none. | Add both. |
  | `PopupSettings` child (`<DropDownListPopupSettings>`) | missing | Spec supports `Height`/`Width`/`AnimationDuration`/`Class` via a nested child tag. Impl hard-codes popup sizing. | Add a settings cascade. |
  | `AdaptiveMode` full-screen fallback | missing | Spec: `AdaptiveMode.Auto` promotes popup to a full-screen adaptive sheet on small viewports. Impl has no adaptive mode. | Add `AdaptiveMode` param + adaptive CSS hook. |
  | Virtualization (`Virtual` / `ItemHeight` / `PageSize`) | missing | Spec documents virtualization. Impl renders the entire list. | Integrate Blazor `Virtualize` (same shape as Autocomplete). |
  | Imperative API (`Open`, `Close`, `Refresh`, `FocusAsync`) | missing | Spec exposes reference methods. Impl: none. | Capture `ElementReference`; add public methods. |
  | `Id`, `TabIndex`, `Name`, `Title` | missing | Spec lists them explicitly. Impl: none. | Add explicit `[Parameter]`s. |
  | `DefaultText` + placeholder-when-empty | covered | Impl renders placeholder. | — |
  | Grouping (`GroupField` + group header render) | covered | Impl groups. | — |
  | `ItemTemplate` / `ValueTemplate` / `HeaderTemplate` / `FooterTemplate` | covered | All render. | — |
  | Keyboard navigation + `aria-activedescendant` | covered | Impl wires up keyboard nav. | — |

- **Verdict**: `needs-work` — 7 missing items but the core value binding, grouping,
  and templating pipelines are healthy. No spec-typed bugs; the gap is feature breadth,
  not correctness.

---

## Component: SunfishFileUpload (spec: `fileselect`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishFileUpload.razor(.cs)`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\FileSelect\Overview\Demo.razor`
    (Overview-only; missing Accessibility/Appearance/Events)
- **Spec files reviewed**: `overview.md`, `events.md`, `appearance.md`, `templates.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Component name drift (`FileSelect` → `FileUpload`) | incomplete | Spec component is `FileSelect`; impl is `SunfishFileUpload`. The mapping file papers over this but `<SunfishFileSelect>` does not compile. | Add a `SunfishFileSelect` alias (empty partial subclass) or rename. |
  | `SelectButtonText` / `SelectFilesButtonText` | missing | Spec lets callers rename the "Select files" button. Impl hard-codes the label. | Add explicit `[Parameter]` strings. |
  | `FileTemplate` (templates.md) | missing | Spec exposes a per-file render template. Impl renders a fixed layout. | Add `RenderFragment<IBrowserFile>`. |
  | `ItemTemplate` / `DropZoneTemplate` | missing | Spec exposes render customization for the drop zone and the file list item. | Add RenderFragment parameters. |
  | Progress + cancel surface (events.md §OnUpload) | missing | Spec separates `OnSelect` (picked) from `OnUpload` (streamed). Impl has `OnSelect`/`OnRemove` only. | Add upload pipeline (`OnUpload`, `OnProgress`, `OnPause`, `OnCancel`) or document `FileSelect`-only scope explicitly. |
  | `Accept`, `AllowedExtensions`, `Multiple`, `MaxFileSize`, `MinFileSize`, `Capture` | covered | All parameters present. | — |
  | Drag-and-drop support | covered | Impl wires up `DropZoneId`. | — |
  | `ClearFiles`, `RemoveFileAsync`, `OpenSelectFilesDialog` imperative API | covered | Public methods exist. | — |
  | ARIA `role="button"` + `aria-label` on drop zone | covered | Impl emits proper ARIA. | — |
  | Overview-only demo (ADR 0022) | incomplete | Accessibility/Appearance/Events demos missing. | Author them. |

- **Verdict**: `needs-work` — 4 missing (templates, label text) + 2 incomplete (name
  drift, missing demos). Core file-select behavior and a11y are solid; the gap is
  templating breadth plus a clearer spec-to-impl name bridge.

---

## Component: SunfishLabel (spec: `floatinglabel`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Containers\SunfishLabel.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\FloatingLabel\Overview\Demo.razor`
    (Overview-only; missing Accessibility/Appearance/Events)
- **Spec files reviewed**: `overview.md`, `appearance.md`, `events.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Floating-label behavior (overview.md entire doc) | bug | Spec: `FloatingLabel` wraps an inner editor and animates the label position based on the input's focused/filled state. Impl: renders a plain `<label for="">` with no wrapping, no animation, no focus detection. It is a basic label, not a floating label. | Rebuild `SunfishLabel` (or add a new `SunfishFloatingLabel`) that wraps a `ChildContent`, tracks the child's focus + value-empty state via `CascadingValue`, and animates the label on transition. |
  | `For` → `EditorId` linkage | incomplete | Impl's `For` attribute emits plain `<label for="">`. Spec wants auto-detection of the wrapped editor's Id, since the floating label *contains* the editor. | Derive the wrapped editor's `Id` via cascade; allow `For` as override. |
  | `FillMode` / `Rounded` / `ThemeColor` (appearance.md) | missing | Spec exposes appearance dials same as TextBox. Impl: none. | Add parameters and styles. |
  | `Enabled` + forwarded disabled styling | missing | Spec: FloatingLabel participates in `Enabled` cascade and greys out when the child editor is disabled. Impl: no cascade, no styling. | Add `Enabled`; cascade; style. |
  | `OnFocus` / `OnBlur` event hooks | missing | Spec: FloatingLabel raises events mirroring the wrapped editor's focus/blur. Impl: none (just a label element). | Wire events after the wrapping rebuild. |
  | ARIA: `aria-hidden` on label vs `aria-labelledby` wiring | incomplete | Spec requires labelled-by wiring to the wrapped editor. Impl: plain `<label for="">` only. | After wrapping rebuild, emit `aria-labelledby` from the editor and `id` on label. |
  | Overview-only demo (ADR 0022) | incomplete | No Accessibility/Appearance/Events demos exist. | Author them. |

- **Verdict**: `downgrade-to-partial` — 1 bug (wrong component class entirely), 3 missing
  parameters, 2 incomplete items. This is the second-deepest regression in Forms-A:
  the impl is not the component the spec describes — it is a plain label.

---

## Component: SunfishForm (spec: `form`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Containers\SunfishForm.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Forms\Form\Overview\Demo.razor`
    (Overview-only; missing Accessibility/Appearance/Events)
- **Spec files reviewed**: `overview.md`, `form-items.md`, `form-groups.md`,
  `orientation.md`, `validation.md`, `buttons.md`, `events.md`, `templates.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `<FormItems>` / `<FormItem>` child tags (form-items.md) | missing | Spec's primary usage has declarative child tags with per-field `Field`, `Label`, `EditorType`, `EditorTemplate`, `LabelTemplate`, `ValidationMessage`. Impl only auto-generates fields via reflection on `TModel`. | Add `FormItem` component with cascading registration to the parent form. |
  | `<FormGroup>` child tag (form-groups.md) | missing | Spec supports grouping with labeled sections. Impl: none. | Add `FormGroup` with nested `FormItem`s. |
  | `EditorType` enum (spec enum) | missing | Spec enumerates editor types for auto-rendering (TextBox, NumericTextBox, DatePicker, etc.). Impl uses plain HTML `<input>` tags keyed off `Type.GetType()`, not Sunfish editors. | Add `EditorType` enum + a map from enum→Sunfish editor. |
  | `<FormValidation>` slot (validation.md) | missing | Spec exposes a top-level validation panel slot. Impl: none. | Add `ValidationSummary` `RenderFragment` + default fallback. |
  | `ValidationMessageType` enum (validation.md) | missing | Spec enumerates Text/Tooltip/None. Impl: fixed inline text. | Add enum + rendering variants. |
  | `FormButtonsLayout` enum (buttons.md) | missing | Spec: Start/End/Stretch/Center. Impl: fixed placement. | Add enum + CSS. |
  | `RowSpacing`, `ColumnSpacing`, `Size`, `Width`, `Id` | missing | Spec lists explicit layout parameters. Impl has `Columns`/`Orientation` only. | Add them. |
  | Public `EditContext` property + `Refresh()` method | missing | Spec exposes the EditContext via a public property and a `Refresh()` method. Impl exposes `EditContext` only as an optional `[Parameter]`, not as a read-back property, and no `Refresh()` imperative. | Add public property + method. |
  | Data-annotation `[Display]` / `[Editable]` processing | missing | Spec auto-renders labels from `[Display(Name=)]` and respects `[Editable(false)]`. Impl uses plain property name only. | Process the attributes during reflection. |
  | `OnSubmit` / `OnValidSubmit` / `OnInvalidSubmit` | covered | Impl matches spec signature. | — |
  | `Columns`, `Orientation`, `SubmitText`, `ActionsTemplate` | covered | Present. | — |
  | Overview-only demo (ADR 0022) | incomplete | Accessibility/Appearance/Events demos missing. | Author them. |

- **Verdict**: `downgrade-to-partial` — 9 missing (child tag hierarchy, validation slot,
  enums, EditContext surface) + 1 incomplete demo item. Core `<EditForm>` wrapping
  works but the spec's declarative API is absent.

---

## Component: SunfishMaskedInput (spec: `maskedtextbox`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishMaskedInput.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\MaskedTextBox\Overview\Demo.razor`
    (Overview-only; missing Accessibility/Appearance/Events)
- **Spec files reviewed**: `overview.md`, `mask-rules.md`, `events.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Mask rendering + enforcement (mask-rules.md entire doc) | bug | Spec: MaskedTextBox enforces a format (`0000-0000`, `A9A9A9`, custom rules map) with prompt characters, caret-snapping, and rejection of invalid keystrokes. Impl: the `Mask` parameter is rendered as a *placeholder* only — no keystroke validation, no caret handling, no prompt. The "masked" behavior is unimplemented. | Build mask engine (character classes, literal pass-through, caret snap, invalid-key reject). |
  | `PromptPlaceholder` | missing | Spec: which character represents an unfilled slot (default `_`). Impl: none. | Add parameter; render in unfilled positions. |
  | `MaskOnFocus` | missing | Spec: show the mask only when the input is focused. Impl: none. | Add bool; toggle display. |
  | `IncludeLiterals` | incomplete | Impl exposes the parameter but because mask isn't implemented, the parameter has no effect. | Honor during mask engine output. |
  | `Rules` dictionary (mask-rules.md §custom rules) | missing | Spec accepts a `Rules` parameter (`IDictionary<char, Regex>`) for custom character classes. Impl: none. | Add parameter; integrate with engine. |
  | `DebounceDelay` | missing | Spec parameter. Impl: none. | Add timer-based debounce. |
  | `Id`, `TabIndex`, `Name`, `Title`, `AutoCapitalize`, `SpellCheck`, `AutoComplete`, `InputMode` | missing | Spec lists explicit identity/input-shaping parameters. Impl: only `Mask` + `Placeholder` + `Disabled`. | Add them. |
  | `ShowClearButton` | missing | Spec parameter. Impl: none. | Add clear button affordance. |
  | `FocusAsync` reference method | missing | Spec documents programmatic focus. Impl: none. | Capture ElementReference + method. |
  | Overview-only demo (ADR 0022) | incomplete | Accessibility/Appearance/Events demos missing. | Author them. |

- **Verdict**: `downgrade-to-partial` — 1 severe bug (mask not implemented) + 6 missing
  items + 1 incomplete parameter + 1 missing-demo cluster. Tied with DateInput for
  deepest regression: the component does not perform its core spec function.

---

## Component: SunfishNumericInput (spec: `numerictextbox`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishNumericInput.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\NumericTextBox\Overview\Demo.razor`
    (Overview-only; missing Accessibility/Appearance/Events)
- **Spec files reviewed**: `overview.md`, `events.md`, `appearance.md`, `formats.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Generic `TValue` parameter | bug | Spec: `NumericTextBox<TValue>` supports `int`/`decimal`/`double`/`float`/nullable variants. Impl: `Value` is hard-typed `double`. Callers of `SunfishNumericInput<int>` will not compile. | Convert to `@typeparam TValue` with numeric constraints (or `INumber<TValue>`). |
  | `Format` parameter application (formats.md) | incomplete | `Format` parameter exists but is not applied to the rendered input value. | Use `Value.ToString(Format, culture)` on render. |
  | `Decimals` application (formats.md) | incomplete | `Decimals` parameter exists but does not round/format on focus. | Apply on focus + blur. |
  | `Arrows` / `Spinners` visibility | missing | Spec: `Arrows` bool (default true) shows/hides the increment/decrement buttons. Impl hard-codes spinners. | Add `Arrows` parameter; conditionally render. |
  | `SelectOnFocus` | missing | Spec parameter. Impl: none. | Add bool; wire `onfocus`. |
  | `ShowClearButton` rendering | bug | Parameter exists but no clear button rendered. | Render inline clear affordance. |
  | `Autocomplete`, `InputMode`, `Name`, `Id`, `TabIndex` | missing | Spec lists them explicitly. Impl: none. | Add explicit `[Parameter]`s. |
  | `FocusAsync` reference method | missing | Spec documents programmatic focus. Impl: none. | Capture ElementReference + method. |
  | `ValidateOn` enum | missing | Spec: Input/Blur/Change. Impl: none. | Add enum + plumbing. |
  | `Step` (covered) + `Min`/`Max` clamp | covered | Present and working. | — |
  | `IsInvalid` styling | covered | Impl applies invalid CSS class. | — |
  | Overview-only demo (ADR 0022) | incomplete | Accessibility/Appearance/Events demos missing. | Author them. |

- **Verdict**: `downgrade-to-partial` — 2 bug (generic TValue, dead ShowClearButton) +
  6 missing + 2 incomplete + 1 missing-demo cluster. `TValue` drift is spec-breaking.

---

## Component: SunfishRadioGroup (spec: `radiogroup`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishRadioGroup.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\RadioGroup\Overview\Demo.razor`
    (Overview-only; missing Accessibility/Appearance/Events)
- **Spec files reviewed**: `overview.md`, `events.md`, `layout.md`, `templates.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Generic `TValue` parameter | bug | Spec: `RadioGroup<TValue>` binds to any value type. Impl: `Value` is hard-typed `string`. `@bind-Value` with `int`/enum/Guid will not compile. | Convert to `@typeparam TValue`. |
  | `LabelPosition` enum (layout.md) | bug | Spec: `RadioGroupLabelPosition { Before, After }`. Impl: parameter is `string`. | Add enum; retire string. |
  | `Layout` enum (layout.md) | bug | Spec: `RadioGroupLayout { Horizontal, Vertical }`. Impl: parameter is `string`. | Add enum; retire string. |
  | `FocusItemAsync(TValue)` reference method | missing | Spec lists a public method to focus a specific item. Impl: none. | Capture ElementReferences per item; add method. |
  | `ItemTemplate` (templates.md) | missing | Spec exposes per-item render customization. Impl: fixed render. | Add `RenderFragment<RadioGroupItem<TValue>>`. |
  | `Size` / `ThemeColor` (appearance parity with checkbox) | missing | Spec exposes appearance dials. Impl: none. | Add parameters. |
  | `Id`, `Name` | missing | Spec parameters. Impl: none. | Add. |
  | ARIA `role="radiogroup"` + per-item `role="radio"` + `aria-checked` | covered | Impl renders native radios. | — |
  | Keyboard arrow-key nav within group | covered | Native radios handle it. | — |
  | Overview-only demo (ADR 0022) | incomplete | Accessibility/Appearance/Events demos missing. | Author them. |

- **Verdict**: `downgrade-to-partial` — 3 bug (generic TValue, two string-instead-of-enum
  parameters) + 4 missing + 1 missing-demo cluster. Generic TValue drift is the
  worst; enum-as-string drift also invalidates every spec example.

---

## Component: SunfishRangeSlider (spec: `rangeslider`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishRangeSlider.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\RangeSlider\Overview\Demo.razor`
    (Overview-only; missing Accessibility/Appearance/Events)
- **Spec files reviewed**: `overview.md`, `events.md`, `appearance.md`, `ticks.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Generic `TValue` parameter | bug | Spec: `RangeSlider<TValue>` supports `int`/`decimal`/`double`. Impl: `Start`/`End` are hard-typed `double`. | Convert to `@typeparam TValue`. |
  | `SmallStep` vs `Step` parameter name (ticks.md) | bug | Spec: `SmallStep` controls increment; `LargeStep` controls ticks/labels. Impl names the increment `Step` (missing `SmallStep`). | Rename `Step`→`SmallStep` (keep alias). |
  | `TickPosition` enum (ticks.md) | missing | Spec: `SliderTickPosition { None, Before, After, Both }`. Impl: tick position is implicit. | Add enum + render. |
  | `Decimals` / `Format` label display | missing | Spec exposes label-formatting parameters. Impl: raw numbers only. | Add parameters and apply on label render. |
  | `LargeStep` (tick spacing) | covered | Impl renders ticks using this. | — |
  | `Orientation` (Horizontal/Vertical) | covered | Impl handles both. | — |
  | ARIA `role="slider"` + `aria-valuemin`/`aria-valuemax`/`aria-valuenow` dual handles | covered | Impl emits per-handle ARIA. | — |
  | Keyboard arrow-key movement | covered | Impl wires up. | — |
  | Overview-only demo (ADR 0022) | incomplete | Accessibility/Appearance/Events demos missing. | Author them. |

- **Verdict**: `downgrade-to-partial` — 2 bug (generic TValue, Step naming) + 2 missing
  + 1 missing-demo cluster. TValue drift is spec-breaking; ticks/labels otherwise solid.

---

## Component: SunfishRating (spec: `rating`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishRating.razor`
- **Demos**: four `Demo.razor` files under
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\Rating\{Overview,Accessibility,Appearance,Events}`.
- **Spec files reviewed**: `overview.md`, `events.md`, `precision.md`, `selection.md`,
  `templates.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Value` type (`double`) | bug | Spec: `double? Value` (decimal ratings for half/fractional support). Impl: `int Value`. Fractional/half-star ratings are not representable. | Change type to `double?`. |
  | `PrecisionMode` enum (precision.md) | missing | Spec: `RatingPrecision { Full, Half }`. Impl: full only. | Add enum + render half-star fill. |
  | `SelectionMode` enum (selection.md) | missing | Spec: `RatingSelection { Continuous, Single }` — Continuous fills all stars ≤ value; Single selects only one. Impl: continuous only. | Add enum + render logic. |
  | `HoverThrottleInterval` | missing | Spec parameter to throttle hover repaints. Impl: none. | Add int param; throttle. |
  | `ReadOnly` parameter name | bug | Spec: `ReadOnly`. Impl: `IsReadOnly`. | Rename (keep alias). |
  | `ItemTemplate` (templates.md) | missing | Spec exposes per-item render customization. Impl: fixed star markup. | Add `RenderFragment<RatingItem>`. |
  | `AriaLabel` / `AriaLabelledBy` | missing | Spec parameters. Impl: none (pass-through via `AdditionalAttributes` only). | Add explicit `[Parameter]`s. |
  | `Class` parameter | missing | Spec parameter for additional class names. Impl: uses base class `Class` via AdditionalAttributes only. | Verify base class handling or add. |
  | ARIA `role="slider"` + `aria-valuemin`/`aria-valuemax`/`aria-valuenow` | covered | Impl emits ARIA. | — |
  | Keyboard arrow-key + Home/End | covered | Impl wires up. | — |

- **Verdict**: `downgrade-to-partial` — 2 bug (int vs double, `IsReadOnly` vs `ReadOnly`)
  + 5 missing items. Fractional/precision support is spec-core.

---

## Component: SunfishTextArea (spec: `textarea`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishTextArea.razor`
- **Demos**: four `Demo.razor` files under
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\TextArea\{Overview,Accessibility,Appearance,Events}`.
- **Spec files reviewed**: `overview.md`, `events.md`, `appearance.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Id`, `TabIndex`, `Title`, `Name`, `Cols` | missing | Spec lists them explicitly. Impl: only `Rows`, `MaxLength`, `AutoComplete`, `SpellCheck`. | Add explicit `[Parameter]`s. |
  | `DebounceDelay` | missing | Spec parameter. Impl: synchronous. | Add timer-based debounce. |
  | `ResizeMode` enum (appearance.md) | missing | Spec: `TextAreaResizeMode { None, Both, Horizontal, Vertical, Auto }`. Impl: uses the browser default (`resize: both`). | Add enum + CSS. |
  | `Width` | missing | Spec parameter. Impl: none. | Add param; render inline style. |
  | `AutoCapitalize` | missing | Spec parameter. Impl: none. | Add param. |
  | `FocusAsync` reference method | missing | Spec documents programmatic focus. Impl: none. | Capture ElementReference + method. |
  | `Rows`, `MaxLength`, `AutoComplete`, `SpellCheck`, `Placeholder`, `Disabled`, `ReadOnly` | covered | Present. | — |
  | `Value` + `ValueChanged` two-way binding | covered | Present. | — |
  | ARIA `aria-multiline="true"` + `aria-label` | covered | Native `<textarea>` handles it. | — |

- **Verdict**: `needs-work` — 6 missing but nothing spec-breaking. Core input behavior
  is solid; the gap is parameter breadth and debounce.

---

## Component: SunfishTextBox (spec: `textbox`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishTextBox.razor`
- **Demos**: four `Demo.razor` files under
  `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\TextBox\{Overview,Accessibility,Appearance,Events}`.
- **Spec files reviewed**: `overview.md`, `events.md`, `appearance.md`, `adornments.md`,
  `validation.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Id`, `TabIndex`, `Title`, `Name` | missing | Spec lists identity parameters explicitly. Impl: only `Value`, `Placeholder`, `Disabled`, `ReadOnly`, `MaxLength`, `DebounceDelay`, `Autocomplete`, `Prefix`/`Suffix`, `ShowClearButton`. | Add explicit `[Parameter]`s. |
  | `AutoCapitalize`, `InputMode`, `SpellCheck` | missing | Spec lists input-shaping parameters. Impl: none. | Add params + render attributes. |
  | `Password` parameter | missing | Spec exposes a `bool Password` to switch to `type="password"`. Impl renders `type="text"` only. | Add param; derive `type`. |
  | `ValidateOn` enum (validation.md) | missing | Spec: Input/Blur/Change. Impl: none. | Add enum + plumbing. |
  | `FocusAsync` reference method | missing | Spec documents programmatic focus. Impl: none. | Capture ElementReference + method. |
  | `Size` / `Rounded` / `FillMode` (appearance.md) | missing | Spec exposes appearance dials. Impl: none (relies on parent `SunfishField`). | Add parameters that cascade into wrapper classes. |
  | `ShowClearButton` rendering | incomplete | Parameter exists; need to verify the clear affordance renders in all configs (not just with prefix/suffix). | Audit render paths. |
  | `Placeholder`, `DebounceDelay`, `Autocomplete`, `Prefix`/`Suffix`, `MaxLength` | covered | Present. | — |
  | `IsInvalid` styling | covered | Impl applies invalid CSS class. | — |
  | ARIA `aria-invalid`, `aria-describedby`, `role="textbox"` | covered | Native `<input>` handles most; impl wires `aria-invalid`. | — |

- **Verdict**: `needs-work` — 6 missing parameter/method items + 1 incomplete render
  path. No spec-breaking bugs; the core adornments/debounce pipeline is working. Gap
  is breadth of input-shaping + identity parameters.

---

## Next actions (top priority fixes for the Forms-A family)

Ordered by blast-radius, not by component:

1. **Rebuild the two "wrong component" impls** (highest priority — these are not
   spec-conforming components)
   - `SunfishMaskedInput`: implement the mask engine (character classes, prompt
     chars, caret snap, invalid-key reject, `Rules` dictionary, `MaskOnFocus`,
     `PromptPlaceholder`, `IncludeLiterals` honored). Today the "mask" is a
     placeholder string only.
   - `SunfishLabel` (mapped to `floatinglabel`): build a true floating-label
     wrapper that tracks the child editor's focus + value-empty state via
     cascade and animates the label. Today it is a plain `<label for="">`.
   - `SunfishDateInput`: build a segment-based input (MM/DD/YYYY sub-fields with
     auto-switch, per-segment `role="spinbutton"`, arrow-step, `Format`/`FormatPlaceholder`).
     Today it is a native `<input type="date">` wrapper.

2. **Fix spec-breaking type signatures (generic TValue + primitive drifts)**
   - `SunfishNumericInput`: convert to `@typeparam TValue` (int/decimal/double,
     nullable variants).
   - `SunfishRadioGroup`: convert `Value` from `string` to `@typeparam TValue`.
   - `SunfishRangeSlider`: convert `Start`/`End` from `double` to `@typeparam TValue`.
   - `SunfishRating`: change `Value` from `int` to `double?`; add `PrecisionMode`
     (Full/Half) and `SelectionMode` (Continuous/Single) enums.
   - `SunfishDatePicker`: change `Value` from `DateOnly?` to `DateTime?` (or add
     overload).

3. **Rename bind parameters to match spec examples**
   - `SunfishCheckbox`: rename `Checked` → `Value` (keep `[Obsolete]` alias).
   - `SunfishChip` (already covered in Buttons audit — not Forms-A).
   - `SunfishRating`: rename `IsReadOnly` → `ReadOnly`.
   - `SunfishRadioGroup`: replace string `LabelPosition`/`Layout` params with
     `RadioGroupLabelPosition` and `RadioGroupLayout` enums.
   - `SunfishRangeSlider`: rename `Step` → `SmallStep` (keep alias).

4. **Add imperative APIs that every spec documents**
   - Add `FocusAsync` (via `ElementReference`) on TextBox, TextArea, NumericInput,
     MaskedInput, DateInput, RadioGroup (per-item `FocusItemAsync`),
     Autocomplete, DropDownList, Checkbox, DatePicker.
   - Add `Open`/`Close`/`Refresh`/`NavigateTo` on DatePicker and DateTimePicker
     (DateRangePicker already has them).
   - Add public `EditContext` property and `Refresh()` method on `SunfishForm`.

5. **Add missing identity parameters across the family**
   - `Id`, `TabIndex`, `Name`, `Title`, `AriaLabel`, `AriaLabelledBy` on every
     simple input that doesn't have them (Checkbox, DateInput, DatePicker,
     DropDownList, MaskedInput, NumericInput, RadioGroup, TextArea, TextBox,
     Autocomplete). Spec treats these as first-class, not pass-through.

6. **Add missing child-tag / declarative-API hierarchies**
   - `SunfishForm`: add `FormItem`, `FormItems`, `FormGroup`, `FormValidation`
     child components with cascading registration; add `EditorType` enum +
     editor map; process `[Display]` and `[Editable]` data annotations.
   - `SunfishAutocomplete`, `SunfishDropDownList`: add `PopupSettings` child
     component for popup sizing/animation.
   - `SunfishFileUpload`: add `FileTemplate`, `ItemTemplate`, `DropZoneTemplate`
     render fragments.

7. **Kitchen-sink demo gaps (ADR 0022)**
   - Nine components have only an Overview demo and need three more each:
     DateInput, DateRangePicker, DateTimePicker, FileSelect, FloatingLabel,
     Form, MaskedTextBox, NumericTextBox, RadioGroup, RangeSlider.
   - FileSelect demo should also switch `<SunfishFileUpload>` to `<SunfishFileSelect>`
     (after adding the alias/rename in item 1 group).

---

_Audit prepared: 2026-04-21. Tier 1 of the Blazor-100% parity push (ADR 0022)._
