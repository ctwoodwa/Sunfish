# Migrating from DevExpress Blazor to Sunfish

**Sunfish does NOT ship a `Sunfish.Compat.DevExpress` package.** Unlike Telerik,
Syncfusion, and Infragistics — each of which has a dedicated flip-your-usings off-ramp —
DevExpress migrators follow a **manual migration** path to canonical Sunfish types.

## Why there is no compat shim

DevExpress's Universal EULA §7(a) states: *"LICENSEE shall not develop software
applications that provide an application programming interface to the SOFTWARE DEVELOPMENT
PRODUCT(S)."* The conservative reading directly targets the compat-shim pattern (type-name
parity without NuGet reference). Rather than run a legal-review spike that would still
leave asymmetric derivative-work risk, the BDFL chose to drop `compat-devexpress` from
scope on 2026-04-22. See
[`icm/00_intake/output/compat-expansion-intake.md`](../../icm/00_intake/output/compat-expansion-intake.md)
§Scope reduction and
[`docs/adrs/0026-bridge-posture.md`](../adrs/0026-bridge-posture.md) for full rationale.

## What the analyzer does

`Sunfish.Analyzers.CompatVendorUsings` still flags `using DevExpress.Blazor;` (and child
namespaces) as **SF0002 — Vendor namespace detected; Sunfish has no shim (manual
migration).** SF0002 is informational only; **no code fix is offered** because there is no
shim namespace to flip to.

## Migration path — rewrite site-by-site

Replace DevExpress components with canonical Sunfish types directly. There is no
flip-your-usings shortcut. The common mapping:

| DevExpress component | Sunfish canonical type | Notes |
|---|---|---|
| `DxButton` | `SunfishButton` | `IconCssClass` maps via the Sunfish `Icon` render fragment. |
| `DxCheckBox<T>` | `SunfishCheckbox` | `SunfishCheckbox` is `bool`-only; other `T` require manual coercion. |
| `DxTextBox` | `SunfishTextBox` | Rewrite `BindValueMode` to the `@bind-Value` / `@bind-Value:event` idiom. |
| `DxComboBox<TData,TValue>` | `SunfishComboBox<TItem,TValue>` | `TextFieldName`/`ValueFieldName` become `TextField`/`ValueField`. |
| `DxComboBox` with `AllowUserInput="false"` | `SunfishDropDownList<TItem,TValue>` | Use `DropDownList` for non-editable selection. |
| `DxDateEdit<T>` | `SunfishDatePicker` | `SunfishDatePicker` is `DateTime?`-based; coerce `DateOnly`/`DateTimeOffset` at the binding boundary. |
| `DxFormLayout` | `SunfishForm` + your own layout | Lossy — `DxFormLayout` is a grid/stack layout; `SunfishForm` is EditContext-based. `CaptionPosition` and column-span have no direct Sunfish-side equivalent. |
| `DxGrid` (non-generic) | `SunfishDataGrid<TItem>` (generic) | **Shape break.** `DxGrid` uses `IEnumerable` + `KeyFieldName` string; `SunfishDataGrid<TItem>` is generic. Introduce the type parameter at every call site. |
| `DxGridDataColumn` | `SunfishGridColumn<TItem>` | Column-level parameter rewriting. |
| `DxWindow` | `SunfishWindow` | Straightforward. |
| `DxPopup` | `SunfishWindow` with `Modal=true` | DevExpress ships both; consolidate. |
| `DxToast` + `DxToastProvider` | `SunfishSnackbarHost` + `ISnackbarService` | Register `ISnackbarService` in DI; rewrite declarative `DxToast` to imperative `snackbar.Show(...)`. |
| _(no `DxIcon` exists)_ | `SunfishIcon` | DevExpress uses `IconCssClass` strings; replace with Sunfish's icon render-fragment pattern. |

Full per-component parameter mapping skeletons are in
[`icm/01_discovery/output/compat-devexpress-surface-inventory-2026-04-22.md`](../../icm/01_discovery/output/compat-devexpress-surface-inventory-2026-04-22.md) — a retained
research artifact, not a compat-package commitment.

## Community alternatives as a stepping stone

If you need a compat-shaped layer before moving to canonical Sunfish, other OSS Blazor
kits (BlazorBootstrap, MudBlazor, Radzen's MIT-licensed tier) may be a viable intermediate
step. Sunfish does not endorse any of them for parity with DevExpress; they are simply
other open-source Blazor component libraries with broadly similar shape.

## When to stay on DevExpress

If your product depends on DevExpress-specific features (XAF integration, Reports,
Dashboard, Scheduler, RichEdit, SpreadsheetView), there is no Sunfish off-ramp for those
and none is planned. Migrate the simple form/grid/editor surfaces incrementally; keep
DevExpress for the heavyweight surfaces.
