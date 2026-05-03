# Gap Analysis Part 3 -- Form Input Components (Select through Upload)

> Base class (`SunfishComponentBase`) provides: `Class`, `Style`, `AdditionalAttributes`

---

## 1. SunfishSelect

**Spec source:** `docfx/articles/components/select/overview.md`

- **Filterable mode not in spec** -- Component implements `Filterable` parameter with filter textbox and `OnFilterTextChanged` callback; spec documents none of this.
- **Spec is minimal** -- Spec only lists `Value`, `ValueChanged`, `IsInvalid`, `ChildContent`; the implementation matches these exactly.
- **No Enabled/Disabled support** -- Spec mentions none; component has none. Consider adding `Disabled` for parity with other inputs.
- **No multi-select** -- Spec does not mention it; component does not support it. Reference spec implies single-select only.

---

## 2. SunfishSlider

**Spec source:** `docs/component-specs/slider/overview.md`

- **Missing `SmallStep` parameter** -- Spec requires `SmallStep` (defines selectable values and small ticks); component only has `Step` which partially covers this.
- **Missing `TickPosition` parameter** -- Spec defines `TickPosition` (`Before`/`After`/`Both`/`None`); component has no tick-position control.
- **Missing `ShowButtons` parameter** -- Spec defines side arrow buttons (`ShowButtons`, default `true`); component renders none.
- **Missing `Enabled`/`Decimals` parameters** -- Spec lists both; component has neither. No disabled state support.
- **Generic type not implemented** -- Spec says Slider is generic over numeric types (`int`, `decimal`, etc.); component is hardcoded to `double`.

---

## 3. SunfishSwitch

**Spec source:** `docs/component-specs/switch/overview.md`

- **Missing `OnLabel`/`OffLabel` parameters** -- Spec defines separate labels for on/off states; component has only a single `Label` string.
- **Binding name mismatch** -- Spec uses `Value`/`@bind-Value` (bool); component uses `IsOn`/`IsOnChanged`. Not aligned with spec convention.
- **Missing `Enabled` parameter** -- Spec lists `Enabled` for disabling the switch; component has no disabled state.
- **Missing `Id`, `TabIndex`, `Width`** -- Spec lists all three for accessibility and sizing; component has none.
- **Missing `FocusAsync` method** -- Spec documents programmatic focus; component does not expose it.

---

## 4. SunfishTextArea

**Spec source:** `docs/component-specs/textarea/overview.md`

- **Missing many parameters** -- Spec lists `Placeholder`, `MaxLength`, `Enabled`, `ReadOnly`, `DebounceDelay`, `Cols`, `AutoComplete`, `ResizeMode`, `SpellCheck`, `TabIndex`, `Name`, `Id`; component only has `Rows` and `IsInvalid` beyond base.
- **No `Placeholder` support** -- A basic usability feature specified in the spec is absent.
- **No debounce support** -- Spec specifies `DebounceDelay` (default 150ms); component fires on every `oninput` with no throttling.
- **No `Enabled`/`ReadOnly` support** -- Spec lists both; component cannot be disabled or made read-only.
- **No `FocusAsync` method** -- Spec documents programmatic focus; component does not expose it.

---

## 5. SunfishTextField

**Spec source:** `docs/component-specs/textbox/overview.md`

- **Missing `Password` mode** -- Spec defines `Password` bool to render `type="password"`; component uses generic `InputType` string instead (functional but not API-compatible).
- **Missing `ShowClearButton`** -- Spec defines an in-input clear button; component has none.
- **Missing `Id`, `Name`, `TabIndex`, `Title`, `InputMode`, `SpellCheck`** -- Spec lists all for accessibility and browser integration; component has none of these.
- **`Enabled` vs `Disabled` naming** -- Spec uses `Enabled` (default true); component uses `Disabled` (inverted). Inconsistent with spec convention.
- **Good coverage otherwise** -- `Placeholder`, `ReadOnly`, `MaxLength`, `AutoComplete`, `DebounceDelay`, `Prefix`/`Suffix` are all implemented (Prefix/Suffix exceed spec).

---

## 6. SunfishTimePicker

**Spec source:** `docs/component-specs/timepicker/overview.md`

- **No dropdown/tumbler UI** -- Spec describes a visual dropdown with tumblers for hours/minutes/AM-PM; component renders a plain `<input type="time">` only.
- **Missing `Format` parameter** -- Spec supports custom time format strings (e.g., `"hh:mm:ss tt"`); component has no format control.
- **Missing `Min`/`Max` parameters** -- Spec defines time range constraints; component has none.
- **`TimeOnly` vs `DateTime` type** -- Spec binds to `DateTime`/`DateTime?`; component uses `TimeOnly?`. Different API contract.
- **Missing `Open`/`Close` methods, `DebounceDelay`, `ShowClearButton`, `ReadOnly`** -- All specified; none implemented.

---

## 7. SunfishUpload

**Spec source:** `docs/component-specs/upload/overview.md`

- **Missing `SaveField`/`RemoveField` parameters** -- Spec allows customizing the FormData key name (default `"files"`); component hardcodes `"file"` in `MultipartFormDataContent`.
- **Missing `MinFileSize` validation** -- Spec defines minimum file size check; component only validates `MaxFileSize`.
- **Missing chunk upload support** -- Spec documents chunked upload for large files; component uploads in one piece only.
- **Missing programmatic methods** -- Spec lists `ClearFiles`, `UploadFiles`, `PauseFile`, `ResumeFile`, `RetryFile`, `CancelFile`, `OpenSelectFilesDialog`; component has none.
- **Missing `Initial Files` / `Enabled` / `Capture` / `DropZoneId`** -- Spec defines pre-populated file list, enabled state, camera capture, and external drop zone; none are implemented.
