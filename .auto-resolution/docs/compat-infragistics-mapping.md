# compat-infragistics Parameter Mapping & Divergence Log

> **Audit trail — treated as public API.** Consumers rely on this document to understand
> what their Ignite UI code maps to after migration. Any change to an entry (promoting a
> parameter from "mapped" to "throws", changing a default, adding a divergence) is a
> **breaking change** for consumers and must land under the policy gate in the same PR as
> the code change. See `packages/compat-infragistics/POLICY.md`.

## Scope

`Sunfish.Compat.Infragistics` provides source-code shape parity with Infragistics Ignite UI
for Blazor (`IgniteUI.Blazor.Controls`), wrapping canonical Sunfish adapter components. It
does NOT provide visual or behavioral parity. See
`packages/compat-infragistics/POLICY.md`.

**Coverage:** 11 main wrappers (Form dropped — Ignite UI ships no `IgbForm`, consumers use
Blazor `EditForm` natively) + 1 child-component shim (`IgbSelectItem`) + 1 grid-column shim
(`IgbColumn`) + 5 EventArgs shims.

## Conventions

- **Mapped** — Ignite UI parameter value translates 1:1 to a Sunfish parameter / value.
- **Forwarded** — Ignite UI attribute is passed through via `AdditionalAttributes` (e.g.
  `name`, `required`, `aria-invalid`). No semantic transform.
- **Dropped (cosmetic)** — Silently ignored; logged via ILogger at Warning level. Reserved
  for parameters with no functional impact.
- **Throws** — Raises `NotSupportedException` with a migration hint. Applies to values that
  would silently change behavior if dropped.
- **LogAndFallback** — Unrecognized value (not in the mapping table) logs a warning and
  falls back to the Sunfish default.

## Global Ignite-UI Divergences

Captured once here; each per-component table may reference these:

- **Form dropped.** Ignite UI Blazor has no `IgbForm` component. Consumers use Blazor's
  standard `EditForm` with Ignite UI inputs as children. `compat-infragistics` does not ship
  a Form wrapper; migrating consumers either keep `EditForm` or migrate to `SunfishForm`.
- **`Checked` vs `Value` on checkbox.** Ignite UI's `IgbCheckbox` uses the `Checked` /
  `CheckedChanged` parameter pair (Sunfish uses `Value` / `ValueChanged`). The shim preserves
  Ignite UI's naming at the public surface.
- **`--ig-size` CSS-variable sizing.** Ignite UI controls component sizing via the
  `--ig-size` CSS custom property (not a direct Blazor parameter on most components). The
  shim accepts a `Size` parameter on `IgbButton` where it maps to Sunfish's `ButtonSize`;
  elsewhere, consumers can pass `style="--ig-size: 3"` via `AdditionalAttributes` — the
  token is forwarded verbatim but Sunfish ignores it (no runtime effect).
- **Icon registry (`IgbIcon.RegisterIconFromText` / `RegisterIcon`).** Ignite UI registers
  icons against its WC runtime's global registry. compat-infragistics re-implements these as
  static methods against a process-local dictionary so consumer calls at `Program.cs`-level
  compile and retain some behavior (registered SVG renders via `AddMarkupContent`).
- **Polarity flip on `IgbDialog.KeepOpenOnEscape`.** Ignite UI's `KeepOpenOnEscape=true`
  means "Escape does NOT close." The inverse polarity vs Telerik's `CloseOnEscape=false`.
  Currently cosmetic-dropped; Sunfish's window defaults to close-on-Escape.
- **IgbTooltip parameter list.** Upstream docs page returned 404 at Stage 01. The parameter
  set here reflects the underlying `igc-tooltip` WC inventory and will need runtime
  re-verification against `components/Blazor/Tooltip.cs` before Phase 2 sign-off.
- **Web Components backend.** Ignite UI Blazor emits `<igc-*>` custom element tags that are
  upgraded by the `igniteui-webcomponents` Lit-based library. `compat-infragistics` does
  NOT emit `<igc-*>` tags — wrappers render canonical Sunfish components directly. Shadow
  DOM / Lit / WC JS interop are entirely absent from the consumer's build because this
  package does not reference `IgniteUI.Blazor`. See
  `icm/01_discovery/output/infragistics-wc-architecture-spike-2026-04-22.md`.

---

## IgbButton

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbButton`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton`

| Ignite UI parameter | Type | Sunfish target | Mapping |
|---|---|---|---|
| `Variant` | `ButtonVariant` (Flat/Contained/Outlined/Fab) | `FillMode` | `Contained→Solid`, `Outlined→Outline`, `Flat→Flat`, `Fab→Solid` (cosmetic-dropped). |
| `Type` | `ButtonBaseType` (Button/Submit/Reset) | `ButtonType` | `Button→Button`, `Submit→Submit`, **`Reset→throws`**. |
| `Disabled` | `bool` | `Enabled` (inverted) | `Disabled=true` ⇒ `Enabled=false`. |
| `Size` | `string` ("large"/"medium"/"small") | `ButtonSize` | `"large"→Large`, `"medium"→Medium`, `"small"→Small`. LogAndFallback to Medium. |
| `Href`, `Target` | `string` | — | Forwarded via `AdditionalAttributes["data-href"]` / `data-target`. Sunfish has no anchor-button semantics; consumers should migrate to an `NavigationManager` OnClick handler. |
| `Name`, `Value` | `string` | — | Forwarded via `AdditionalAttributes`. |
| `Click` | `EventCallback<MouseEventArgs>` | `OnClick` | Mapped. |
| `ChildContent` | `RenderFragment?` | `ChildContent` | Passthrough. |

### Divergences

- No `ThemeColor` parameter exposed — Ignite UI theming is via `--ig-primary` etc. Default
  Sunfish variant is `Primary`.
- `Fab` variant has no direct Sunfish equivalent; shim falls back to `Contained`.

---

## IgbIcon

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbIcon`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Utility.SunfishIcon`

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `IconName` | `string?` | Primary identifier — looked up in the process-local registry; on miss, passed to `SunfishIcon.Name` as a font-icon name. |
| `Collection` | `string?` | Collection namespace for registry lookup (defaults to `"default"`). |
| `Mirrored` | `bool` | Dropped with warning (Sunfish has no analog). |
| `AriaLabel` | `string?` | Passthrough. |

### Static methods

- `IgbIcon.RegisterIconFromText(name, svg, collection?)` — records SVG markup in the
  process-local dictionary; subsequent renders of `IgbIcon` with the matching
  `IconName`/`Collection` emit the recorded markup via `AddMarkupContent`.
- `IgbIcon.RegisterIcon(name, url, collection?)` — records an `<img>` tag referencing the
  URL in the process-local dictionary. Consumers migrating for long-term maintenance should
  move to `SunfishIcon`'s native identifier model.

### Divergences

- No round-trip to the Ignite UI WC library icon registry. Icons registered via these
  methods are Blazor-side only and live as markup strings (not as cached WC-registered
  SVGs). Icon styling via `--ig-color` etc. does not apply.

---

## IgbCheckbox

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbCheckbox`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishCheckbox`

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `Checked` | `bool` | Mapped to Sunfish `Value`. |
| `CheckedChanged` | `EventCallback<bool>` | Mapped to Sunfish `ValueChanged`. |
| `Change` | `EventCallback<IgbInputChangeEventArgs>` | Bridged — fires after `CheckedChanged` with `{ Value, Name }`. |
| `Indeterminate` | `bool` | Passthrough. |
| `Disabled` | `bool` | Inverted to Sunfish `Enabled`. |
| `Required`, `Invalid`, `AriaLabelledby` | `bool`/`string?` | Forwarded via `AdditionalAttributes`. |
| `Name`, `Value` | `string?` | Forwarded via `AdditionalAttributes`. |
| `LabelPosition` | `CompatEnums.LabelPosition` | `Before` dropped with warning; Sunfish always renders label after. |

### Divergences

- **Divergence (high visibility):** `Checked` vs Sunfish's `Value`. The shim preserves
  Ignite UI's `Checked` / `CheckedChanged` naming; consumer markup using `@bind-Checked` or
  `Checked="@isOn"` continues to compile.

---

## IgbInput

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbInput`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishTextBox`

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `Value`, `ValueChanged` | `string?` / `EventCallback<string?>` | Passthrough. |
| `Change` | `EventCallback<IgbInputChangeEventArgs>` | Bridged. |
| `DisplayType` | `InputType` enum | Rendered as HTML `type` attribute. |
| `InputMode`, `Pattern`, `Autocomplete` | `string?` | Forwarded via `AdditionalAttributes`. |
| `MinLength`, `MaxLength` | `double?` | `MaxLength` coerced to `int` for Sunfish (Ignite UI uses `double` to match WC attribute typing). `MinLength` forwarded as `minlength` attribute. |
| `Min`, `Max`, `Step` | `double?` | Forwarded as HTML attributes. |
| `Autofocus` | `bool` | Forwarded via `AdditionalAttributes`. |
| `Label` | `string?` | Falls back to Placeholder if Placeholder is null. |
| `Placeholder`, `Required`, `Disabled`, `Readonly`, `Invalid` | — | Standard passthrough. |
| `ValidateOnly` | `bool` | Dropped (cosmetic). |

### Divergences

- `IgbInput` covers Ignite UI's text + email + password + numeric + search surfaces. For
  native numeric inputs consumers should migrate to `SunfishNumericInput` directly — the
  shim currently renders the HTML5 numeric input type inside a text box.

---

## IgbSelect & IgbSelectItem

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbSelect` + `IgbSelectItem`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDropDownList<TItem,TValue>`

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `Value`, `ValueChanged` | `TValue?` / `EventCallback` | Mapped. |
| `Change` | `EventCallback<IgbInputChangeEventArgs>` | Bridged. |
| `Label`, `Placeholder`, `Disabled`, `Required`, `Invalid`, `Name` | — | Standard passthrough/forwarding. |
| `Open` | `bool` | Dropped with warning. |
| `SameWidth` | `bool` | Dropped (cosmetic). |
| `Placement`, `DistanceFromTarget`, `FlipStrategy` | — | Dropped with warning. |

### Child component: `IgbSelectItem<TValue>`

- Child-of-IgbSelect shim. Buffers `{ Value, Text, Selected, Disabled }` into the parent's
  option list at render time; the parent binds the buffered list as
  `SunfishDropDownList.Data`.
- Throws `InvalidOperationException` when rendered outside an `IgbSelect`.
- `Text` parameter is required when the display text differs from `Value.ToString()` —
  `ChildContent` is NOT evaluated synchronously to extract display text.
- `IgbSelectHeader` / `IgbSelectGroup` are NOT shimmed in Phase 0. Consumers using these
  should migrate to Sunfish's `DropDownList.Data` with grouping on the row model.

---

## IgbCombo

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbCombo`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishComboBox<TItem,TValue>`

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `Data` | `IEnumerable<TItem>?` | Passthrough. |
| `Value` | `object?` (scalar single-select, array multi-select) | Scalar mapped; multi-select logged as unsupported. |
| `ValueChanged` | `EventCallback<TValue?>` | Scalar single-select binding. |
| `DisplayKey` | `string?` | Mapped to Sunfish `TextField`. |
| `ValueKey` | `string?` | Mapped to Sunfish `ValueField`. |
| `GroupKey` | `string?` | Dropped with warning — Sunfish ComboBox has no grouping surface. |
| `Label`, `Placeholder`, `PlaceholderSearch`, `Outlined` | — | Standard passthrough/forwarding. |
| `SingleSelect` | `bool` (default true) | `false` → warning; no multi-select shim; migrate to `SunfishMultiSelect`. |
| `Open`, `Autofocus`, `Disabled`, `Required`, `Invalid`, `Name` | — | Standard passthrough/forwarding. |

---

## IgbDatePicker

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbDatePicker`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Forms.Inputs.SunfishDatePicker`

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `Value`, `ValueChanged` | `DateTime?` | Passthrough. |
| `Change` | `EventCallback<IgbInputChangeEventArgs>` | Bridged. |
| `Min`, `Max` | `DateTime?` | Passthrough. |
| `DisplayFormat` | `string?` | Mapped to Sunfish `Format`. |
| `InputFormat` | `string?` | Mapped to Sunfish `Format` when DisplayFormat is unset. |
| `Mode` | `PickerMode` (Dialog/DropDown) | Dropped with warning — Sunfish uses a single style. |
| `Locale`, `DisabledDates`, `WeekStart` | — | Dropped with warning. |
| `Open`, `KeepOpenOnSelect`, `Outlined`, `Placeholder` | — | Dropped with warning. |

### Unsupported / throws

- `ShowAsync()` / `HideAsync()` / `ClearAsync()` / `StepUpAsync()` / `StepDownAsync()` — not
  shimmed; consumers should drive visibility via `@bind-Value` and Sunfish's standard
  control model.

---

## IgbDialog

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbDialog`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Overlays.SunfishWindow`

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `Open` | `bool` | Mapped to Sunfish `Visible`. |
| `OpenChanged` | `EventCallback<bool>` | Mapped to Sunfish `VisibleChanged`. |
| `Title` | `string?` | Passthrough. |
| `HideDefaultAction` | `bool` | Dropped with warning. |
| `KeepOpenOnEscape` | `bool` | **Polarity flip from Telerik's `CloseOnEscape`.** Currently dropped with warning. |
| `CloseOnOutsideClick` | `bool` (default true) | Dropped with warning when `false`. |
| `ReturnValue` | `string?` | Dropped (Ignite UI form-return-value pattern has no Sunfish analog). |

### Imperative methods

- `ShowAsync()` / `HideAsync()` / `ToggleAsync()` — implemented on the shim; they update
  `Open` and invoke `OpenChanged`. Consumers using `dialogRef.ShowAsync()` continue to work.

---

## IgbTooltip

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbTooltip`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishTooltip`

> Parameter list needs runtime re-verification — upstream docs page was 404 at Stage 01.
> Parameters listed here reflect the underlying `igc-tooltip` WC inventory.

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `Open` | `bool` | Passthrough (Sunfish's tooltip visibility is trigger-driven). |
| `Anchor` | `string?` (element id / selector) | Mapped to Sunfish `TargetSelector`. |
| `Placement` | `IgbPlacement` enum | Mapped to Sunfish `TooltipPosition` (Top/Bottom/Left/Right; variants collapse to their cardinal direction). |
| `Content` | `string?` | Mapped to Sunfish `Text`. |
| `ChildContent` | `RenderFragment?` | Passthrough. |
| `Offset`, `ShowDelay`, `HideDelay`, `DisableArrow`, `Sticky` | — | Dropped with warning. |

---

## IgbToast

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbToast`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.Feedback.SunfishSnackbarHost`

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `Open` | `bool` | Passthrough. |
| `DisplayTime` | `int` (ms) | Passthrough (Ignite UI default 4000). |
| `KeepOpen` | `bool` | Dropped with warning — Sunfish controls auto-hide per-message. |
| `Position` | `ToastPosition` (Bottom/Middle/Top) | Translated to Sunfish vertical/horizontal positions; `Middle` falls back to Bottom. |

### Related: IgbSnackbar (not shipped in Phase 0)

Ignite UI ships both `IgbToast` and `IgbSnackbar` as notification-family components. Phase
0 ships only `IgbToast`; `IgbSnackbar` is a candidate for future coverage under the policy
gate (see POLICY.md §Coverage).

---

## IgbGrid & IgbColumn  (COMMERCIAL)

- **Ignite UI target:** `IgniteUI.Blazor.Controls.IgbGrid` + `IgbColumn`
- **Sunfish target:** `Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishDataGrid<TItem>` + `SunfishGridColumn<TItem>`

### License note

The underlying `igniteui-webcomponents` Data Grid is **commercial-licensed**. compat-
infragistics declares the API shape WITHOUT pulling the commercial runtime. Future changes
that go beyond shape-parity delegation require explicit BDFL policy sign-off (see
`POLICY.md` §IgbGrid BDFL clause).

### IgbGrid parameters

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `Id`, `Class` | `string?` | Forwarded via `AdditionalAttributes`. |
| `Data` | `IEnumerable<TItem>?` | Passthrough. |
| `Height`, `Width` | `string?` | Forwarded as inline style. |
| `AutoGenerate` | `bool` | Dropped with warning — declare columns explicitly with `<IgbColumn>`. |
| `AllowFiltering`, `AllowAdvancedFiltering` | `bool` | `AllowFiltering` drops through; `AllowAdvancedFiltering` logs a warning. |
| `Paging` | `bool` | Not shimmed in Phase 0. Migrate to `SunfishDataGrid` paging directly. |
| `RowSelection`, `CellSelection` | `string?` | Not shimmed in Phase 0. |
| `PrimaryKey` | `string?` | Not shimmed (cosmetic — Sunfish uses row references implicitly). |

### IgbColumn parameters

| Ignite UI parameter | Type | Mapping |
|---|---|---|
| `Field` | `string?` | Passthrough (coerced to empty string if null). |
| `Header` | `string?` | Mapped to Sunfish `Title`. |
| `Sortable`, `Filterable`, `Editable`, `Resizable`, `Hidden`, `Width` | — | Standard passthrough. |
| `Template` | `RenderFragment<TItem>?` | Passthrough to `SunfishGridColumn.Template`. |
| `HeaderTemplate` | `RenderFragment?` | Passthrough. |
| `BodyTemplateScript` | `string?` | **Throws `NotSupportedException`**. Migrate to `Template` (RenderFragment). |
| `InlineEditorTemplateScript` | `string?` | **Throws `NotSupportedException`**. Migrate to `EditorTemplate` (RenderFragment). |

---

## EventArgs Types

All shipped in the root `Sunfish.Compat.Infragistics` namespace (matches Ignite UI's flat
layout).

| Type | Shape highlights |
|---|---|
| `IgbInputChangeEventArgs` | `Value` (object?), `Name` (string?). Used by checkbox/input/select `Change` events. |
| `IgbGridRowClickEventArgs` | `RowData` (object?), `EventArgs`, `Field`. Ignite UI erases row type to object. |
| `IgbGridCellClickEventArgs` | `RowData`, `CellValue`, `Field`, `EventArgs`. |
| `IgbGridSelectionEventArgs` | `NewSelection`, `OldSelection` (IEnumerable<object>?), `Cancel`. |
| `IgbGridSortingEventArgs` | `Field`, `Direction` (`"asc"/"desc"/"none"`), `Cancel`. |

Functional wiring from the grid wrapper to these types arrives in a future policy-gated PR;
types ship now so consumer handler signatures compile after the using-swap.

---

## Unsupported / Throws (summary)

| Location | Behavior |
|---|---|
| `IgbButton.Type = Reset` | Throws `NotSupportedException`. |
| `IgbColumn.BodyTemplateScript` | Throws — migrate to RenderFragment `Template`. |
| `IgbColumn.InlineEditorTemplateScript` | Throws — migrate to RenderFragment `EditorTemplate`. |
| `IgbDatePicker.ShowAsync/HideAsync/ClearAsync/StepUpAsync/StepDownAsync` | Not shimmed (future-gated). |
| `IgbSelectItem` rendered outside `IgbSelect` | Throws `InvalidOperationException`. |

## Cross-references

- `packages/compat-infragistics/POLICY.md` — policy gate + licensing + WC-backend note
- `packages/compat-telerik/` — pattern reference; divergences above mirror the compat-telerik
  divergence-log format one-for-one.
- `icm/01_discovery/output/compat-infragistics-surface-inventory-2026-04-22.md` — surface
  spec driving this doc.
- `icm/01_discovery/output/infragistics-wc-architecture-spike-2026-04-22.md` — Decision-2
  spike that confirmed the standard delegation pattern works.
