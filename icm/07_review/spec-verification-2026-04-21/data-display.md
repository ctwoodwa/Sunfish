# Data Display family spec audit — 2026-04-21

Tier 1 spec-verification (ADR 0022) for every entry in
`apps/docs/component-specs/component-mapping.json` with `category == "data-display"` and
`status in ["implemented", "partial"]`.

Scope (in-mapping order): `avatar`, `badge`, `grid`, `listview`, `popover`, `tooltip`,
`treelist`. 7 components — 6 `implemented`, 1 `partial` (treelist).

Out-of-scope per mapping filter: `aiprompt`, `pivotgrid`, `spreadsheet` (all `planned`).
Card is `category=layout` per mapping, so it is audited in the Layout family pass, not
here.

---

## Summary

| Component          | Spec dir   | Mapping status | Verdict              | Priority gaps |
|--------------------|------------|----------------|----------------------|---------------|
| SunfishAvatar      | avatar     | implemented    | downgrade-to-partial | 4 |
| SunfishBadge       | badge      | implemented    | downgrade-to-partial | 6 |
| SunfishDataGrid    | grid       | implemented    | needs-work           | 6 |
| SunfishList/ListView | listview | implemented    | needs-work           | 7 |
| SunfishPopover     | popover    | implemented    | needs-work           | 4 (plus 3 missing demos) |
| SunfishTooltip     | tooltip    | implemented    | needs-work           | 5 (plus cross-category placement) |
| SunfishTreeView    | treelist   | partial        | downgrade-to-partial *(stays partial, deep gaps + naming bug)* | 8 |

Cross-family call-outs:
- **Tooltip demos live under `Pages/Components/Feedback/Tooltip/`** while the mapping
  classifies `tooltip` as `data-display`. ADR 0022 requires demos to live under the
  mapped category; either move the demo folder or update the mapping.
- **`treelist` maps to `SunfishTreeView`** in `component-mapping.json`, but
  `SunfishTreeView` is the navigation `treeview` component. The actual TreeList impl is
  `SunfishTreeList` (distinct type). This is a mapping bug and it masks the fact that
  TreeList is closer to `implemented` than `partial`.
- **`listview` maps to `SunfishList`** in mapping, but the relevant impl is
  `SunfishListView<TItem>` (adjacent to `SunfishList`). Another mapping smell.
- **Popover has only one kitchen-sink demo and it is a stub** — "demo is forthcoming"
  placeholder. The missing Animation / Position-Collision / Accessibility demos are the
  single biggest kitchen-sink gap in the data-display family.

---

## Component: SunfishAvatar (spec: `avatar`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\SunfishAvatar.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\DataDisplay\Avatar\Overview\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\DataDisplay\Avatar\Appearance\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\DataDisplay\Avatar\Accessibility\Demo.razor`
- **Spec files reviewed**: `overview.md`, `appearance.md`, `types.md` (no accessibility
  spec page exists in `apps/docs/component-specs/avatar/` — kitchen-sink adds one as a
  bonus).
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Type` parameter + `AvatarType` enum (overview.md + types.md) | missing | Spec: `Type` is `AvatarType { Image, Text, Icon }`. Impl infers type from which content params are set (`Src`/`Initials`/`ChildContent`) — no discriminator. Spec-style `Type="@AvatarType.Icon"` does not compile. | Add `AvatarType` enum and explicit `[Parameter] AvatarType Type`; keep auto-detection as fallback. |
  | `FillMode` parameter (appearance.md) | missing | Spec: `FillMode` (Solid / Outline). Impl has no equivalent. | Add `FillMode` enum; render Outline variant styling. |
  | `ThemeColor` parameter (appearance.md) | missing | Spec: 11-member `ThemeColor` (`Base`/`Primary`/`Secondary`/`Tertiary`/`Info`/`Success`/`Warning`/`Error`/`Dark`/`Light`/`Inverse`). Impl has no colour dial at all; background comes from `Initials` hash. | Add `ThemeColor` parameter + theme swatch. |
  | `Rounded` parameter type (appearance.md) | bug | Spec: `Rounded` is an enum (Small / Medium / Large / Full). Impl: `Rounded` is `bool`. Spec-style `Rounded="Rounded.Small"` does not compile; `Rounded.None` does not exist in spec but would effectively be `false` today. | Convert `Rounded` to an enum; keep `bool` as deprecated alias. |
  | `Size` enum range (appearance.md) | incomplete | Spec lists Small / Medium / Large (string constants from `ThemeConstants.Avatar.Size`). Impl `AvatarSize` extends this with `ExtraSmall` and `ExtraLarge`. Flag as Sunfish extension. | Document the two extras as Sunfish-specific. |
  | `Alt` vs decorative `Alt=""` pattern (accessibility — no spec) | covered | Accessibility demo in kitchen-sink exercises the empty-Alt pattern explicitly. | — |
  | `Bordered` parameter | covered | Impl exposes it; demos exercise it. | — |
  | `ChildContent` slot | covered | Works and is documented in the Appearance demo (custom emoji / icons). | — |

- **Verdict**: `downgrade-to-partial` — 3 missing + 1 bug + 1 incomplete. Core spec
  dial `Type`/`AvatarType` is absent and `Rounded` has the wrong type shape, so the
  spec's primary samples will not compile against this impl.

---

## Component: SunfishBadge (spec: `badge`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\SunfishBadge.razor`
- **Demos**:
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\DataDisplay\Badge\Overview\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\DataDisplay\Badge\Appearance\Demo.razor`
  - `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\DataDisplay\Badge\Accessibility\Demo.razor`
- **Spec files reviewed**: `overview.md`, `appearance.md`, `position.md` (no dedicated
  accessibility spec page exists; kitchen-sink supplies one).
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `ThemeColor` parameter (appearance.md) | bug | Spec: 11-member `ThemeColor` enum (`Primary`/`Secondary`/`Tertiary`/`Info`/`Success`/`Warning`/`Error`/`Dark`/`Light`/`Inverse` + `Base`). Impl uses its own `BadgeVariant` enum with 7 members (`Default`/`Primary`/`Secondary`/`Danger`/`Warning`/`Info`/`Success`). Spec-style `ThemeColor="@ThemeColor.Error"` does not compile; impl uses `Danger` not `Error`. | Rename `Variant` → `ThemeColor`; widen enum to spec set. |
  | `FillMode` parameter (appearance.md) | missing | Spec: `FillMode` (Solid / Flat / Outline). Impl has no `FillMode`. | Add `FillMode` enum; render three styles. |
  | `Rounded` parameter (appearance.md) | missing | Spec: `Rounded` enum (Small / Medium / Large / Full). Impl has none — pill shape is hard-coded. | Add parameter; plumb to CSS. |
  | `Size` parameter (appearance.md) | missing | Spec: `Size` (Small / Medium / Large). Impl has none. | Add `BadgeSize` enum. |
  | `Position` / `HorizontalAlign` / `VerticalAlign` types (position.md) | bug | Spec typed enums: `BadgePosition { Edge, Inline, Inside, Outside }`, `BadgeAlign { Start, End }` and `{ Top, Bottom }`. Impl: plain `string` parameters. Spec-style `Position="@BadgePosition.Edge"` does not compile; demo uses lowercase strings. | Port to typed enums; keep string accepters as obsolete aliases for one release. |
  | `ShowCutoutBorder` parameter | covered | Impl exposes it; Accessibility demo uses it on the status-dot example. | — |
  | Standalone badge rendering as `<span>` / no implicit ARIA role | covered | Accessibility demo documents the `aria-label` wrapper pattern. | — |
  | `role="status"` pattern (accessibility demo) | covered | Demo exercises it directly. | — |

- **Verdict**: `downgrade-to-partial` — 2 bug + 3 missing items, all in the appearance
  surface. `FillMode`, `Rounded`, `Size`, and `ThemeColor` are the four dials the spec
  calls out as primary; only one (the shape-implicit-from-content behavior) is even
  partially honoured today.

---

## Component: SunfishDataGrid (spec: `grid`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\DataGrid\SunfishDataGrid.razor` +
  `SunfishDataGrid.razor.cs` and partial files (`.Data.cs`, `.Editing.cs`, `.Export.cs`,
  `.Interop.cs`, `.Pdf.cs`, `.Rendering.cs`). Column support:
  `SunfishGridColumn.razor`, `SunfishGridColumnMenu.razor`,
  `SunfishGridCommandButton.razor`, `SunfishGridToolbar.razor`, `GridState.cs`,
  `GridEventArgs.cs`, `GridCommandTypes.cs`, `GridCellReference.cs`,
  `GridColumnFrozenPosition.cs`.
- **Demos**:
  - `Pages\Components\DataDisplay\DataGrid\Overview\Demo.razor`
  - `Pages\Components\DataDisplay\DataGrid\Appearance\Demo.razor`
  - `Pages\Components\DataDisplay\DataGrid\Events\Demo.razor`
  - `Pages\Components\DataDisplay\DataGrid\Accessibility\Demo.razor`
  - *(Also: legacy `Pages\Components\DataDisplay\Grid\Overview\Demo.razor` — likely a
    pre-rename artifact.)*
- **Spec files reviewed**: `overview.md`, `data-binding.md`, `events.md`, `paging.md`,
  `sorting.md`, `state.md`, `accessibility/overview.md` (partial — WAI-ARIA detail not
  deep-dived), plus table-of-contents skim for `columns/*`, `editing/*`, `filter/*`,
  `grouping/*`, `selection/*`, `templates/*`, `export/*`, and `hierarchy.md`,
  `keyboard-navigation.md`, `toolbar.md`, `virtual-scrolling.md`, `refresh-data.md`,
  `sizing.md`, `loading-animation.md`, `manual-operations.md`, `row-drag-drop.md`,
  `highlighting.md`, `smart-ai-features/*`. The grid spec is 60+ files; a full per-file
  sweep is deferred to Tier 2.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `<GridSettings>` / `<GridPagerSettings>` nested composition (paging.md + state.md) | missing | Spec exposes declarative sub-tags that group pager options (`ButtonCount`, `InputType`, page-size dropdown, PagerPosition) under a child. Impl flattens them onto the parent (`PagerButtonCount`, `PageSizes`, etc.). | Add `GridSettings` + `GridPagerSettings` as child components that cascade into parent. |
  | `<GridToolBarTemplate>` naming (toolbar.md) | bug | Spec: the template child tag is `GridToolBarTemplate` (title-cased `ToolBar`). Impl: `ToolbarTemplate` parameter (no child tag). Spec-style usage will not compile. | Add a `GridToolBarTemplate` child wrapper that maps to the existing `ToolbarTemplate`. |
  | `AdaptiveMode` (mobile / small-viewport layout) | missing | Spec lists `AdaptiveMode { None, Auto }` on the parent. Impl has no equivalent. | Add parameter and small-viewport renderer or document as deferred. |
  | `CustomKeyboardShortcuts` (keyboard-navigation.md) | missing | Spec documents an escape hatch for callers to register custom shortcuts. Impl fires built-in navigation only. | Add a dictionary-style parameter and wire it into the existing key handler. |
  | `GridState.TableWidth` (state.md) | missing | Spec's persisted state includes `TableWidth`. Impl `GridState` omits it (retains `ColumnStates[*].Width` only). | Add `TableWidth` (nullable string) and round-trip. |
  | `GridState.Skip` (state.md) | missing | Spec exposes `Skip` for virtual-scroll restoration. Impl persists `CurrentPage`/`PageSize` only. | Add `Skip` when virtualizing. |
  | `GridState.SelectedItems` typed (state.md) | bug | Spec: typed `SelectedItems` collection on state. Impl: `SelectedKeys` (`HashSet<object>`) with no typed view. | Add a typed accessor next to the existing keyed store. |
  | `<GridCheckboxColumn>` child composition | covered-but-flat | Impl exposes `ShowCheckboxColumn` bool on parent; spec's declarative child tag is not offered. | Add `SunfishGridCheckboxColumn` component (thin wrapper over existing support). |
  | `OnRead` server-binding event + `GridReadEventArgs` (data-binding.md) | covered | Impl uses `DataGridReadEventArgs` with the documented fields (`Request`, `Response`). | — |
  | WAI-ARIA: `role="grid"` + `aria-rowcount` / `aria-colcount` | covered *(not re-verified)* | Impl renders a `<table role="grid">` with headers; full WAI-ARIA conformance against the full spec page deferred to Tier 2. | Spot-verify in Tier 2. |

- **Verdict**: `needs-work` — 2 bug + 4 missing (of which at least 3 are non-core
  polish). The impl is broad and production-class; the gaps are primarily shape /
  declarative-composition polish, not core feature absence. Verdict stays above
  `downgrade-to-partial` because the primary capabilities (paging, sorting, filtering,
  grouping, selection, editing, virtualization, export, server-binding, templates, state,
  row DnD) are all present.

---

## Component: SunfishListView (spec: `listview`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\List\SunfishListView.razor` +
  sibling `SunfishList.razor`, `SunfishListItem.razor`, `ListEventArgs.cs`.
- **Demos**:
  - `Pages\Components\DataDisplay\ListView\Overview\Demo.razor`
  - `Pages\Components\DataDisplay\ListView\Appearance\Demo.razor`
  - `Pages\Components\DataDisplay\ListView\Events\Demo.razor`
  - `Pages\Components\DataDisplay\ListView\Accessibility\Demo.razor`
- **Spec files reviewed**: `overview.md`, `editing.md`, `events.md`,
  `manual-operations.md`, `paging.md`, `refresh-data.md`, `templates.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `<ListViewCommandButton>` child component (editing.md) | missing | Core shape of the editing API. Spec has `ListViewCommandButton Command="Add|Edit|Save|Delete|Cancel"` tags for toolbar + row-edit commands. Impl has no such component. | Introduce `SunfishListViewCommandButton` that wires to parent via `CascadingValue`. |
  | `OnEdit` / `OnCancel` events (events.md + editing.md) | missing | Spec fires `OnEdit` when a row enters edit mode, `OnCancel` when discarded. Impl has `OnCreate`/`OnUpdate`/`OnDelete` only. | Add both `[EventCallback<ListViewCommandEventArgs<TItem>>]` parameters; fire from edit lifecycle. |
  | `OnModelInit` (overview.md) | missing | Spec: callback to mint new items (lets caller set defaults before insertion). Impl has no hook. | Add `Func<TItem>? OnModelInit` parameter; use in default-construction path. |
  | `PageSizeChanged` event (paging.md) | missing | Spec: `[EventCallback<int>] PageSizeChanged`. Impl fires `PageChanged` only. | Add `PageSizeChanged`; fire from pager when size dropdown changes. |
  | `EnableLoaderContainer` (refresh-data.md) | missing | Spec: boolean enabling an overlay during `OnRead`. Impl has no loader hook; consumers must build their own. | Add parameter + render the existing `SunfishSpinner` overlay. |
  | `<Template>` vs `ItemTemplate` (templates.md) | bug | Spec's primary template child is `<Template>`. Impl uses `<ItemTemplate>`. Spec-style `<Template>@context.Name</Template>` does not compile. | Add `Template` child `RenderFragment<TItem>`; keep `ItemTemplate` as deprecated alias. |
  | `ListViewReadEventArgs` / `ListViewCommandEventArgs` (events.md) | bug | Spec uses dedicated event arg types. Impl uses `DataRequest` / untyped `TItem` on callbacks. Spec-style `OnRead="@(args => ...)"` where `args.Request` is a `DataSourceRequest` won't match impl exactly. | Introduce `ListViewReadEventArgs<TItem>`, `ListViewCommandEventArgs<TItem>`; keep old signature as `[Obsolete]` wrapper. |
  | `<ListViewSettings>` / `<ListViewPagerSettings>` child composition | missing | Same pattern as Grid — spec uses nested settings for pager options. Impl flattens everything onto the parent. | Add child components (share design w/ `GridPagerSettings`). |
  | `Rebind()` method (manual-operations.md + refresh-data.md) | covered | Impl exposes public `RebindAsync`. | — |
  | Selection: `SelectedItemsChanged` two-way bind (events.md) | covered | Impl fires `SelectedItemsChanged` on selection changes. | — |
  | Paging basics + `PageChanged` event | covered | Impl implements `Pageable`, `PageSize`, `Page`, `PageChanged`; demo exercises it. | — |
  | WAI-ARIA: `role="list"` + per-item `role="listitem"` (accessibility) | covered (surface) | Impl emits the documented roles; full `aria-selected` / `listbox` mode verification deferred. | — |

- **Verdict**: `needs-work` — 2 bug + 5 missing items. The command-button composition
  and the three edit-lifecycle events (`OnEdit`, `OnCancel`, `OnModelInit`) are the
  most spec-user-facing gaps. The core data rendering + selection + paging surface is
  sound.

---

## Component: SunfishPopover (spec: `popover`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\SunfishPopover.razor`
- **Demos**:
  - `Pages\Components\DataDisplay\Popover\Overview\Demo.razor` — **stub** ("demo is
    forthcoming" placeholder).
  - *(No Animation / Position-Collision / Accessibility sibling demos exist.)*
- **Spec files reviewed**: `overview.md`, `animation.md`, `position-collision.md`,
  `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | `Position` parameter type (position-collision.md) | bug | Spec: `PopoverPosition { Top, Bottom, Left, Right }`. Impl: reuses `TooltipPosition`. The two types are semantically distinct and must be separable. | Introduce `PopoverPosition` enum; keep `TooltipPosition` for the tooltip. |
  | `Offset` type (overview.md) | bug | Spec: `Offset` is `double`. Impl: `Offset` is `int`. Sub-pixel callouts cannot be expressed. | Widen to `double`. |
  | `AnimationType` enum breadth (animation.md) | incomplete | Spec enumerates 14 values (`None`, `Fade`, `PushUp`/`Down`/`Left`/`Right`, `RevealVertical`, `SlideUp`/`In`/`Down`/`Right`/`Left`, `ZoomIn`/`Out`). Impl uses its own `PopoverAnimationType` — enum matches by value set on spot-check but naming is divergent from the shared `AnimationType` the spec uses elsewhere. | Either rename to shared `AnimationType` or document the fork with an explicit alias. |
  | `ActionsLayout` parameter (overview.md) | missing | Spec: `ActionsLayout { Stretch, Start, Center, End }` on the `<PopoverActions>` area. Impl has no layout control for actions. | Add parameter; style the actions row. |
  | `Show` / `Hide` / `Refresh` reference methods (overview.md §Methods) | covered | Impl exposes `ShowAsync` / `HideAsync`; `Refresh` is not present but the component re-positions automatically on show. | Add `RefreshAsync` for parity. |
  | `<PopoverContent>` / `<PopoverActions>` / `<PopoverHeader>` child composition (overview.md) | covered | Impl exposes equivalent named `RenderFragment` slots. | — |
  | WAI-ARIA: `role="dialog"` + `aria-labelledby` + `aria-describedby` | covered | Impl emits all three. | — |
  | Missing kitchen-sink demos: Animation, Position-Collision, Accessibility (ADR 0022) | missing | Route exists only as a "forthcoming" Overview stub. | Author Overview (real), Animation, Position, Accessibility demos — four pages total. |

- **Verdict**: `needs-work` — 2 bug + 2 missing (component) + 3 missing demos + 1
  incomplete. Component gaps alone put it at `needs-work`; demo coverage is the real
  pain point (the only Popover page users can visit is a placeholder).

---

## Component: SunfishTooltip (spec: `tooltip`)

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\SunfishTooltip.razor`
- **Demos**:
  - `Pages\Components\Feedback\Tooltip\Overview\Demo.razor`
  - `Pages\Components\Feedback\Tooltip\Appearance\Demo.razor`
  - `Pages\Components\Feedback\Tooltip\Accessibility\Demo.razor`
  — **cross-category placement**: demos live under `Feedback/`, not
  `DataDisplay/`, while the mapping says `data-display`.
- **Spec files reviewed**: `overview.md`, `position.md`, `show-event.md`,
  `template.md`, `accessibility/wai-aria-support.md`.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Demo category placement | bug | Tooltip maps to `data-display`; demos live under `Pages\Components\Feedback\Tooltip\`. ADR 0022 routes/demo-catalog treat category + spec-dir as canonical. | Either move demos to `Pages\Components\DataDisplay\Tooltip\` and update routes, or re-map tooltip to `category=feedback` (recommended: move). |
  | `ShowOn` enum type (show-event.md) | bug | Spec: `TooltipShowEvent { Hover, Click }`. Impl: `TooltipShowOn { Hover, Focus, Click }` (adds `Focus`; name differs). Spec-style `ShowOn="@TooltipShowEvent.Hover"` does not compile. | Rename enum to `TooltipShowEvent`, keep `TooltipShowOn` as alias; `Focus` is a Sunfish extension to document (or drop). |
  | `ShowDelay` parameter (overview.md) | missing | Spec: `ShowDelay` (ms) before tooltip appears. Impl has no delay control. | Add parameter; gate the show-call behind a timer. |
  | `HideDelay` parameter (overview.md) | missing | Spec: `HideDelay` (ms) before tooltip disappears. Impl has no delay control. | Add parameter; gate the hide-call behind a timer. |
  | `Id` parameter (overview.md) | missing | Spec exposes explicit `Id` for ARIA wiring (`aria-describedby` target). Impl has no explicit `Id` parameter. | Add `Id` `[Parameter]`; emit on the tooltip's root element. |
  | `<Template>` context (template.md) | bug | Spec: template context exposes `{ DataAttributes: IReadOnlyDictionary<string,string>, Title: string }` per-anchor (lets one tooltip render different content per target based on `data-*` attributes). Impl: `TooltipTemplate` is a `RenderFragment<TooltipTemplateContext>` but the context's schema doesn't match the spec (e.g., the per-target `DataAttributes` dict is not surfaced). | Enrich the context type to expose per-anchor data; or document as intentionally minimal. |
  | `TargetSelector` parameter | covered | Impl exposes it. | — |
  | `Position` enum (position.md) | covered | Impl uses `TooltipPosition { Top, Bottom, Left, Right }`; matches spec. | — |
  | WAI-ARIA: `role="tooltip"` + `aria-describedby` linkage | covered | Impl emits them when an anchor/target is resolved. | — |

- **Verdict**: `needs-work` — 3 bug + 3 missing items plus the cross-category demo
  placement. The `ShowDelay`/`HideDelay` absence is the most user-visible gap (most
  production tooltip patterns rely on delay).

---

## Component: SunfishTreeList (spec: `treelist`, mapping status = `partial`)

> **Mapping bug**: `treelist` is mapped to `SunfishTreeView`, but the real impl is
> `SunfishTreeList<TItem>` (separate file, separate type). This audit uses the actual
> impl. The mapping should be corrected to `SunfishTreeList`.

- **Impl**: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\DataGrid\SunfishTreeList.razor` +
  `SunfishTreeList.razor.cs`, `SunfishTreeListColumn.razor`, `TreeListToolbar.razor`,
  `TreeListToolbarButton.razor`, `ITreeListEditController.cs`.
- **Demos**:
  - `Pages\Components\DataDisplay\TreeList\Overview\Demo.razor` *(only demo — no
    Appearance / Events / Accessibility siblings)*.
- **Spec files reviewed**: `overview.md`, `accessibility/overview.md`, table-of-contents
  skim for `columns/*` (16 files), `data-binding/*`, `editing/*`, `filter/*`,
  `selection/*`, `templates/*`, plus `aggregates.md`, `events.md`, `paging.md`,
  `refresh-data.md`, `row-drag-drop.md`, `sorting.md`, `state.md`, `toolbar.md`,
  `virtual-scrolling.md`. Full per-file sweep deferred to Tier 2.
- **Gaps**:

  | Spec feature | Severity | Finding | Fix hint |
  |---|---|---|---|
  | Mapping bug: `component-mapping.json.treelist.sunfish` | bug | Mapping points at `SunfishTreeView` (the navigation TreeView). Correct target is `SunfishTreeList`. | Update `component-mapping.json` → `"treelist": { "sunfish": "SunfishTreeList", ... }`. |
  | `<TreeListColumns>` wrapper element (overview.md) | missing | Spec puts `<TreeListColumn>` children *inside* a `<TreeListColumns>` wrapper. Impl uses bare `<SunfishTreeListColumn>` children directly. | Introduce a wrapper component; retain current form as deprecated. |
  | `<TreeListCommandColumn>` / `<TreeListCommandButton>` (accessibility/overview.md sample) | missing | Spec uses dedicated command-column/command-button components for edit operations. Impl has `TreeListToolbarButton` only (toolbar-scoped), with ad-hoc edit buttons rendered inside `RenderRows` in C#. | Extract command-column/button components for declarative composition. |
  | `<TreeListToolBarTemplate>` (overview.md + accessibility/overview.md sample) | missing | Spec uses this named slot for the toolbar template. Impl: `TreeListToolbar.razor` exists but is rendered internally; there is no caller-facing `TreeListToolBarTemplate` slot. | Add the template slot. |
  | `<TreeListSettings>` / `<TreeListColumnMenuSettings>` (accessibility/overview.md sample) | missing | Spec exposes nested settings children (`Lockable`, `Reorderable`, `FilterMode`). Impl has none. | Add components; plumb options. |
  | `OnEdit`, `OnCancel`, `OnRowContextMenu` events (accessibility/overview.md sample + events.md) | missing | Spec's example wires all three. Impl has `OnCreate`/`OnUpdate`/`OnDelete`/`OnRowClick`/`OnSelectionChanged`/`OnExpand`/`OnCollapse`/`OnColumnReordered`/`OnSortChanged`/`OnRowDropped` — no `OnEdit`/`OnCancel`/`OnRowContextMenu`. | Add the three missing events. |
  | `ShowColumnMenu` parameter (accessibility/overview.md sample) | missing | Spec exposes it on the parent; impl does not. | Add parameter + menu UX. |
  | `GetState()` / `SetStateAsync()` (accessibility/overview.md sample — uses `TreeListRef.GetState()`) | missing | Spec documents a typed state object (`InsertedItem`, `EditItem`, `OriginalEditItem`, etc.). Impl exposes `RebindAsync` only — no public state. | Add `TreeListState` mirroring `GridState`; add getter/setter. |
  | `AutoFitAllColumns()` (overview.md §Reference and Methods) | missing | Spec exposes `AutoFitAllColumns()`. Impl has none. | Add method; implement via column-width runtime measurements. |
  | Sibling kitchen-sink demos (ADR 0022) | missing | Only `Overview/Demo.razor` exists. Missing `Appearance`, `Events`, `Accessibility`. | Author the three missing demos. |
  | `SelectionMode`, `SelectedItems`, paging, filtering, sorting, resize/reorder, row-DnD, inline editing, virtualization | covered | Impl is broad; all present. | — |
  | WAI-ARIA: `role="row"` / `aria-level` / `aria-expanded` / `aria-selected` | covered | Impl emits all of them in `RenderRows`. | — |

- **Verdict**: `downgrade-to-partial` — the impl already meets `partial` scope by
  virtue of broad render + selection + editing support, but the naming bug, the
  missing declarative composition surface (command column/button, toolbar template,
  settings children), the three missing events, and the missing state API are deep
  enough that the `partial` mapping should stand until Tier 2 closes them.

---

## Next actions (top priority fixes for the Data Display family)

Ordered by blast-radius, not by component:

1. **Fix mapping records**
   - `treelist.sunfish`: `SunfishTreeView` → `SunfishTreeList`.
   - `listview.sunfish`: `SunfishList` → `SunfishListView` (or document the
     List/ListView pairing explicitly; today the demo route uses ListView, the mapping
     says List).
   - Move Tooltip demos to `Pages\Components\DataDisplay\Tooltip\` (or re-map Tooltip
     to `category=feedback`).

2. **Adopt spec's declarative child-tag composition across the family**
   - Avatar: no child composition needed beyond `ChildContent`.
   - Badge: no new children, but introduce typed enums (see 3).
   - Grid: `GridSettings`, `GridPagerSettings`, `GridToolBarTemplate`,
     `GridCheckboxColumn`.
   - ListView: `ListViewCommandButton`, `<Template>`, `ListViewSettings`,
     `ListViewPagerSettings`.
   - TreeList: `TreeListColumns`, `TreeListCommandColumn`, `TreeListCommandButton`,
     `TreeListToolBarTemplate`, `TreeListSettings`, `TreeListColumnMenuSettings`.

3. **Align typed enums with spec names and widen appearance scales**
   - Add `AvatarType { Image, Text, Icon }`; add `FillMode`, `ThemeColor`, typed
     `Rounded` to `SunfishAvatar`.
   - Add `FillMode`, `Rounded`, `Size`, `ThemeColor`, typed `BadgePosition`,
     typed `BadgeAlign` to `SunfishBadge`; retire `BadgeVariant` name.
   - Add `PopoverPosition` (distinct from `TooltipPosition`); widen `Offset` to
     `double`.
   - Rename `TooltipShowOn` → `TooltipShowEvent`; drop or document the `Focus`
     extension; align naming with spec.
   - Add `AdaptiveMode` to `SunfishDataGrid`.

4. **Close event-surface gaps**
   - ListView: `OnEdit`, `OnCancel`, `OnModelInit`, `PageSizeChanged`,
     `EnableLoaderContainer`.
   - TreeList: `OnEdit`, `OnCancel`, `OnRowContextMenu`, `GetState`, `SetStateAsync`,
     `AutoFitAllColumns()`.
   - Tooltip: `ShowDelay`, `HideDelay`, `Id`.
   - Popover: `RefreshAsync`, `ActionsLayout`.
   - Grid: extend `GridState` with `TableWidth`, `Skip`, typed `SelectedItems`.

5. **Kitchen-sink demo gaps (ADR 0022)**
   - **Popover**: replace stub Overview with a real Overview; author Animation,
     Position-Collision, Accessibility demos. (4 pages)
   - **TreeList**: author Appearance, Events, Accessibility demos. (3 pages)
   - **Tooltip**: once moved to `DataDisplay/`, keep existing three demos; add Events
     (exercises `ShowDelay`/`HideDelay`) after parameters exist.
   - Kitchen-sink placeholder text "demo is forthcoming" should be treated as a lint
     failure for published routes.

6. **Spec surface fully exhaustively audited in Tier 2**
   - Grid: 60+ files — columns/*, editing/*, filter/*, grouping/*, selection/*,
     templates/*, export/*, smart-ai-features/*, hierarchy.md, keyboard-navigation.md,
     loading-animation.md, manual-operations.md, refresh-data.md, row-drag-drop.md,
     sizing.md, toolbar.md, virtual-scrolling.md, highlighting.md, accessibility/*.
   - TreeList: 50+ files (same sub-areas as Grid).

---

_Audit prepared: 2026-04-21. Tier 1 of the Blazor-100% parity push (ADR 0022)._
