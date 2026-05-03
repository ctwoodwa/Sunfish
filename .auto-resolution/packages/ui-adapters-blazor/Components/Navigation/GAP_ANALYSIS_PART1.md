# Navigation Components - Gap Analysis (Part 1)

## 1. SunfishBreadcrumb.razor

**Spec expects:** `Data` property (collection), `CollapseMode`, `Width`, `Height`, `Size`, `ItemTemplate`, `SeparatorTemplate`, events, generic typed component (`SunfishBreadcrumb<T>`)
**Current implementation:** `ChildContent` only (manual markup), no data binding, no templates, no sizing/collapse

- **MISSING: `Data` property and data-binding model** -- spec centers entirely on data-driven rendering; component has none
- **MISSING: `ItemTemplate` / `SeparatorTemplate`** -- spec lists both as key features
- **MISSING: `CollapseMode`, `Width`, `Height`, `Size`** -- layout/sizing parameters absent
- **MISSING: Events** -- spec references breadcrumb events; none implemented
- Current `ChildContent` approach works but is undocumented in spec; could coexist with `Data`

## 2. SunfishBreadcrumbItem.razor

**No dedicated spec** (part of breadcrumb). Evaluated against breadcrumb spec's `BreadcrumbItem` model.

- Has `Href`, `IsActive`, `ChildContent` -- reasonable for manual/template mode
- **MISSING: `Icon` support** -- spec's model includes `ISvgIcon Icon`
- **MISSING: `Text` field binding** -- spec model uses `Text`; component relies on `ChildContent` only
- Adequately handles active state with `aria-current="page"`

## 3. SunfishContextMenu.razor

**Spec expects:** `Data` property (hierarchical), `Selector` (CSS selector for targets), `OnClick` event, `ShowAsync`/`HideAsync` methods, `Template`, popup collision settings, generic typed component

- **MISSING: `Data` property and data-binding** -- spec is entirely data-driven; component uses `MenuContent` RenderFragment only
- **MISSING: `Selector` parameter** -- spec attaches to targets via CSS selector; component wraps trigger content inline instead
- **MISSING: `OnClick` event, `ShowAsync`/`HideAsync` methods** -- spec lists all three; none present
- **MISSING: Templates, popup collision settings, hierarchical items**
- Has basic show/hide via right-click with overlay dismiss -- functional but minimal

## 4. SunfishEnvironmentBadge.razor

**No spec.** Evaluated standalone.

- Reads environment from `IConfiguration` (`CommandCenter:Environment` or `ASPNETCORE_ENVIRONMENT`), defaults to "DEV"
- Renders uppercase environment name in a `<span>` with title attribute
- No configurable parameters beyond base class (`Class`, `Style`, `AdditionalAttributes`)
- Consider adding: explicit `Environment` parameter override, color/severity theming, visibility toggle for production

## 5. SunfishMenu.razor

**Spec expects:** `Data` (hierarchical), `CloseOnClick`, `ShowOn`/`HideOn` (hover vs click), popup settings, `Class`, templates, events, navigation via `UrlField`

- **Has `Data` with field-name bindings** (`TextField`, `IconField`, `UrlField`, `SeparatorField`, `DisabledField`, `IdField`, `ParentIdField`, `ItemsField`, `HasChildrenField`) -- good coverage
- **MISSING: Hierarchical/nested rendering** -- has `ParentIdField`/`ItemsField` params but only renders flat list from `Data`
- **MISSING: `CloseOnClick`, `ShowOn`, `HideOn`** -- spec's show/hide behavior parameters absent
- **MISSING: Popup collision settings** (`HorizontalCollision`, `VerticalCollision`)
- **MISSING: `OnClick` event** -- spec lists item click events; only overlay close is handled

## 6. SunfishMenuDivider.razor

**No dedicated spec** (part of menu).

- Simple `<div>` with `role="separator"` -- correct semantics
- Uses base class CSS/style/attributes -- complete for its purpose
- No gaps identified; component is intentionally minimal

## 7. SunfishMenuItem.razor

**No dedicated spec** (part of menu). Evaluated against menu spec's item model.

- Has `OnClick`, `IsDisabled`, `Icon`, `ChildContent` -- good basic coverage
- **MISSING: `Url`/`Href` support** -- spec items can be links; component only renders `<button>`
- **MISSING: Nested/child items** -- spec supports hierarchical menus; no sub-menu capability
- **MISSING: `Text` field** -- relies on `ChildContent` only (acceptable for template mode)

## 8. SunfishNavBar.razor

**No spec.** Evaluated standalone.

- Minimal wrapper: `<nav>` with `role="navigation"` and `ChildContent`
- Uses base class CSS/style/attributes -- complete for a container
- Consider adding: `Brand`/`Logo` slot, responsive collapse/toggle, orientation parameter

## 9. SunfishNavItem.razor

**No spec.** Evaluated standalone.

- Renders `<a>` when `Href` is set, `<button>` otherwise -- good pattern
- Has `IsActive` with `aria-current="page"` -- correct accessibility
- Has `OnClick` event -- functional
- Consider adding: `Icon` support, `Disabled` state, badge/indicator slot

## 10. SunfishNavMenu.razor

**No spec.** Evaluated standalone.

- Wraps `<ul role="list">` inside `<nav>` -- correct semantics
- Uses base class CSS/style/attributes -- complete for a container
- Consider adding: `Orientation` (horizontal/vertical), collapse/expand toggle, `Data` binding

---

## Summary

| Component | Spec? | Status |
|-----------|-------|--------|
| SunfishBreadcrumb | Yes | **Major gaps** -- no data binding, templates, sizing, or events |
| SunfishBreadcrumbItem | (part of above) | Missing icon support and text field |
| SunfishContextMenu | Yes | **Major gaps** -- no data binding, selector, methods, or events |
| SunfishEnvironmentBadge | No | Functional; consider parameter override |
| SunfishMenu | Yes | **Moderate gaps** -- has data binding but missing hierarchy, show/hide, events |
| SunfishMenuDivider | (part of menu) | Complete |
| SunfishMenuItem | (part of menu) | Missing URL/link mode and sub-menus |
| SunfishNavBar | No | Minimal but functional container |
| SunfishNavItem | No | Good; consider icon/disabled support |
| SunfishNavMenu | No | Minimal but functional container |
