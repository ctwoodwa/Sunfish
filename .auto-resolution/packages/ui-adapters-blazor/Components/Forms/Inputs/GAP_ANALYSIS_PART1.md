# Gap Analysis Part 1: Input Components

Base class (`SunfishComponentBase`) provides: `Class`, `Style`, `AdditionalAttributes`.

---

## 1. SunfishAutocomplete

**Current**: `Value`, `ValueChanged`, `Items`, `Data`, `TextField`, `ValueField`, `Placeholder`, `Disabled`, `IsInvalid`, `ReadOnly`, `ShowClearButton`, `FilterOperator`, `DebounceDelay`, `MinLength`, `ItemTemplate`, `HeaderTemplate`, `FooterTemplate`, `GroupField`

- **Missing `Enabled` param**: Spec uses `Enabled` (bool); implementation uses `Disabled` (inverted logic). Align naming with spec.
- **Missing `Filterable` param**: Spec has a `Filterable` toggle; current impl always filters when typing. Should add explicit on/off control.
- **Missing `Id` / `TabIndex` / `InputMode`**: Spec defines `Id`, `TabIndex`, and `InputMode` for the input element; none are wired through.
- **Missing popup settings**: Spec supports `AutoCompletePopupSettings` (Height, MaxHeight, MinHeight) and `AdaptiveMode`; not implemented.
- **Missing programmatic methods**: Spec references `Open()`, `Refresh()`, `FocusAsync()` on a component ref; not implemented.

---

## 2. SunfishCheckbox

**Current**: `Checked`, `CheckedChanged`, `Label`, `Indeterminate`

- **`Value` vs `Checked` naming**: Spec uses `Value` (bool) bound to the checkbox; implementation uses `Checked`. Consider renaming for spec alignment.
- **Missing `Enabled` param**: Spec has `Enabled`; implementation has no disabled state at all (no `Disabled` parameter, no `disabled` attribute on input).
- **Missing `Id` / `TabIndex`**: Spec defines `Id` (for label-for association) and `TabIndex`; neither is implemented.
- **Missing `Class` on input element**: Spec says `Class` renders on `<input class="k-checkbox">`; current impl puts class on `<label>`, not the input.

---

## 3. SunfishColorPicker

**Current**: `Value`, `ValueChanged`, `Disabled`

- **Missing `ValueFormat` param**: Spec supports `ColorFormat.Hex` / `ColorFormat.Rgb`; implementation only uses native `<input type="color">` (hex only).
- **Missing views / gradient+palette UI**: Spec describes a rich popup with HSV canvas, palette tiles, RGBA/HEX inputs, Apply/Cancel buttons; implementation is a bare `<input type="color">`.
- **Missing `ShowClearButton`, `ShowButtons`, `ShowPreview`, `Icon`**: Spec defines several UI toggle params; none exist.
- **Missing `Enabled` naming + `Size`/`Rounded`/`FillMode`**: Spec uses `Enabled` and appearance params; implementation only has `Disabled`.
- **Missing programmatic `Open()`/`Close()`/`FocusAsync()`**: Spec references component methods; not implemented.

---

## 4. SunfishComboBox

**Current**: `Data`, `TextField`, `ValueField`, `Value`, `ValueChanged`, `OnChange`, `Placeholder`, `Disabled`, `IsInvalid`, `AllowCustom`, `ShowClearButton`, `FilterMode`, `ItemTemplate`, `ValueTemplate`

- **Missing `Enabled`/`ReadOnly` params**: Spec has both `Enabled` and `ReadOnly`; implementation only has `Disabled`.
- **Missing `Filterable` toggle**: Spec has `Filterable` (bool); current impl always filters. Should be opt-in.
- **Missing `FilterOperator` / `DebounceDelay`**: Spec defines `StringFilterOperator` and `DebounceDelay` (150ms default); implementation has only `FilterMode` enum with Contains/StartsWith.
- **Missing `Id` / `TabIndex` / `InputMode` / `GroupField`**: Spec defines all four; none implemented.
- **Missing popup settings and `AdaptiveMode`**: Spec supports `ComboBoxPopupSettings` and adaptive rendering; not implemented.

---

## 5. SunfishDatePicker

**Current**: `Value` (DateOnly?), `ValueChanged`, `Placeholder`, `Min` (DateOnly?), `Max` (DateOnly?), `Disabled`, `DisabledDates`, `Format`

- **Type mismatch: `DateOnly` vs `DateTime`**: Spec binds to `DateTime` or `DateTime?`; implementation uses `DateOnly?`. This is a significant API incompatibility.
- **Missing `Enabled`/`ReadOnly` params**: Spec has both; implementation only has `Disabled`.
- **Missing `ShowClearButton` / `ShowWeekNumbers` / `ShowOtherMonthDays`**: Spec defines these; none implemented.
- **Missing `Id` / `TabIndex` / `DebounceDelay` / `AutoComplete`**: Spec defines all; none wired to the input element.
- **Missing calendar views (`BottomView`/`View`)** and methods (`Open`/`Close`/`NavigateTo`/`Refresh`/`FocusAsync`): Spec supports multi-view navigation; implementation is month-only.

---

## 6. SunfishDateRangePicker

**Current**: `StartDate`, `StartDateChanged`, `EndDate`, `EndDateChanged`, `Disabled`

- **Minimal implementation**: Uses two native `<input type="date">` elements. Spec describes a rich dual-calendar popup with range highlighting.
- **Missing `Min`/`Max`/`DisabledDates`/`AllowReverse`/`Format`**: Spec defines all of these range constraints; none implemented.
- **Missing `Enabled`/`ReadOnly`/`Placeholder`/`ShowClearButton`/`ShowWeekNumbers`**: Spec has these; only `Disabled` exists.
- **Missing `StartValue`/`EndValue` naming**: Spec uses `StartValue`/`EndValue`; implementation uses `StartDate`/`EndDate`.
- **Missing calendar popup, orientation, methods**: Spec supports `Open`/`Close`/`NavigateTo`/`FocusStartAsync`/`FocusEndAsync` and `Orientation`; none implemented.

---

## 7. SunfishDateTimePicker

**Current**: `Value` (DateTime?), `ValueChanged`, `Placeholder`, `Disabled`

- **Minimal implementation**: Uses a single native `<input type="datetime-local">`. Spec describes a calendar+time-tumbler popup with Set/Cancel/Now buttons.
- **Missing `Min`/`Max`/`Format`/`ShowClearButton`/`ShowWeekNumbers`**: Spec defines all; none implemented.
- **Missing `Enabled`/`ReadOnly`/`Id`/`TabIndex`/`DebounceDelay`/`AutoComplete`**: Spec defines all; none present.
- **Missing time tumblers and action buttons**: Spec has AM/PM, hour/minute/second tumblers with increment steps (`DateTimePickerSteps`); not implemented.
- **Missing programmatic `Open()`/`Close()`**: Spec references these methods; not implemented.

---

## 8. SunfishDropDownList

**Current**: `Data`, `TextField`, `ValueField`, `GroupField`, `Value`, `ValueChanged`, `Placeholder`, `DefaultText`, `Disabled`, `IsInvalid`, `Filterable`, `ItemTemplate`, `ValueTemplate`

- **Missing `Enabled`/`ReadOnly` params**: Spec has both; implementation only has `Disabled`.
- **Missing `FilterOperator` / `FilterDebounceDelay` / `FilterPlaceholder`**: Spec has all three filter-tuning params; implementation only has a `Filterable` toggle with hardcoded contains logic.
- **Missing `Id` / `TabIndex` / `InputMode`**: Spec defines these for accessibility; none wired through (component sets `tabindex="0"` but not configurable).
- **Missing `OnChange` event callback**: Spec implies change events; implementation only exposes `ValueChanged` (no separate `OnChange`).
- **Missing popup settings and `AdaptiveMode`**: Spec supports `DropDownListPopupSettings` (Height, MaxHeight, MinHeight) and adaptive rendering; not implemented.
