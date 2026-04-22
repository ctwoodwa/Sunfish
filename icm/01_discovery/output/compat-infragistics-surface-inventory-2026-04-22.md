# compat-infragistics — Surface Inventory & Mapping Skeleton

**Date:** 2026-04-22
**Stage:** 01 Discovery
**Parent intake:** [`icm/00_intake/output/compat-expansion-intake.md`](../../00_intake/output/compat-expansion-intake.md)
**Task IDs:** #103 (Discovery), #108 (compat-infragistics package — spike sub-task)
**Companion doc:** [`infragistics-wc-architecture-spike-2026-04-22.md`](infragistics-wc-architecture-spike-2026-04-22.md) (Decision-2 WC spike full outcome)

---

## Executive Summary

- **Vendor library:** Infragistics Ignite UI for Blazor (`IgniteUI.Blazor` NuGet; namespace `IgniteUI.Blazor.Controls`).
- **Component prefix:** `Igb*` (e.g. `IgbButton`, `IgbGrid`, `IgbDialog`).
- **Underlying tech:** The `Igb*` Blazor classes are **thin C# wrappers that emit custom-element tags** (`<igc-button>`, `<igc-grid>`, `<igc-dialog>`) and delegate rendering + behavior to the `igniteui-webcomponents` Lit-based WC library (`~6.3.6` as of this discovery). Confirmed via source inspection of `BaseRendererControl.cs` and `Button.cs` in `github.com/IgniteUI/igniteui-blazor`.
- **12-component inventory:** All 12 targets have a direct `Igb*` equivalent. Only **Form** has no Igb* component (consumers use Blazor `EditForm`); everything else maps 1:1.
- **Spike outcome (full rationale in companion doc):** **YES — standard delegation pattern works.** The compat wrapper stays on the Blazor side; it never emits `<igc-*>` tags; Shadow DOM / Lit / WC interop are irrelevant to the shim.
- **Licensing:** `igniteui-blazor` (the Blazor wrapper repo) is **MIT-licensed** (`github.com/IgniteUI/igniteui-blazor/blob/master/LICENSE`). Underlying standard web components are also MIT; only **Grids (Data/Tree/Hierarchical/Pivot) and Dock Manager are commercial**. This is a materially more permissive licensing posture than Telerik / Syncfusion / DevExpress.

---

## 1. Component Surface Inventory

Source: `infragistics.com/products/ignite-ui-blazor` docs, `github.com/IgniteUI/igniteui-blazor/tree/master/components/Blazor`, and `github.com/IgniteUI/igniteui-webcomponents`.

### 1.1 Button — `IgbButton` → `<igc-button>`

| Field | Value |
|---|---|
| Blazor class | `IgbButton` |
| WC tag | `<igc-button>` |
| Source file | `components/Blazor/Button.cs` (C#, not `.razor`) |
| Base class | `IgbButtonBase` → `BaseRendererControl` |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton` |

**Top parameters:**
- `Variant` (enum `ButtonVariant`: `Flat`, `Contained`, `Outlined`, `Fab`) — analog of Telerik `FillMode`.
- `Type` (enum `ButtonBaseType`: `Button`, `Reset`, `Submit`) — analog of Telerik `ButtonType`; same `Reset` divergence will apply.
- `Disabled` (bool) — opposite polarity to Sunfish `Enabled`.
- `Size` (`Large`, `Medium`, `Small`) — Igb size values appear to come from a separate mechanism (`--ig-size` CSS custom property) rather than a direct parameter on `IgbButton`; verify at Stage 02.
- `Href` (string) — anchor-style button (Telerik has no direct equivalent; Sunfish `ButtonType` extension may be needed, or compat ignores this).
- `Target` (enum `ButtonBaseTarget`) — anchor target.
- `Name` / `Value` — form-participation attributes.
- `Click` event — `EventCallback` analog of Telerik `OnClick`.

**Divergences from Telerik shape:**
- No `ThemeColor` equivalent — `Variant` conflates visual style + chrome. Mapping: `"primary"`-like semantic must be routed via Igb's `--ig-primary` theme color (out of compat scope) OR via an explicit `Variant` → `Sunfish ButtonVariant` table.
- No `Rounded` — Igb corners are theme-controlled, not parameter-controlled.
- No `Icon` parameter — Igb pattern is `<IgbIcon>` as child content, not a Telerik-style `Icon="..."` scalar.

---

### 1.2 Icon — `IgbIcon` → `<igc-icon>`

| Field | Value |
|---|---|
| Blazor class | `IgbIcon` |
| WC tag | `<igc-icon>` |
| Source file | `components/Blazor/Icon.cs` |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.Icons.SunfishIcon` (or icon RenderFragment adapter) |

**Top parameters:**
- `IconName` (string) — named icon reference.
- `Collection` (string) — icon-set identifier; icons are pre-registered under named collections.
- `Mirrored` (bool) — RTL mirror.
- `Size` — CSS-variable-driven, not a direct parameter.

**Icon registration model:**
- Static helpers `IgbIcon.RegisterIcon(name, url, collection)` and `IgbIcon.RegisterIconFromText(name, svg, collection)` — these are **interop-time calls** that round-trip to the WC library's icon registry. Compat shim implication: if a migrating consumer has `IgbIcon.RegisterIconFromText(...)` in `Program.cs`, the compat shim either:
  - A) Re-implements registration against a Sunfish icon registry (preferred).
  - B) No-ops + logs (acceptable if Sunfish uses a different icon system entirely).
- Decision deferred to Stage 02 Architecture.

---

### 1.3 CheckBox — `IgbCheckbox` → `<igc-checkbox>`

| Field | Value |
|---|---|
| Blazor class | `IgbCheckbox` |
| WC tag | `<igc-checkbox>` |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.Forms.SunfishCheckbox` |

**Top parameters:** `Checked` (bool), `Indeterminate` (bool), `Disabled` (bool), `Required` (bool), `Invalid` (bool), `Name` (string), `Value` (string), `LabelPosition` (enum: `Before`, `After`), `AriaLabelledby` (string).

**Events:** `Change` (`CustomEventArgs`) — WC-side `change` event bubbled through JS interop.

**Divergences:** Telerik's `TelerikCheckBox` takes `Value`/`ValueChanged` for two-way binding on the boolean state. `IgbCheckbox` uses `Checked`/`ChangeCallback`. Mapping is straightforward but parameter-name-renamed.

---

### 1.4 TextBox — `IgbInput` → `<igc-input>`

| Field | Value |
|---|---|
| Blazor class | `IgbInput` |
| WC tag | `<igc-input>` |
| Source file | `components/Blazor/Input.cs` |
| Base class | `IgbInputBase` |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.Forms.SunfishTextBox` |

**Top parameters:**
- `Value` (string), `DisplayType` (enum `InputType`: `Text`, `Email`, `Password`, etc.), `InputMode` (string), `Pattern` (string), `MinLength`/`MaxLength` (double), `Min`/`Max`/`Step` (double), `Autofocus` (bool), `Autocomplete` (string), `ValidateOnly` (bool), `Label` (string), `Placeholder` (string), `Required` (bool), `Disabled` (bool), `Readonly` (bool).
- Methods: `SelectAsync()`, `StepUpAsync()`, `StepDownAsync()` + sync variants — interop-returning.
- Event: `ValueChanged`, `Change`.

**Note:** `IgbInput` covers Telerik's `TelerikTextBox` + `TelerikNumericTextBox` + `TelerikMaskedTextBox` overlap. For a single compat shim, `IgbInput` → `SunfishTextBox` is the likely minimum; numeric/masked variants would need separate wrappers if consumers use them.

---

### 1.5 DropDownList — `IgbSelect` → `<igc-select>`

| Field | Value |
|---|---|
| Blazor class | `IgbSelect` |
| WC tag | `<igc-select>` |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.Forms.SunfishDropDownList` |

**Top parameters:** `Value` (string), `Label` (string), `Placeholder` (string), `Disabled` (bool), `Required` (bool), `Name` (string), `Invalid` (bool), `Open` (bool), `SameWidth` (bool), `FlipStrategy` (enum), `DistanceFromTarget` (double), `Placement` (enum).

**Child components:**
- `IgbSelectItem` — a single option; parameters `Value` (string), `Selected` (bool), `Disabled` (bool), `DisableRipple` (bool).
- `IgbSelectHeader` — visual header in the dropdown panel (not a selectable item).
- `IgbSelectGroup` — wraps `IgbSelectItem`s for visual grouping.

**Compat implication:** Child-component shimming (Decision-4 gap-closure prerequisite) covers this shape. `IgbSelectItem` maps to whatever `SunfishDropDownList` uses for options — likely a `Data`-binding model, not a child-items model. Two shim options: (a) buffer `IgbSelectItem`s into a collection at render time and bind to `SunfishDropDownList.Data`, or (b) declare `IgbSelectItem` as a no-op marker and require manual data migration. Decision deferred to Stage 02.

---

### 1.6 ComboBox — `IgbCombo` → `<igc-combo>`

| Field | Value |
|---|---|
| Blazor class | `IgbCombo` |
| WC tag | `<igc-combo>` |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.Forms.SunfishComboBox` |

**Top parameters:** `Data` (array of objects), `Value` (object/array), `DisplayKey` (string), `ValueKey` (string), `GroupKey` (string), `Label` (string), `Placeholder` (string), `SingleSelect` (bool), `Outlined` (bool), `PlaceholderSearch` (string), `Open` (bool), `Autofocus` (bool), `Disabled` (bool), `Required` (bool), `Invalid` (bool).

**Mapping is cleaner than `IgbSelect` because `IgbCombo` uses `Data` + key strings rather than child components.** `Data` → `SunfishComboBox.Data`, `DisplayKey` → `TextField`, `ValueKey` → `ValueField`.

---

### 1.7 DatePicker — `IgbDatePicker` → `<igc-date-picker>`

| Field | Value |
|---|---|
| Blazor class | `IgbDatePicker` |
| WC tag | `<igc-date-picker>` |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.Forms.SunfishDatePicker` |

**Top parameters:** `Value` (DateTime?), `Mode` (enum `PickerMode`: `Dialog`, `DropDown`), `Label` (string), `Min` (DateTime), `Max` (DateTime), `DisabledDates` (collection), `WeekStart` (enum), `DisplayFormat` (string), `InputFormat` (string), `Locale` (string), `Placeholder` (string), `Open` (bool), `KeepOpenOnSelect` (bool), `Outlined` (bool).

**Methods:** `ShowAsync()`, `HideAsync()`, `ClearAsync()`, `StepUpAsync()`, `StepDownAsync()`.

---

### 1.8 Form — **NO direct `Igb*` equivalent**

**Finding:** Ignite UI Blazor does not ship an `IgbForm` component. The documented form-patterns all use **standard Blazor `EditForm`** with data-annotation validators and Igb inputs as children. This is a materially different shape from Telerik's `TelerikForm`.

**Compat implication:**
- If the migration source actually uses `TelerikForm`-style markup, there is no Igb equivalent to compat-shim from. Migrating consumers from Ignite UI never had a `TelerikForm`-shaped component to migrate from.
- **Recommendation:** Drop Form from the 12-component compat-infragistics target; document this in the mapping doc as "Ignite UI Blazor ships no Form component — use `EditForm` + Sunfish inputs directly, or `SunfishForm` if migrating to Sunfish-native." Reduces compat-infragistics target to 11 components.

---

### 1.9 Grid — `IgbGrid` → `<igc-grid>` (**commercial**)

| Field | Value |
|---|---|
| Blazor class | `IgbGrid` (generic closes to `IgbGrid<T>` via `Data` binding) |
| WC tag | `<igc-grid>` |
| License | **Commercial** (not MIT) — see §3 Licensing. |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.DataGrid.SunfishGrid<T>` |

**Top parameters:** `Id` (string), `Data` (IEnumerable), `AutoGenerate` (bool — auto-columns from data shape), `AllowFiltering` (bool), `AllowAdvancedFiltering` (bool), `Paging` (bool / child config), `RowSelection` (enum), `CellSelection` (enum), `PrimaryKey` (string), `Height` (string), `Width` (string), `Class` (string).

**Child components:** `IgbColumn` — `Field` (string), `Header` (string), `Sortable` (bool), `Filterable` (bool), `Editable` (bool), `Resizable` (bool), `Hidden` (bool), `Width` (string), `BodyTemplateScript` (string — references a JS template function), `InlineEditorTemplateScript` (string).

**Notable:** Column templates are **JS-side references** (a function name registered in JS scope), not Blazor `RenderFragment`s. This is a direct consequence of the underlying WC architecture — the column body is rendered inside the custom element's shadow root, outside Blazor's render tree. **For a compat shim that delegates to `SunfishGrid`, this script-template surface is the hardest parameter to map** — it has no Blazor-semantic equivalent, and simply ignoring it breaks column bodies. Decision deferred to Stage 02; likely "Unsupported, throws with migration hint 'use RenderFragment-based column templates on SunfishGrid'."

**Grid EventArgs:** `IgbGridRowClickEventArgs`, `IgbGridCellClickEventArgs`, `IgbGridSelectionEventArgs`, `IgbGridSortingEventArgs` — same shape as Telerik gap-closure (Decision-4) will address.

---

### 1.10 Window — `IgbDialog` → `<igc-dialog>`

| Field | Value |
|---|---|
| Blazor class | `IgbDialog` |
| WC tag | `<igc-dialog>` |
| Source file | `components/Blazor/Dialog.cs` (confirmed via GitHub) |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.Overlays.SunfishWindow` (or `SunfishDialog`, whichever is canonical) |

**Top parameters:** `Open` (bool), `Title` (string), `HideDefaultAction` (bool), `KeepOpenOnEscape` (bool), `CloseOnOutsideClick` (bool), `ReturnValue` (string).

**Methods:** `ShowAsync()`, `HideAsync()`, `ToggleAsync()`.

**Note:** Polarity inversion vs. Telerik: `KeepOpenOnEscape=true` ↔ Telerik `CloseOnEscape=false`. Compat shim needs the polarity flip in the parameter mapping.

---

### 1.11 Tooltip — `IgbTooltip` → `<igc-tooltip>`

| Field | Value |
|---|---|
| Blazor class | `IgbTooltip` |
| WC tag | `<igc-tooltip>` |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.Overlays.SunfishTooltip` |

**Top parameters (inferred from WC library + docs):** `Open` (bool), `Anchor` (element ref / id), `Placement` (enum), `Offset` (double), `ShowDelay` (int ms), `HideDelay` (int ms), `DisableArrow` (bool), `Sticky` (bool).

(Direct docs page returned 404 during spike; parameter set inferred from `igc-tooltip` WC docs. Stage 02 Architecture should re-verify via `components/Blazor/Tooltip.cs` source.)

---

### 1.12 Notification — `IgbToast` → `<igc-toast>` (primary) + `IgbSnackbar` → `<igc-snackbar>` (alternative)

| Field | Value (Toast) | Value (Snackbar) |
|---|---|---|
| Blazor class | `IgbToast` | `IgbSnackbar` |
| WC tag | `<igc-toast>` | `<igc-snackbar>` |
| Sunfish equivalent | `Sunfish.UIAdapters.Blazor.Components.Overlays.SunfishNotification` | (same) |

**`IgbToast` top parameters:** `Open` (bool), `DisplayTime` (int, default 4000), `KeepOpen` (bool), `Position` (enum).
**`IgbSnackbar` top parameters:** `Open` (bool), `DisplayTime` (int, default 4000), `KeepOpen` (bool), `ActionText` (string).

**Recommendation:** Compat-infragistics exposes `IgbToast` as the primary Notification shim; `IgbSnackbar` optionally as a second wrapper. Both map to `SunfishNotification` with different parameter translation (Snackbar's `ActionText` is a richer surface that may require a Sunfish `NotificationAction` RenderFragment).

---

### 1.13 Inventory Summary Table

| # | Target | Igb class | WC tag | Sunfish analog | License | Spike risk |
|---|---|---|---|---|---|---|
| 1 | Button | `IgbButton` | `<igc-button>` | `SunfishButton` | MIT | low |
| 2 | Icon | `IgbIcon` | `<igc-icon>` | `SunfishIcon` | MIT | medium (registry) |
| 3 | CheckBox | `IgbCheckbox` | `<igc-checkbox>` | `SunfishCheckbox` | MIT | low |
| 4 | TextBox | `IgbInput` | `<igc-input>` | `SunfishTextBox` | MIT | low |
| 5 | DropDownList | `IgbSelect` | `<igc-select>` | `SunfishDropDownList` | MIT | medium (child items) |
| 6 | ComboBox | `IgbCombo` | `<igc-combo>` | `SunfishComboBox` | MIT | low |
| 7 | DatePicker | `IgbDatePicker` | `<igc-date-picker>` | `SunfishDatePicker` | MIT | low |
| 8 | Form | *(none)* | — | `SunfishForm` | n/a | **descope** |
| 9 | Grid | `IgbGrid` | `<igc-grid>` | `SunfishGrid<T>` | **commercial** | high (JS templates) |
| 10 | Window | `IgbDialog` | `<igc-dialog>` | `SunfishWindow` | MIT | low (polarity flip) |
| 11 | Tooltip | `IgbTooltip` | `<igc-tooltip>` | `SunfishTooltip` | MIT | low |
| 12 | Notification | `IgbToast` | `<igc-toast>` | `SunfishNotification` | MIT | low |

**Revised target:** **11 components** (Form dropped). Stage 02 Architecture may optionally propose adding `IgbSnackbar` as a 12th to preserve the count, OR add `IgbIconButton` / `IgbTreeGrid` / `IgbDatePicker`-variants as replacements.

---

## 2. Mapping-Doc Skeleton

Companion to the eventual `docs/compat-infragistics-mapping.md`. Same structure as `docs/compat-telerik-mapping.md`, with one vendor-specific section.

```markdown
# compat-infragistics Mapping Reference

## Scope

`Sunfish.Compat.Infragistics` provides source-code shape parity with Infragistics
Ignite UI for Blazor (`IgniteUI.Blazor.Controls`), wrapping canonical Sunfish
adapter components. It does NOT provide visual or behavioral parity. See
`packages/compat-infragistics/POLICY.md`.

## Per-Component Mapping

### IgbButton
  Upstream: IgniteUI.Blazor.Controls.IgbButton
  Delegates to: Sunfish.UIAdapters.Blazor.Components.Buttons.SunfishButton

  Parameter map:
    Variant (Contained|Outlined|Flat|Fab)  → SunfishButton.FillMode (Solid|Outline|Flat|Solid)
    Type (Button|Submit|Reset)              → SunfishButton.ButtonType; Reset throws
    Disabled                                → !SunfishButton.Enabled
    Href, Target                            → rendered as anchor semantics OR Unsupported
    Size (Large|Medium|Small)               → SunfishButton.Size
    Click                                   → SunfishButton.OnClick

  Divergences:
    - No ThemeColor parameter. Visual color comes from IgbTheme; compat maps
      Variant to ButtonVariant best-effort.
    - Href-as-anchor: Sunfish treats this as a separate component; compat
      renders SunfishButton with an OnClick navigation handler (behavioral
      divergence documented).

### IgbCheckbox  ...
### IgbInput (TextBox) ...
### IgbSelect (DropDownList) ...
### IgbCombo (ComboBox) ...
### IgbDatePicker ...
### IgbGrid (commercial) ...
### IgbDialog (Window) ...
### IgbTooltip ...
### IgbToast (Notification) ...
### IgbIcon ...

## Ignite-UI-Specific Section: Web Components Interop

Ignite UI Blazor is a wrapper library over a Lit-based Web Components library
(igniteui-webcomponents, v6.x). The Igb* Blazor classes emit custom element
tags (<igc-button>, etc.) and delegate rendering to a shadow-DOM-encapsulated
WC.

**compat-infragistics does NOT preserve this architecture.** Wrappers delegate
to plain Sunfish Blazor components; no <igc-*> tags are emitted. Migrating
consumers therefore lose:

1. Access to IgbIcon.RegisterIcon / RegisterIconFromText global registry.
   (compat shims no-op or redirect to a Sunfish icon registry — Stage 02.)
2. CSS customization via --ig-* custom properties.
   (compat uses Sunfish's design-token system; no automatic carry-over.)
3. JS-side template hooks (IgbColumn.BodyTemplateScript).
   (compat throws NotSupported with migration hint → use RenderFragment.)
4. Any direct calls to IgbGrid JS methods (e.g. `await grid.FilterAsync(...)`).
   (Stage 02 decision: shim those that have Sunfish analogs; throw on the rest.)

See `docs/compat-infragistics-wc-divergence.md` (to be written at Stage 02) for
the full list.

## Unsupported / Throws

  IgbButton.Reset button type
  IgbColumn.BodyTemplateScript
  IgbColumn.InlineEditorTemplateScript
  IgbInput.StepUpAsync / StepDownAsync (deferred)
  IgbDatePicker.ShowAsync / HideAsync (deferred unless Sunfish ship analog)

## EventArgs Types

Covered by Decision-4 gap closure (shared shims for GridRowClickEventArgs etc.).
compat-infragistics reuses the generic pattern established by compat-telerik.
```

---

## 3. Licensing + POLICY Compatibility

### 3.1 `igniteui-blazor` (the Blazor wrapper library)

- **License:** **MIT** — confirmed at `github.com/IgniteUI/igniteui-blazor/blob/master/LICENSE` (copyright INFRAGISTICS 2025).
- **Implication:** Declaring types with identical names (`IgbButton`, `IgbColumn`) in a different namespace (`Sunfish.Compat.Infragistics`) is unambiguously permitted. MIT allows "use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software" — a compat shim is a vanilla permitted use.
- **Attribution requirement:** compat-infragistics source files SHOULD include a comment crediting Infragistics as the upstream API shape reference, pointing to the MIT LICENSE text (matches compat-telerik's pattern of crediting Progress Software).

### 3.2 `igniteui-webcomponents` (the underlying WC library)

- **License:** **Dual — MIT + commercial.**
  - **MIT:** 35+ standard components (Button, Input, Checkbox, Select, Combo, Dialog, Toast, Snackbar, Tooltip, Icon, DatePicker, etc.).
  - **Commercial:** Grids (Data, Tree, Hierarchical, Pivot) and Dock Manager.
- **Implication for compat-infragistics:** For the 11 MIT-covered components, compat-infragistics carries no license risk. For `IgbGrid`, **the public API surface (parameter names, method signatures) is not itself commercial** — the commercial license covers the *runtime implementation* of the WC. A compat shim that re-declares `IgbGrid` public parameter names and delegates to `SunfishGrid` does not touch the commercial WC code. **Safe to implement, but Stage 02 should double-check by (a) reading Infragistics's Ultimate license full text and (b) BDFL sign-off explicitly for the Grid wrapper.**

### 3.3 compat-telerik POLICY.md extension to compat-infragistics

The four hard invariants from `packages/compat-telerik/POLICY.md` extend cleanly:

| # | Telerik invariant | Infragistics adaptation | Status |
|---|---|---|---|
| 1 | No Telerik NuGet dependency | **No `IgniteUI.Blazor` NuGet dependency.** compat-infragistics must not `<PackageReference>` `IgniteUI.Blazor` — consumers must not be forced to carry a commercial Ignite UI license. | clean |
| 2 | Wrappers in root namespace `Sunfish.Compat.Telerik` | Wrappers in root namespace `Sunfish.Compat.Infragistics`. Mirrors Igb's flat `IgniteUI.Blazor.Controls.*` shape. | clean |
| 3 | Unsupported params throw `NotSupportedException` via `UnsupportedParam.Throw` | Same helper, shared via `Sunfish.Compat.Shared`. | clean — reuse as-is |
| 4 | Divergences documented in mapping doc | `docs/compat-infragistics-mapping.md` + `docs/compat-infragistics-wc-divergence.md` (WC-specific addendum). | new WC divergence doc needed at Stage 02 |

**No new POLICY invariants required for Infragistics.** The spike outcome (§4) eliminates the "WC-specific shim pattern required" contingency that would have motivated a policy amendment.

---

## 4. Decision-2 Spike Status — Binary Outcome

**Spike question:** Does the compat-telerik "wrapper delegates to canonical Sunfish" pattern survive when the upstream library wraps Web Components instead of native Blazor components?

### ✅ **YES — standard delegation pattern works.**

**Rationale (3 bullets; full treatment in [`infragistics-wc-architecture-spike-2026-04-22.md`](infragistics-wc-architecture-spike-2026-04-22.md)):**

1. **The compat wrapper renders OUTSIDE the web component.** The Ignite UI WC boundary is between `IgbButton`'s C# code and the `<igc-button>` it emits. A compat shim replaces `IgbButton`'s C# implementation entirely — the compat `IgbButton.razor` renders `<SunfishButton>`, never emits `<igc-button>`. Shadow DOM, Lit, and WC JS interop are downstream of a boundary the compat shim has already cut.
2. **Public API surface is materially similar to Telerik's.** `IgbButton.Variant`, `IgbInput.Value`, `IgbCheckbox.Checked` are plain C# properties. Parameter-mapping via switch expressions + `LogAndFallback` (the compat-telerik pattern) applies unchanged. `UnsupportedParam.Throw` for unmappable values applies unchanged.
3. **Only minor edge cases require vendor-specific shim logic.** Icon registration (`IgbIcon.RegisterIconFromText`) and Grid column templates (`IgbColumn.BodyTemplateScript`) are interop surfaces without Blazor-semantic analogs — but these are divergence-documented exceptions, not reasons to rethink the shim architecture.

**Implication for Stage 02 Architecture:** compat-infragistics proceeds with the standard compat-telerik scaffolding template. No alternate shim pattern is required. Stage 02 should focus on:
- Per-component parameter-mapping tables (11 components × ~10 params = ~110 mapping decisions).
- Grid JS-template divergence handling (NotSupported + migration hint).
- Icon registry handling (Stage 02 decision: redirect to Sunfish registry, or no-op + log).
- Form descoped — document rationale in Stage 02 output.

---

## 5. Open Questions for Stage 02 Architecture

1. **Icon registration:** Does Sunfish ship an icon registry (`SunfishIcon.RegisterFromText`)? If yes, compat shim redirects `IgbIcon.RegisterIconFromText` to it. If no, compat no-ops + logs + documents. **Stage 01 Discovery did not confirm; defer to Stage 02.**
2. **IgbColumn → SunfishGrid columns:** `SunfishGrid`'s column-declaration shape (child `SunfishGridColumn<T>` components vs. `Columns` collection parameter) determines the compat child-component pattern. Shared with Decision-4 gap-closure for `TelerikGridColumn`.
3. **11 vs 12 component count:** Form has no Igb equivalent. Options: (a) compat-infragistics ships 11, (b) add `IgbSnackbar` as second notification to reach 12, (c) add `IgbIconButton`. Recommendation: (a), documented clearly.
4. **Grid commercial-license sign-off:** Although the API-declaration approach is not implicated by the commercial license (compat declares types, doesn't ship runtime), BDFL should explicitly bless the Grid wrapper before Stage 06 Build.
5. **Size parameter mechanism:** Ignite UI controls size via `--ig-size` CSS custom property rather than a direct parameter on most components. Compat shim either (a) accepts `Size` as a compat-only parameter and routes to `Sunfish ButtonSize`, or (b) declares Size unsupported. Recommendation: (a).

---

## Cross-References

- [`icm/00_intake/output/compat-expansion-intake.md`](../../00_intake/output/compat-expansion-intake.md) — §5 Decisions 1+2
- [`infragistics-wc-architecture-spike-2026-04-22.md`](infragistics-wc-architecture-spike-2026-04-22.md) — full WC spike
- [`packages/compat-telerik/POLICY.md`](../../../packages/compat-telerik/POLICY.md) — pattern-reference policy
- [`docs/compat-telerik-mapping.md`](../../../docs/compat-telerik-mapping.md) — mapping-doc format reference
- Source: [`github.com/IgniteUI/igniteui-blazor`](https://github.com/IgniteUI/igniteui-blazor) (MIT)
- Source: [`github.com/IgniteUI/igniteui-webcomponents`](https://github.com/IgniteUI/igniteui-webcomponents) (MIT + commercial grids)
