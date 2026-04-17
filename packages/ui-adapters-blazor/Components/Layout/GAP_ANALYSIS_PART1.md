# Gap Analysis Part 1 -- Layout Components

> **Note (2026-04-03):** `SunfishGrid` was renamed to `SunfishGridLayout`. Historical references to `SunfishGrid` in this document refer to the layout component now at `Layout/SunfishGridLayout.razor`.

Base class (`SunfishComponentBase`) provides: `Class`, `Style`, `AdditionalAttributes`.

---

## 1. SunfishAccordion.razor (spec: panelbar/overview.md)

| Gap | Severity | Details |
|-----|----------|---------|
| No `Data` property / data binding | **High** | Spec expects `Data` property with flat or hierarchical collections. Component only accepts `ChildContent`. |
| No `ExpandMode` parameter | **High** | Spec defines `PanelBarExpandMode` (Single/Multiple). Component has no expand coordination between items. |
| No `ExpandedItems` collection | **High** | Spec supports two-way-bound `ExpandedItems` collection. Not implemented. |
| No hierarchical nesting | **Medium** | Spec supports multi-level nested items with parent/child relationships. Component is flat. |
| No templates (Header/Content) | **Medium** | Spec supports `HeaderTemplate` and `ContentTemplate`. Not implemented. |
| No navigation support | **Medium** | Spec items support `Url` field for built-in navigation. Not implemented. |
| No icons/images on items | **Low** | Spec supports icon classes, SVG icons, images per item. Not implemented. |
| No `Disabled` per item | **Low** | Spec model supports `Disabled` flag. Not implemented. |
| No `Rebind` method | **Low** | Spec exposes `Rebind()` for data refresh. Not implemented. |

## 2. SunfishAccordionItem.razor (part of panelbar spec)

| Gap | Severity | Details |
|-----|----------|---------|
| No data-driven rendering | **High** | Spec expects items generated from data. This is a manual child component only. |
| No `Icon` parameter | **Low** | Spec items support icons. Not present. |
| No `Disabled` parameter | **Low** | Spec items can be disabled. Not present. |
| No `Url` / navigation | **Medium** | Spec items can have URLs for navigation. Not present. |

Current implementation is a simple manual accordion item with `Title`, `IsExpanded` (two-way), and `ChildContent`. Functional but far from spec.

---

## 3. SunfishAppBar.razor (spec: appbar/overview.md)

| Gap | Severity | Details |
|-----|----------|---------|
| No `PositionMode` parameter | **Medium** | Spec has `PositionMode` (Static/Fixed/Sticky). Component only has `Position` (Top). |
| No `ThemeColor` parameter | **Medium** | Spec supports theme color adjustment. Not implemented. |
| No `Height` / `Width` parameters | **Low** | Spec exposes dedicated `Height` and `Width` params. Component relies on `Style` from base. |
| No child component structure (`AppBarSection`, `AppBarSpacer`, `AppBarSeparator`) | **High** | Spec uses structured child components for sections, spacers, and separators. Component only accepts raw `ChildContent`. |
| No `Refresh` method | **Low** | Spec exposes `Refresh()` method via `@ref`. Not implemented. |

---

## 4. SunfishColumn.razor (no standalone spec)

No spec to compare. Current parameters: `Span`, `Offset`, `ChildContent`. Appears to be a simple CSS grid/flex column helper. No gaps identifiable without a spec.

---

## 5. SunfishContainer.razor (no spec)

No spec to compare. Renders a plain `<div>` wrapper with `ChildContent`. Minimal utility component. No gaps identifiable.

---

## 6. SunfishDivider.razor (no spec)

No spec to compare. Renders `<hr>` with a `Vertical` boolean parameter. Minimal. No gaps identifiable.

---

## 7. SunfishDrawer.razor (spec: drawer/overview.md)

| Gap | Severity | Details |
|-----|----------|---------|
| No `Data` property / data binding | **High** | Spec expects `Data` with item collection (text, icons, URLs). Component only accepts `ChildContent`. |
| No `SelectedItem` parameter | **High** | Spec supports two-way-bound `SelectedItem` for selection tracking. Not implemented. |
| No `Mode` parameter (Push/Overlay) | **High** | Spec supports `DrawerMode.Push` and `DrawerMode.Overlay`. Component always overlays. |
| No `MiniMode` parameter | **Medium** | Spec supports a collapsed mini-view showing icons. Not implemented. |
| No `Expanded` parameter (uses `IsOpen`) | **Low** | Spec uses `Expanded` (bool). Component uses `IsOpen` -- naming divergence. |
| No `ExpandAsync` / `CollapseAsync` / `ToggleAsync` methods | **Medium** | Spec exposes async methods via `@ref`. Component uses `OnClose` callback only. |
| No `DrawerContent` child tag | **Medium** | Spec separates drawer items from page content via `<DrawerContent>`. Component has no such structure. |
| No templates (ItemTemplate / Template) | **Medium** | Spec supports full template customization. Not implemented. |
| No navigation support | **Medium** | Spec items support `UrlField` for built-in nav links. Not implemented. |
| No selection events | **Low** | Spec fires select/expand events. Only `OnClose` exists. |
| Overlay click uses `OnClose` (no animation) | **Low** | Spec methods provide animations; direct parameter toggle does not. |

---

## 8. SunfishGrid.razor (spec: gridlayout/overview.md)

| Gap | Severity | Details |
|-----|----------|---------|
| No `GridLayoutColumns` / `GridLayoutColumn` children | **High** | Spec defines columns via `<GridLayoutColumn Width="...">`. Component only has `ChildContent`. |
| No `GridLayoutRows` / `GridLayoutRow` children | **High** | Spec defines rows via `<GridLayoutRow Height="...">`. Not implemented. |
| No `GridLayoutItems` / `GridLayoutItem` children | **High** | Spec positions items in grid cells with row/column/span. Not implemented. |
| No `ColumnSpacing` parameter | **Medium** | Spec supports `ColumnSpacing`. Not implemented. |
| No `RowSpacing` parameter | **Medium** | Spec supports `RowSpacing`. Not implemented. |
| No `Width` parameter | **Low** | Spec has `Width`. Component relies on base `Style`. |
| No `HorizontalAlign` / `VerticalAlign` | **Medium** | Spec supports alignment enums (Stretch, Start, Center, End). Not implemented. |

---

## Summary

| Component | High | Medium | Low | Notes |
|-----------|------|--------|-----|-------|
| SunfishAccordion | 3 | 3 | 3 | Needs data binding, expand coordination, hierarchy |
| SunfishAccordionItem | 1 | 1 | 2 | Manual-only; no data-driven rendering |
| SunfishAppBar | 1 | 2 | 2 | Missing structured child components |
| SunfishColumn | -- | -- | -- | No spec |
| SunfishContainer | -- | -- | -- | No spec |
| SunfishDivider | -- | -- | -- | No spec |
| SunfishDrawer | 3 | 4 | 3 | Needs data binding, modes, selection |
| SunfishGrid | 3 | 3 | 1 | Needs full grid structure (rows/cols/items) |
| **Totals** | **11** | **13** | **11** | |

**Top priorities:** SunfishDrawer, SunfishAccordion, and SunfishGrid each have 3 High-severity gaps centered on missing data binding and structural child components.
