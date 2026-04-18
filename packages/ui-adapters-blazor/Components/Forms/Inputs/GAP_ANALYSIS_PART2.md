# Gap Analysis Part 2 -- Form Input Components

Base class (`SunfishComponentBase`) provides: `Class`, `Style`, `AdditionalAttributes`.

---

## 1. SunfishFileUpload

**Current**: `Multiple`, `Accept`, `MaxFileSize`, `Disabled`, `InitialFiles`, `OnFilesSelected`. Client-side only via `InputFile`/`IBrowserFile`. Shows file list with remove buttons and error messages.

- **Missing async server upload**: Spec requires `SaveUrl`/`RemoveUrl` endpoint-based upload with XHR; implementation uses Blazor `InputFile` only (no server endpoint integration).
- **Missing `AllowedExtensions` validation**: Spec validates file extensions client-side; implementation only validates file size.
- **Missing `AutoUpload` control**: Spec supports deferred upload with an explicit upload button; implementation uploads immediately on selection.
- **Missing `MinFileSize`**: Spec supports minimum file size validation.
- **Missing chunk upload**: Spec describes chunked file upload for large files; not implemented.

---

## 2. SunfishMaskedInput

**Current**: `Value`/`ValueChanged`, `Mask`, `Placeholder`, `IsInvalid`, `Disabled`. Plain `<input type="text">` with no mask enforcement logic.

- **No mask enforcement**: Spec's MaskedTextBox prevents invalid input in real time; implementation accepts any text and only uses `Mask` as placeholder fallback.
- **Missing `IncludeLiterals`, `Prompt`, `PromptPlaceholder`**: Spec has rich mask configuration (literal inclusion, prompt character customization); none implemented.
- **Missing `MaskOnFocus`**: Spec can show mask only on focus; not supported.
- **Missing `ShowClearButton`, `DebounceDelay`, `ReadOnly`**: Spec parameters not present.
- **Missing accessibility attributes**: Spec provides `AriaLabel`, `AriaDescribedBy`, `AriaLabelledBy`; not implemented.

---

## 3. SunfishMultiSelect

**Current**: Generic `TItem`/`TValue`, `Data`, `TextField`, `ValueField`, `Values`/`ValuesChanged`, `Placeholder`, `Disabled`, `IsInvalid`, `TagMode`, `ShowClearButton`, `AutoClose`, `ItemTemplate`. Keyboard navigation (arrow keys, Enter, Escape).

- **Missing filtering/search**: Spec supports `Filterable`, `FilterOperator`, `MinLength` for narrowing suggestions as user types; not implemented.
- **Missing `AllowCustom`**: Spec allows users to enter values not in the data source.
- **Missing virtualization**: Spec supports virtualized rendering for large data sets.
- **Missing adaptive rendering**: Spec supports `AdaptiveMode` for responsive popup behavior.
- **Missing popup configuration**: Spec has `MultiSelectPopupSettings` (Height, MaxHeight, MinHeight); component uses a basic `DropdownPopup`.

---

## 4. SunfishNumericInput

**Current**: `Value`/`ValueChanged` (double only), `Min`, `Max`, `Step`, `Placeholder`, `IsInvalid`, `Disabled`. Increment/decrement buttons with clamping.

- **Not generic**: Spec's `SunfishNumericTextBox` is generic over numeric types (`int`, `decimal`, `double`, etc.); implementation is hardcoded to `double`.
- **Missing `Format` and `Decimals`**: Spec supports custom number format strings and decimal precision control; not implemented.
- **Missing `Arrows` toggle**: Spec allows hiding spinner arrows via `Arrows` parameter; buttons are always shown.
- **Missing `ShowClearButton`, `ReadOnly`, `SelectOnFocus`**: Spec parameters absent.
- **Missing `DebounceDelay`**: Spec debounces value updates (default 150ms); implementation fires on every input.

---

## 5. SunfishRadio

**Current**: Single radio button with `Value`, `Name`, `IsSelected`, `Label`, `ValueChanged`.

- **Not a RadioGroup**: Spec describes a `SunfishRadioGroup` that renders a full list from `Data` with `TextField`/`ValueField` binding; implementation is a single radio button requiring manual repetition.
- **Missing `Layout` parameter**: Spec supports `Horizontal`/`Vertical` layout options for the group.
- **Missing `LabelPosition`**: Spec allows labels before or after radio buttons.
- **Missing data binding**: Spec binds to a collection of items with `TItem`/`TValue` generics; not implemented.
- **Missing `Enabled` (group-level)**: Spec disables the entire group; current `Disabled` parameter is absent entirely.

---

## 6. SunfishRangeSlider

**Current**: `StartValue`/`EndValue` with changed callbacks, `Min`, `Max`, `Step`, `Orientation`, `LargeStep`, `LabelTemplate`. Visual fill bar and tick marks.

- **Missing `SmallStep`/`LargeStep` distinction**: Spec requires both `SmallStep` (drag increment) and `LargeStep` (major tick interval) as separate required params; implementation uses `Step` for drag and `LargeStep` for ticks only.
- **Missing `TickPosition` control**: Spec allows `Before`, `After`, `Both`, `None` tick positioning; implementation always renders ticks when `LargeStep` is set.
- **Missing `Decimals` parameter**: Spec provides precision control to avoid floating-point rounding errors.
- **Missing `Enabled`/`Disabled`**: Spec has an `Enabled` parameter; implementation has no disabled state.
- **Missing generic type**: Spec's RangeSlider is generic over numeric types; implementation uses `double` only.

---

## 7. SunfishRating

**Current**: `Value`/`ValueChanged` (int), `Max`, `IsReadOnly`. Renders filled/unfilled star icons with button or span based on read-only state.

- **Missing `PrecisionMode`**: Spec supports `Full` and `Half` precision (half-star ratings); implementation only supports whole integers.
- **Missing `SelectionMode`**: Spec supports `Continuous` (fill all up to selected) and `Single` (highlight only selected); implementation is continuous only.
- **Missing `Label` parameter**: Spec renders an optional label element next to the rating.
- **Missing `Enabled` vs `ReadOnly`**: Spec separates `Enabled` (interactive but can be disabled) from `ReadOnly`; implementation only has `IsReadOnly`.
- **Value type mismatch**: Spec uses `double` for `Value` (needed for half-precision); implementation uses `int`.

---

## 8. SunfishSearchBox

**Current**: `Value`/`ValueChanged`, `Placeholder`, `OnSearch`, `OnEscape`, `Disabled`, `KbdHint`. Wraps `SunfishTextField` with search icon prefix, clear button, and kbd hint.

- **Well-aligned with spec**: All parameters from the docfx spec are implemented (`Value`, `Placeholder`, `KbdHint`, `OnSearch`, `OnEscape`, `Disabled`).
- **Missing debounce/auto-search**: No debounced search-as-you-type; `OnSearch` only fires on Enter.
- **Missing search suggestions/autocomplete**: No dropdown with search suggestions or recent searches.
- **Missing `Loading` state**: No visual indicator while search results are being fetched.
- **No `aria-role="search"`**: The wrapping element does not use a `search` landmark role for accessibility.
