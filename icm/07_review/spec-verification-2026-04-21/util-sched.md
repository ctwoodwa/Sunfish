# Spec Verification — Utility + Scheduling Families

Date: 2026-04-21 · ADR 0022 Tier 1 · READ-ONLY audit

Scope: Components in `component-mapping.json` with `status in ["implemented","partial"]` for categories `utility` and `scheduling`.

- Utility in scope: `popup` (partial → SunfishPopover), `rootcomponent` (partial → SunfishThemeProvider). Skipped: `animationcontainer` (n/a), `mediaquery` (n/a), `diagram` (planned), `dropzone` (planned).
- Scheduling in scope: `calendar` (partial → SunfishDatePicker). Skipped: `gantt` (planned), `scheduler` (planned).

Severity legend: `missing`, `bug`, `incomplete`, `covered`. Verdict: `verified` / `needs-work` / `downgrade-to-partial`.

---

## 1. Utility

### Summary

| Component | Mapping | Spec files | Verdict | Notes |
|---|---|---|---|---|
| popup | SunfishPopover (partial) | 3 | **needs-work** | Mapping is wrong: a standalone `SunfishPopup` component exists. Many spec parameters missing from SunfishPopup. |
| rootcomponent | SunfishThemeProvider (partial) | 2 | **needs-work** | Spec surface (RTL, IconType, Localizer, popup host, DialogFactory, RootComponentSettings) largely absent. |

### 1.1 popup

#### Gaps

| Severity | Area | Detail |
|---|---|---|
| bug | Mapping | `component-mapping.json` maps `popup → SunfishPopover`, but an actual `SunfishPopup` component exists at `Components/Overlays/Popup/SunfishPopup.razor`. Mapping should be `popup → SunfishPopup`. |
| missing | API — anchor | Spec uses `AnchorSelector` (CSS selector, e.g. `.popup-target`); impl exposes only `AnchorId` (element id). Every spec example is incompatible with the current impl. |
| missing | API — alignment | Spec defines `AnchorHorizontalAlign` (`PopupAnchorHorizontalAlign`), `AnchorVerticalAlign`, `HorizontalAlign` (`PopupHorizontalAlign`), `VerticalAlign` — none exist in impl. Impl uses a single `Placement` enum (Top/Bottom/Left/Right/Auto) instead. |
| missing | API — collision | Spec requires `HorizontalCollision` and `VerticalCollision` (`PopupCollision` enum with `Fit`/`Flip`). Impl has no collision handling at all (source comment: "Full anchor tracking deferred to Pass 4"). |
| missing | API — animation | Spec requires `AnimationType` enum (14 members: None, Fade, PushUp/Down/Left/Right, RevealVertical, SlideUp/In/Down/Right/Left, ZoomIn/Out) and `AnimationDuration` (int ms). Impl has neither. |
| missing | API — sizing | Spec examples use `Width` parameter; impl has no `Width` / `Height` parameters. |
| incomplete | API — methods | Spec examples call `PopupRef.Show()` / `PopupRef.Hide()` (sync). Impl exposes `ShowAsync()` / `HideAsync()` only — spec code samples would not compile. |
| missing | API — `OnClose` / open event | Spec implies handlers on state change (`IsOpenChanged` is in both). Impl has no `OnOpen`/`OnClose` events, but spec focuses on two-way binding so this is acceptable if documented. |
| incomplete | Accessibility | Impl sets `role=dialog` + `aria-modal` only when `FocusTrap=true`. Spec's overview.md mentions `role="listbox" or no role when FocusTrap is false`, which matches — this is covered. Focus-return-to-trigger on close is specified but not implemented (no focus management visible in source). |
| incomplete | Positioning | Impl note in `SunfishPopup.razor.cs`: uses only `position:absolute` CSS, no JS-based anchor tracking. Spec's position/collision article assumes viewport collision and fit/flip — cannot be satisfied. |
| missing | Accessibility spec file | No `accessibility/` sub-folder under `component-specs/popup/`. Could be intentional (popup is lightweight), but listed here so it can be confirmed. |
| covered | Outside click | Spec overview states "Closes on outside click (fires OnOutsideClick, then sets IsOpen = false)" — implemented via `OnOutsideClickInternal` JS handler. |
| covered | Escape key | `CloseOnEscape` + `KeyDown` handler match spec. |

#### Verdict: **needs-work** (mapping is incorrect and most parameters from the anchor/alignment/collision/animation specs are absent; "partial" is accurate).

- Spec files reviewed: `apps/docs/component-specs/popup/overview.md`, `apps/docs/component-specs/popup/animation.md`, `apps/docs/component-specs/popup/position-collision.md`.
- Impl: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Overlays\Popup\SunfishPopup.razor`, `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Overlays\Popup\SunfishPopup.razor.cs`, `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Overlays\Popup\PopupPlacement.cs`.
- Demo: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Popup\Overview.razor` — single overview page only; no animation or position-collision demos.

### 1.2 rootcomponent

#### Gaps

| Severity | Area | Detail |
|---|---|---|
| missing | API — `EnableRtl` | Spec documents `EnableRtl: bool`. Impl reads `Theme.IsRtl` from the theme object and emits `dir="rtl"`, but there is no top-level `EnableRtl` parameter on `SunfishThemeProvider`. Consumer must mutate the cascaded theme. |
| missing | API — `IconType` | Spec documents `IconType` parameter (`Svg` default). Impl has no such parameter; icon type is not configurable at the root. |
| missing | API — `Localizer` | Spec documents `Localizer: ISunfishStringLocalizer` param. Impl does not expose a Localizer parameter on ThemeProvider. |
| missing | Feature — popup host | Spec states the RootComponent "renders all Sunfish popups" to escape overflow:hidden / scroll-trap containers. Impl is a CSS-variable provider only — no popup host slot, no layered container for popups/dialogs. |
| missing | Feature — DialogFactory | Spec states RootComponent "exposes the DialogFactory for predefined dialogs". Impl has no DialogFactory cascading value; `grep` found no `DialogFactory` symbol in the package. |
| missing | Feature — `<RootComponentSettings>` | Spec documents a nested `<RootComponentSettings>` child tag (for adaptive breakpoints). Impl has no equivalent settings tag. |
| missing | Feature — Per-component interactivity support | Spec has a dedicated `per-component-interactivity-location.md` explaining a `SunfishContainer.razor` pattern. No guidance or helper in impl; a developer migrating from Telerik would be unguided. |
| incomplete | Naming mismatch | Spec calls the component `SunfishRootComponent`; impl is `SunfishThemeProvider`. Mapping file lists this mismatch deliberately, but the spec's slug / example markup uses `<SunfishRootComponent>` verbatim — docs copy/paste will not compile against the current package. |
| covered | Cascading theme | `CascadingValue Value="@Theme"` and dark-mode `data-sf-theme` attribute meet part of the spec (theming only). |
| covered | CSS-variable generation | `GenerateThemeStyles()` emits a full set of `--sf-*` tokens — not in the spec but consistent with the Sunfish framework-agnostic design principle. |

#### Verdict: **needs-work** (spec covers theming + popup host + DialogFactory + adaptive breakpoints + localizer + icon type; impl covers only theming/CSS variables. `partial` is accurate but the gap is wide).

- Spec files reviewed: `apps/docs/component-specs/rootcomponent/overview.md`, `apps/docs/component-specs/rootcomponent/per-component-interactivity-location.md`.
- Impl: `C:\Projects\Sunfish\packages\ui-adapters-blazor\SunfishThemeProvider.razor`.
- Demo: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\RootComponent\Overview.razor` — 3 sections (Usage, Custom Theme, Properties table). Demo documents what exists (theming only) and does not attempt to showcase popup host / DialogFactory / RootComponentSettings.

---

## 2. Scheduling

### Summary

| Component | Mapping | Spec files | Verdict | Notes |
|---|---|---|---|---|
| calendar | SunfishDatePicker (partial) | 8 (overview, appearance, events, navigation, selection, multiview, accessibility/wai-aria, templates/overview + 4 cell templates + header-template = 13 total .md) | **downgrade-to-partial** (confirms current partial flag) — but a **standalone SunfishCalendar already exists** and should carry this mapping instead of SunfishDatePicker. |

### 2.1 calendar

#### Mapping-level finding (critical)

The mapping says `calendar → SunfishDatePicker (partial)`. This is misleading:

- A standalone `SunfishCalendar` component exists at `packages/ui-adapters-blazor/Components/DataDisplay/SunfishCalendar.razor` and covers Month/Year/Decade views, header + cell templates (`CellTemplate`, `HeaderTemplate`), `Min`/`Max`, `ShowWeekNumbers`, `ShowOtherMonthDays`, `Orientation`, and navigation.
- `SunfishDatePicker` is a separate component in `Components/Forms/Inputs/SunfishDatePicker.razor` and embeds its own simpler calendar popup — it is not intended to be a standalone Calendar.
- The kitchen-sink has a Calendar demo (`apps/kitchen-sink/Pages/Components/Editors/Calendar/Overview/Demo.razor`) that uses `SunfishCalendar`, confirming the intent.

Recommendation: **remap** `calendar → SunfishCalendar` and treat `SunfishDatePicker` as the backing for the `datepicker`/`dateinput` specs only. The rest of this section audits SunfishCalendar (the correct target) against the calendar spec.

#### Gaps (SunfishCalendar vs calendar spec)

| Severity | Area | Detail |
|---|---|---|
| missing | Parameter — `Date` | Spec: current-view anchor date, two-way bindable. Impl uses private `_displayDate` only; no `Date`/`DateChanged`. Navigation examples in spec (`@bind-Date`) cannot compile. |
| missing | Parameter — `SelectedDates` | Spec defines multi-selection via `List<DateTime> SelectedDates`. Impl has only `SelectionCount: int` (placeholder) and no multi-selection support; `Value` is single `DateTime?`. |
| missing | Parameter — `SelectionMode` | Spec defines `CalendarSelectionMode` enum (Single / Multiple / Range). Impl has no `SelectionMode` or `CalendarSelectionMode` type. Range/multi-select examples in spec will not compile. |
| missing | Parameter — `RangeStart` / `RangeEnd` / `RangeStartChanged` / `RangeEndChanged` / `AllowReverse` | Range selection surface entirely absent from impl. |
| missing | Parameter — `DisabledDates` | Spec: `List<DateTime>` to block selection. Impl has no `DisabledDates` collection. Only `Min` / `Max` constraints exist. |
| missing | Parameter — `TopView` / `BottomView` | Spec lets callers cap navigation (e.g., BottomView=Year). Impl has no TopView/BottomView. |
| missing | Parameter — `Views` (multi-view) | Spec documents multi-view rendering via `Views: int` + `Orientation`. Impl has `Orientation` (used only for a CSS class, not panel layout) and no `Views` param — only a single view renders. |
| missing | View — Century | Spec defines 4 zoom levels (Century, Decade, Year, Month). Impl renders Month, Year, Decade only — `CalendarView.Century` is not handled in the `@if` chain. |
| missing | Templates — typed per-view | Spec defines distinct templates: `MonthCellTemplate`, `YearCellTemplate`, `DecadeCellTemplate`, `CenturyCellTemplate`, `HeaderTemplate`. Impl has a single generic `CellTemplate: RenderFragment<DateTime>` shared across month view only (not wired in year/decade views) + a `HeaderTemplate` (wired only in month view). |
| missing | Event — `OnCellRender` | Spec's cell styling/customization via `CalendarCellRenderEventArgs` (Class/Date/View) is absent. |
| missing | Event — `ValueChanged` argument shape | Spec's `ValueChanged` returns `DateTime`. Impl returns `DateTime?`. Non-fatal, but doc examples use non-nullable. |
| missing | Appearance — `Size` | Spec documents `Size` via `ThemeConstants.Calendar.Size` (`Small`/`Medium`/`Large`). Impl has no `Size` parameter. |
| missing | Methods — `NavigateTo` / `Refresh` | Spec documents `NavigateTo(DateTime, CalendarView)` and `Refresh()`. Impl exposes neither publicly. |
| missing | WAI-ARIA (`accessibility/wai-aria-support.md`) | Spec requires the grid to expose `role="grid"`, `aria-activedescendant`, `aria-labelledby` to the nav-fast label, `aria-disabled` on disabled cells, `tabindex="-1"` on nav/prev/next/today buttons, and `aria-label` on abbreviated Mon/Tue headers. Impl's `<table role="grid">` is set, but missing `aria-activedescendant`, `aria-labelledby`, `aria-label` on header abbreviations, and the nav buttons use `aria-label` but are focusable (no `tabindex="-1"`). `aria-disabled` is not set on disabled cells (only a CSS class). |
| missing | Today button | Spec documents a "Today" link (`role=link`, `.k-nav-today`). Impl has no Today action. |
| incomplete | Week numbers | Spec: ISO-8601. Impl calls `GetWeekOfYear(…, FirstFourDayWeek, DayOfWeek.Monday)` which approximates ISO but is not `ISOWeek.GetWeekOfYear` — may drift at year boundaries. |
| covered | Basic month navigation / prev-next arrows | Present in impl. |
| covered | ShowOtherMonthDays | Parameter exists and is honored. |
| covered | Orientation enum presence | `CalendarOrientation` enum exists; however not wired into multi-view layout (see `Views` gap). |

#### Gaps (if mapping were intentionally SunfishDatePicker)

If the mapping is kept as `SunfishDatePicker`, the gap set is larger still: SunfishDatePicker is a closed popup picker over `DateOnly`, lacks Year/Decade/Century views, multi-selection, range selection, templates, and view-navigation events. `downgrade-to-partial` is generous; `missing-core-features` would be more accurate.

#### Verdict: **downgrade-to-partial** (confirmed). Standalone calendar work needed: mapping fix + parameter surface + a11y uplift + templates + multi-view.

- Spec files reviewed (13):
  - `apps/docs/component-specs/calendar/overview.md`
  - `apps/docs/component-specs/calendar/appearance.md`
  - `apps/docs/component-specs/calendar/events.md`
  - `apps/docs/component-specs/calendar/navigation.md`
  - `apps/docs/component-specs/calendar/selection.md`
  - `apps/docs/component-specs/calendar/multiview.md`
  - `apps/docs/component-specs/calendar/accessibility/wai-aria-support.md`
  - `apps/docs/component-specs/calendar/templates/overview.md`
  - `apps/docs/component-specs/calendar/templates/month-cell.md`
  - `apps/docs/component-specs/calendar/templates/year-cell.md`
  - `apps/docs/component-specs/calendar/templates/decade-cell.md`
  - `apps/docs/component-specs/calendar/templates/century-cell.md`
  - `apps/docs/component-specs/calendar/templates/header-template.md`
- Impl (actual): `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\SunfishCalendar.razor`.
- Impl (per current mapping): `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Forms\Inputs\SunfishDatePicker.razor`.
- Demo: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Editors\Calendar\Overview\Demo.razor` — Overview only. References `/components/editors/calendar/range-selection`, `/multiple-selection`, `/templates` that do **not** exist (`apps/kitchen-sink/Pages/Components/Editors/Calendar/` contains only `Overview/`). Broken links.

---

## 3. Cross-family Next Actions (Tier 2 priorities)

Ordered by impact, then effort:

1. **Fix component mapping for `calendar`** — change `component-mapping.json` entry `calendar.sunfish` from `SunfishDatePicker` to `SunfishCalendar` (and ensure `datepicker`/`dateinput` continue to point at SunfishDatePicker). Blocks the Calendar verification work. *(30 min, docs only.)*
2. **Fix component mapping for `popup`** — change `popup.sunfish` from `SunfishPopover` to `SunfishPopup`. `popover` remains `SunfishPopover`. *(30 min, docs only.)*
3. **SunfishCalendar parameter surface** — add `Date`/`DateChanged`, `SelectionMode` + `CalendarSelectionMode` enum, `SelectedDates`, `RangeStart`/`RangeEnd`/`AllowReverse`, `DisabledDates`, `TopView`/`BottomView`, `Views` (multi-view), `Size`, `NavigateTo()`/`Refresh()`. Biggest user-visible gap of the entire audit.
4. **SunfishCalendar Century view + per-view templates** — add the missing `CalendarView.Century` branch and split `CellTemplate` into `MonthCellTemplate`/`YearCellTemplate`/`DecadeCellTemplate`/`CenturyCellTemplate`. Wire `HeaderTemplate` into all views. Add `OnCellRender` event.
5. **SunfishCalendar WAI-ARIA uplift** — add `aria-activedescendant`, `aria-labelledby`, `tabindex=-1` on nav buttons, `aria-disabled` on disabled gridcells, `aria-label` on weekday headers, and a "Today" link.
6. **Calendar demo family** — add range-selection, multiple-selection, templates demos under `apps/kitchen-sink/Pages/Components/Editors/Calendar/` to match links already present in `Overview.razor` (currently broken).
7. **SunfishPopup alignment/collision/animation surface** — add `AnchorHorizontalAlign`/`AnchorVerticalAlign`/`HorizontalAlign`/`VerticalAlign`, `HorizontalCollision`/`VerticalCollision`, `AnimationType`+`AnimationDuration`, `Width`/`Height`, and `AnchorSelector` as an alternative to `AnchorId`. Without this the spec examples do not compile.
8. **SunfishPopup focus return** — implement focus return to trigger on close (spec-required).
9. **SunfishThemeProvider → root shell** — add `EnableRtl`, `IconType`, `Localizer` params; add a popup/dialog host region (currently any component that wants to render popups above `overflow:hidden` containers has nowhere to portal to); expose a `DialogFactory`; add `RootComponentSettings` nested tag for adaptive breakpoints. Either rename to `SunfishRootComponent` or publish a `SunfishRootComponent` type-alias to keep spec examples compilable.
10. **Popup demo family** — add animation and position-collision demos to match the three spec files (only overview is demo'd today).
11. **RootComponent interactivity demo** — add a demo/knowledge-base page for per-component interactivity (spec's second file), even if it just links to the pattern, to keep docs ↔ demo parity.

### Biggest single gap

SunfishCalendar is missing ~12 of the spec's ~20 documented parameters (selection modes, ranges, disabled dates, multi-view, per-view templates, size, top/bottom view) **and** the current mapping points at the wrong component. Fixing the mapping (action 1) is the single highest-leverage change; action 3 is the single biggest implementation task.

### Severity roll-up

| Family | missing | bug | incomplete | covered |
|---|---|---|---|---|
| Utility (2 components) | 13 | 1 | 3 | 4 |
| Scheduling (1 component) | 14 | 0 | 2 | 3 |
| **Total** | **27** | **1** | **5** | **7** |

Verdicts: 0 verified · 3 needs-work (popup, rootcomponent, calendar — calendar also carries a confirm-partial downgrade) · 0 standalone downgrade. All three components keep `partial` as accurate.
