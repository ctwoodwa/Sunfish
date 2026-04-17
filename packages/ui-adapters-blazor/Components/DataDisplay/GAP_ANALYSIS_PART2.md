# DataDisplay Components - Gap Analysis (Part 2)

> **Note (2026-04-03):** `SunfishTable` has been removed as an obsolete component. It was superseded by `SunfishDataGrid`. Historical references to `SunfishTable` in this document are retained for audit accuracy.

Base class (`SunfishComponentBase`) provides: `Class`, `Style`, `AdditionalAttributes`.

---

## 1. SunfishList.razor (spec: listbox/overview.md)

**Implemented:** `ChildContent` -- renders a simple `<ul>` wrapper.

**Spec requires but missing:**

| Gap | Severity |
|-----|----------|
| `Data` parameter with `IEnumerable<T>` binding | **[High]** |
| `TextField` for model property mapping | **[High]** |
| `SelectionMode` (Single/Multiple) and `SelectedItems` two-way binding | **[High]** |
| Toolbar (`ListBoxToolBarSettings`) with move up/down/remove/transfer buttons | **[High]** |
| `ConnectedListBoxId` / `DropSources` for multi-listbox transfer | **[Medium]** |
| Drag-and-drop reordering (`Draggable`, `OnReorder` event) | **[Medium]** |
| `Enabled` parameter | **[Medium]** |
| `Width` / `Height` parameters | **[Low]** |
| `Size` parameter (padding/font control) | **[Low]** |
| `AriaLabel` / `AriaLabelledBy` accessibility attributes | **[Medium]** |
| `Rebind()` method via `@ref` | **[Medium]** |
| Item templates | **[Medium]** |

**Summary:** Current implementation is a bare `<ul>` container. It is essentially a layout wrapper with no ListBox functionality. Nearly all spec features are missing.

---

## 2. SunfishListItem.razor (part of listbox spec)

**Implemented:** `ChildContent` -- renders a simple `<li>` wrapper.

| Gap | Severity |
|-----|----------|
| No selection state, click handling, or disabled state | **[High]** |
| No integration with parent SunfishList for selection/reorder | **[High]** |

**Summary:** Purely structural. The spec expects the ListBox to render items from data, not via manual `<SunfishListItem>` children. The entire item-rendering model differs from spec.

---

## 3. SunfishListView.razor (spec: listview/overview.md)

**Implemented:** `Data`, `ItemTemplate` (EditorRequired), `Pageable`, `PageSize`, `SelectionMode` (Single/Multiple via `GridSelectionMode`), `SelectedItems` two-way binding, `EditTemplate`, `OnCreate`/`OnUpdate`/`OnDelete` events, `OnRead` for server-side data, paging UI with Previous/Next buttons.

**Spec requires but missing:**

| Gap | Severity |
|-----|----------|
| `HeaderTemplate` / `FooterTemplate` | **[Medium]** |
| `Width` / `Height` parameters | **[Low]** |
| `Page` parameter (external page control / two-way binding) | **[Medium]** |
| `EnableLoaderContainer` for long-running operations | **[Low]** |
| Insert mode / add-new-item command | **[Medium]** |
| `Rebind()` method for programmatic data refresh | **[Low]** |

**Already beyond spec:** Server-side `OnRead` with `DataRequest`, `OnCreate`/`OnUpdate`/`OnDelete` CRUD events, selection support -- these are good additions.

**Summary:** Solid implementation covering core functionality. Main gaps are header/footer templates and external page control.

---

## 4. SunfishPopover.razor (spec: popover/overview.md)

**Implemented:** `IsOpen` two-way binding, `Position` (reuses `TooltipPosition`), `ChildContent` (trigger), `PopoverContent`, toggle on click.

**Spec requires but missing:**

| Gap | Severity |
|-----|----------|
| `AnchorSelector` parameter (CSS selector targeting) | **[High]** |
| `ShowOn` parameter (`MouseEnter` / `Click`) -- currently hardcoded to click only | **[Medium]** |
| `<PopoverHeader>` child tag | **[Medium]** |
| `<PopoverActions>` child tag with `ActionsLayout` | **[Medium]** |
| `Show()` / `Hide()` / `Refresh()` programmatic methods | **[High]** |
| `AnimationType` / `AnimationDuration` | **[Low]** |
| `Collision` behavior for viewport edge detection | **[Medium]** |
| `Offset` parameter | **[Low]** |
| `ShowCallout` parameter | **[Low]** |
| `Width` / `Height` parameters | **[Low]** |
| Rendering as child of `SunfishRootComponent` (portal pattern) | **[Medium]** |

**Summary:** Basic open/close toggle works. Missing anchor-based positioning model, structured content regions (header/actions), programmatic API, and animation.

---

## 5. SunfishTable.razor (no spec)

**Implemented:**
- `ChildContent` -- renders a simple `<table>` wrapper.

**Summary:** Bare structural wrapper. Provides only `<table>` element with class/style/attributes from base. No columns, data binding, sorting, filtering, or paging. Intended as a simple HTML table container for manual markup.

---

## 6. SunfishTimeline.razor (no spec)

**Implemented:**
- `ChildContent` -- renders a `<div>` container with timeline CSS class.

**Summary:** Simple container for `SunfishTimelineItem` children. No orientation, alignment mode, or reverse parameters.

---

## 7. SunfishTimelineItem.razor (no spec)

**Implemented:**
- `Title` (string) -- rendered in a title div
- `Timestamp` (string) -- rendered in a timestamp div
- `Icon` (string) -- rendered via `IconProvider` at small size in a dot element
- `ChildContent` -- additional content area

**Summary:** Functional timeline item with dot/icon, title, timestamp, and free-form content. Reasonable feature set for a no-spec component.

---

## 8. SunfishTooltip.razor (spec: tooltip/overview.md)

**Implemented:** `Position` (`TooltipPosition`, default `Top`), `Text`, `ChildContent` (wraps target inline), `TooltipTemplate` (custom content), `ShowOn` (`TooltipShowOn`: Hover/Click/Focus), `Width`, `Height`, `OnShow`/`OnHide` events, `ShowCallout` with directional callout span.

**Spec requires but missing:**

| Gap | Severity |
|-----|----------|
| `TargetSelector` (CSS selector to attach to external elements) | **[High]** -- current model wraps the target; spec model uses a selector to attach to one or many external targets |
| `ShowDelay` / `HideDelay` timing parameters | **[Medium]** |
| `Id` for accessibility (`aria-describedby` linkage) | **[Medium]** |
| Single-instance-many-targets optimization pattern | **[Medium]** |
| Auto-flip/shift when insufficient viewport space | **[Low]** |
| Reading `title`/`alt` attributes as fallback content | **[Low]** |

**Already beyond spec:** `OnShow`/`OnHide` events, Focus trigger mode, `ShowCallout`, inline `Width`/`Height` styling -- good additions not in the spec overview.

**Summary:** Works well as a wrapper-style tooltip. The fundamental architecture differs from spec (wrapper vs. selector-based). Adding `TargetSelector` support would be the highest-impact change.

---

## 9. SunfishTypography.razor (no spec)

**Implemented:**
- `Variant` (`TypographyVariant`, default `Body1`) -- maps to HTML elements:
  - H1-H6 -> `<h1>`-`<h6>`
  - Caption, Overline -> `<span>`
  - Body1/Body2/default -> `<p>`
- `ChildContent`

**Summary:** Clean semantic typography component. Renders correct HTML elements per variant with theme CSS classes. No spec to compare against; implementation is straightforward and complete for its scope.

---

## Priority Summary

| Priority | Components | Key Action |
|----------|-----------|------------|
| **Critical** | SunfishList/SunfishListItem | Rewrite as data-driven ListBox with selection, toolbar, drag-drop |
| **High** | SunfishPopover | Add anchor selector model, programmatic Show/Hide, header/actions |
| **High** | SunfishTooltip | Add TargetSelector for external/multi-target attachment |
| **Moderate** | SunfishListView | Add header/footer templates, external page control |
| **Low** | SunfishTable, SunfishTimeline, SunfishTimelineItem, SunfishTypography | Adequate for current scope; enhance as needed |
