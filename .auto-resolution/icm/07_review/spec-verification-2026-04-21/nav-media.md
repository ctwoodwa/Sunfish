# Spec Verification Audit — Navigation + Media Families

**Pipeline:** ICM Stage 07 · sunfish-quality-control
**Date:** 2026-04-21
**Scope:** ADR 0022 Tier 1 — verify Blazor impl + demos against `apps/docs/component-specs/` for Navigation (breadcrumb, contextmenu, menu, pager, toolbar, treeview, stepper) and Media (carousel) families where `status ∈ {implemented, partial}`. Planned components skipped.
**Basis:** `apps/docs/component-specs/component-mapping.json`
**Mode:** Read-only audit — no impl or demo changes.

---

## Summary

### Navigation

| Component | Mapping Status | Verdict | Gaps (missing/bug/incomplete) |
| --- | --- | --- | --- |
| Breadcrumb | implemented | needs-work | 0 / 0 / 7 |
| ContextMenu | implemented | needs-work | 3 / 0 / 4 |
| Menu | implemented | needs-work | 3 / 0 / 4 |
| Pager | implemented | downgrade-to-partial | 4 / 1 / 3 |
| Toolbar | implemented | needs-work | 4 / 1 / 4 |
| TreeView | implemented | verified | 0 / 0 / 3 |
| Stepper | implemented | needs-work | 3 / 0 / 4 |

### Media

| Component | Mapping Status | Verdict | Gaps (missing/bug/incomplete) |
| --- | --- | --- | --- |
| Carousel | implemented | needs-work | 2 / 1 / 3 |

### Cross-family folder drift (a finding itself)

| Component | Mapping category | Impl folder | Demo folder |
| --- | --- | --- | --- |
| TreeView | navigation | `Components/Navigation/TreeView/` | `Pages/Components/DataDisplay/TreeView/` |
| Stepper | navigation | `Components/Layout/Stepper/` | `Pages/Components/Navigation/Stepper/` |
| Carousel | media | `Components/DataDisplay/SunfishCarousel.razor` | `Pages/Components/DataDisplay/Carousel/` (no `Media/` tree) |
| Pager / Pagination | navigation | `Components/Navigation/SunfishPagination.razor` (impl renamed from spec "Pager") | `Pages/Components/Navigation/Pager/` AND `.../Pagination/` (two stub demos) |

---

## Navigation

### Breadcrumb

**Verdict:** needs-work

| Gap | Severity | Detail |
| --- | --- | --- |
| `CollapseMode` parameter (Auto/Wrap/None) | incomplete | Spec `collapse-modes.md` requires collapsing behavior; impl has none. |
| `SeparatorIcon` + `SeparatorTemplate` | incomplete | Spec `separator.md` requires customizable glyph; impl only renders default `/`. |
| `OnItemClick` event | incomplete | Spec `events.md` defines event; impl has no event. |
| `Size` parameter | incomplete | Spec lists `Size`; impl omits. |
| `TitleField`, `DisabledField`, `ClassField` data bindings | incomplete | Spec `data-binding.md` maps these fields; impl supports only `TextField`/`UrlField`/`IconField`. |
| Data-bound `aria-current="page"` on last crumb | incomplete | Declarative `SunfishBreadcrumbItem` sets it on `IsActive`; data-bound path does not. |
| Demo limited to declarative usage | incomplete | `Overview/Demo.razor` shows declarative only; no data-bound sibling demos. |

**Paths:**
- Impl: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Navigation\Breadcrumb\SunfishBreadcrumb.razor`, `SunfishBreadcrumbItem.razor`
- Demo: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Navigation\Breadcrumb\Overview\Demo.razor`
- Specs reviewed: `overview.md`, `navigation.md`, `collapse-modes.md`, `data-binding.md`, `events.md`, `icons.md`, `separator.md`, `templates.md`, `accessibility/wai-aria-support.md`

---

### ContextMenu

**Verdict:** needs-work

| Gap | Severity | Detail |
| --- | --- | --- |
| `Refresh()` / `Rebind()` method | missing | Spec `refresh-data.md` requires it; impl has none. |
| `OnItemRender` event | missing | Spec `events.md` defines it; impl has no per-item render hook. |
| `Template` (content-level template slot) | missing | Spec `templates/content.md` defines full-content template; impl has only `ItemTemplate`. |
| `IdField`/`ParentIdField`/`UrlField` data-binding | incomplete | Spec `data-binding/flat-data.md` requires flat ID/ParentId; impl only supports hierarchical via `ItemsField`. No URL → anchor rendering. |
| Popup collision/position settings (`HorizontalCollision`, `VerticalCollision`, etc.) | incomplete | Spec `integration.md` describes popup settings; impl has none. |
| `Selector` not wired to external DOM | incomplete | `Selector` parameter exists but impl doesn't attach JS listeners to external targets; only internal `ChildContent` wrapping works. |
| Data-bound demo + Selector demo | incomplete | Overview demo shows only declarative `ChildContent`+`MenuContent`; no data-bound or `Selector`-driven example. |

**Paths:**
- Impl: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Navigation\ContextMenu\SunfishContextMenu.razor`, `SunfishContextMenu.razor.cs`
- Demo: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Navigation\ContextMenu\Overview\Demo.razor`
- Specs reviewed: `overview.md`, `events.md`, `integration.md`, `navigation.md`, `icons.md`, `refresh-data.md`, `accessibility/wai-aria-support.md`, `data-binding/*`, `templates/*`

---

### Menu

**Verdict:** needs-work

| Gap | Severity | Detail |
| --- | --- | --- |
| `Orientation` parameter (Horizontal default) | missing | Spec `orientation.md` requires horizontal inline rendering as default; impl is popup-only. |
| `HideOn` parameter | missing | Spec `show-hide-behavior.md` requires it alongside `ShowOn`; impl has only `ShowOn`. |
| `OnItemRender` event | missing | Spec `events.md` defines it; impl has no render hook. |
| Horizontal/inline display mode | incomplete | Impl always renders as absolute-positioned popup; spec defaults to inline horizontal menu with submenus. |
| Popup collision/position settings | incomplete | No `HorizontalCollision`/`VerticalCollision` tuning. |
| `UrlField` → anchor rendering in data mode | incomplete | Spec `navigation.md` requires clickable links for `UrlField`; impl binding has no `UrlField`. |
| Demo only shows popup toggle pattern | incomplete | Overview demo reflects the impl's popup-only limitation; does not exercise spec's horizontal default. |

**Paths:**
- Impl: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Navigation\Menu\SunfishMenu.razor`, `SunfishMenu.razor.cs`
- Demo: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Navigation\Menu\Overview\Demo.razor`
- Specs reviewed: `overview.md`, `events.md`, `navigation.md`, `orientation.md`, `icons.md`, `show-hide-behavior.md`, `templates.md`, `refresh-data.md`

---

### Pager

**Verdict:** downgrade-to-partial

| Gap | Severity | Detail |
| --- | --- | --- |
| Demo is aspirational placeholder | bug | `Navigation/Pager/Overview/Demo.razor` shows an "ASPIRATIONAL — not yet built" placeholder though mapping marks status `implemented`. Demo references `TelerikPager` as parity target, contradicting the actual `SunfishPagination` impl. |
| `Responsive` parameter | missing | Spec `overview.md` requires responsive behavior; impl has none. |
| `AdaptiveMode` parameter | missing | Spec defines adaptive mode; impl has none. |
| `InputType` parameter (Buttons/Dropdown) | missing | Spec defines page navigation input style; impl has only button bar. |
| `Size` parameter | missing | Spec lists `Size`; impl omits. |
| `role="application"` + `aria-roledescription="pager"` | incomplete | Spec `accessibility/wai-aria-support.md` requires these; impl uses `<nav role="navigation">` — semantically different. |
| Sibling `Pagination/Overview/Demo.razor` is also a stub | incomplete | Redundant second stub for same component under a different URL. |
| Mapping rename inconsistency (`pager` → `SunfishPagination`) | incomplete | Spec dir `pager/` maps to `SunfishPagination` but demo routes live under both `/Pager/` and `/Pagination/` creating confusion. |

**Paths:**
- Impl: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Navigation\SunfishPagination.razor`
- Demo (stub): `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Navigation\Pager\Overview\Demo.razor`
- Demo (stub): `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Navigation\Pagination\Overview\Demo.razor`
- Specs reviewed: `overview.md`, `appearance.md`, `events.md`, `accessibility/wai-aria-support.md`

---

### Toolbar

**Verdict:** needs-work

| Gap | Severity | Detail |
| --- | --- | --- |
| Demo uses `IsSelected`/`IsSelectedChanged` but impl's `SunfishToolbarToggleButton` takes `IsActive`/`IsActiveChanged` | bug | `Toolbar/Overview/Demo.razor` lines 34-36 will not compile against the current component parameter names. (Checked: earlier reading of `SunfishToolbarToggleButton.razor`.) |
| `SunfishToolbarSpacer` | missing | Spec `built-in-tools.md` includes a spacer for flex push; impl has none. |
| `SunfishToolbarTemplateItem` (explicit named slot) | missing | Spec `templated-item.md` defines a dedicated template item; impl requires ad-hoc children. |
| `Overflow` / `OverflowMode` / `OverflowText` | missing | Spec `overview.md` requires overflow dropdown; impl has none. |
| Scroll buttons (`ScrollButtonsPosition`, `ScrollButtonsVisibility`) | missing | Spec defines scroll buttons as alternative to overflow; impl has none. |
| `FillMode`, `Size` parameters | incomplete | Spec `appearance.md` defines both; impl omits. |
| ToggleButton naming mismatch vs spec | incomplete | Spec uses `Selected`/`SelectedChanged`; impl uses `IsActive`/`IsActiveChanged` — cosmetic parity issue. |
| `ButtonGroup.SelectionMode` (Single/Multiple) | incomplete | Spec supports group-scoped single-select toggle behavior; impl group does not. |

**Paths:**
- Impl: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Navigation\Toolbar\SunfishToolbar.razor`, `SunfishToolbarButton.razor`, `SunfishToolbarGroup.razor`, `SunfishToolbarSeparator.razor`, `SunfishToolbarToggleButton.razor`
- Demo: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Navigation\Toolbar\Overview\Demo.razor`
- Specs reviewed: `overview.md`, `appearance.md`, `built-in-tools.md`, `events.md`, `separators.md`, `templated-item.md`, `accessibility/wai-aria-support.md`

---

### TreeView

**Verdict:** verified

Implementation is the deepest of the family: flat + hierarchical data binding, `CheckBoxMode` (None/Single/Multiple), `AllowCheckChildren`/`AllowCheckParents` (spec uses `CheckChildren`/`CheckParents`), `SelectionMode`, keyboard nav (ArrowKeys/Home/End/Enter/Space/`*`/F2/Escape), `ExpandAllAsync` (with `includeUnloaded`/`maxDepth`/`ct`), `CollapseAllAsync`, `SelectNodeAsync`, `ClearFilter`, `Rebind`, `LoadChildrenAsync` lazy loading, `EnableDragDrop` with `OnItemDrop`, `AllowEditing` + `OnItemEdit` (F2/Enter/Escape), `OnItemContextMenu`, `CheckboxTemplate`/`CheckboxContext`, `Size` parameter + density CSS custom props, `ReadOnly`/`Disabled`, `FilterFunc`, `AutoExpand`, `SingleExpand`. ARIA: `role=tree` / `role=treeitem` / `role=group` / `aria-expanded` / `aria-selected` / `aria-checked` (tri-state `mixed`).

| Gap | Severity | Detail |
| --- | --- | --- |
| Parameter name drift: `AllowCheckChildren`/`AllowCheckParents` vs spec's `CheckChildren`/`CheckParents` | incomplete | Cosmetic parity; semantically equivalent. |
| `CheckOnClick` parameter | incomplete | Spec `checkboxes/overview.md` lists it; impl does not (keyboard Enter/Space will toggle both, but click-on-row won't). |
| Kitchen-sink Overview demo uses only declarative `SunfishTreeItem` (children shape) | incomplete | Demo does not exercise `Data` + flat/hierarchical bindings, checkboxes, selection, drag-drop, or lazy loading — though sibling `Accessibility`/`Appearance`/`Events` demos exist under `DataDisplay/TreeView/` (folder drift noted). |

**Paths:**
- Impl: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Navigation\TreeView\SunfishTreeView.razor`, `SunfishTreeView.razor.cs`, `SunfishTreeItem.razor`, `SunfishTreeItem.razor.cs`
- Demo: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\DataDisplay\TreeView\Overview\Demo.razor` (folder mismatch vs mapping category)
- Specs reviewed: `overview.md`, `events.md`, `drag-drop.md`, `navigation.md`, `icons.md`, `expanded-items.md`, `refresh-data.md`, `templates.md`, `accessibility/wai-aria-support.md`, `checkboxes/overview.md`, `data-binding/overview.md`, `selection/overview.md`

---

### Stepper

**Verdict:** needs-work

| Gap | Severity | Detail |
| --- | --- | --- |
| `StepType` enum (`Steps` / `Labels`) | missing | Spec `display-modes.md` requires `StepType`; impl has no display mode switch. |
| `OnChange` event with `StepperStepChangeEventArgs` (cancellable) | missing | Spec `events.md` requires cancellable `OnChange`; impl has only `OnStepClick` and `ActiveStepChanged` (neither cancellable). |
| `Valid` (bool?) parameter on `SunfishStep` | missing | Spec `steps/validation.md` requires per-step validity; impl has `StepStatus?` only. |
| API naming drift: `ActiveStep`/`ActiveStepChanged` vs spec `Value`/`ValueChanged` | incomplete | Semantically equivalent but breaks spec parity. |
| Child slot naming drift: impl uses flat `SunfishStep` directly as `ChildContent` vs spec's `<StepperSteps>` wrapper tag | incomplete | Child authoring differs — no `StepperSteps` wrapper in impl. |
| Step label parameter naming: `Title` vs spec `Label` (+ `Text` for indicator override) | incomplete | Only `Title` exists; there is no distinct `Label`/`Text` split, so indicator-only overrides and under-indicator labels can't be styled independently. |
| Keyboard navigation + focus ring | incomplete | No explicit keyboard handler in `SunfishStepper.razor` — relies on native button tab focus and click. ARIA pattern guidance (`aria-current="step"`, `role="tablist"` or Wizard pattern) is not applied. |

**Paths:**
- Impl: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\Layout\Stepper\SunfishStepper.razor`, `SunfishStep.razor` (folder mismatch vs mapping category `navigation`)
- Demo: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\Navigation\Stepper\Overview\Demo.razor`
- Specs reviewed: `overview.md`, `display-modes.md`, `linear-flow.md`, `orientation.md`, `events.md`, `step-template.md`, `steps/overview.md`, `steps/indicators.md`, `steps/labels.md`, `steps/state.md`, `steps/validation.md`, `accessibility/wai-aria-support.md`

---

## Media

### Carousel

**Verdict:** needs-work

| Gap | Severity | Detail |
| --- | --- | --- |
| Demo uses `Style="height:200px;width:400px;"` but impl has no `Style` parameter | bug | `DataDisplay/Carousel/Overview/Demo.razor` line 28. Impl accepts only `Width` / `Height` as discrete params. Demo style attribute is captured by `AdditionalAttributes` splatting (no height applied as intended — spec calls this out: Carousel is zero-height without explicit height). At minimum cosmetic/visual mismatch. |
| `Template` render fragment (named slot) | missing | Spec `template.md` requires `<Template>` nested tag with `context`. Impl exposes `ItemTemplate` (`RenderFragment<object>`) as a plain parameter and `ChildContent` for static slides, but no `<Template>` element. |
| `Rebind()` method | missing | Spec `refresh-data.md` requires `Rebind()`; impl has none. |
| `Page` is 1-based per spec; impl uses 0-based `ActiveIndex` | incomplete | Naming + indexing drift: `Page`/`PageChanged` (spec) vs `ActiveIndex`/`ActiveIndexChanged` (impl). |
| Parameter naming drift: `Arrows` (spec) vs `ShowArrows` (impl), `AutomaticPageChange`/`AutomaticPageChangeInterval` (spec) vs `AutoPlay`/`IntervalMs` (impl) | incomplete | All cosmetic parity issues. |
| ARIA semantics | incomplete | Impl uses plain `<div>` wrapper. Spec `accessibility/wai-aria-support.md` requires `role="application"`, `aria-roledescription="carousel"`, `tabindex="0"`, `role="list"` on track, `role="listitem"` + `aria-roledescription="slide"` on items, and `aria-live="polite"` live region. Impl has only button `aria-label`s. |

**Paths:**
- Impl: `C:\Projects\Sunfish\packages\ui-adapters-blazor\Components\DataDisplay\SunfishCarousel.razor` (folder mismatch vs mapping category `media`)
- Demo: `C:\Projects\Sunfish\apps\kitchen-sink\Pages\Components\DataDisplay\Carousel\Overview\Demo.razor`
- Specs reviewed: `overview.md`, `events.md`, `template.md`, `refresh-data.md`, `accessibility/wai-aria-support.md`

---

## Cross-family Next Actions

1. **Fix demo compile/render bugs (highest priority):**
   - `Toolbar/Overview/Demo.razor` — rename `IsSelected`/`IsSelectedChanged` → `IsActive`/`IsActiveChanged` (or rename the impl's Toggle parameters to match the spec `Selected`/`SelectedChanged` — preferable for parity).
   - `Carousel/Overview/Demo.razor` — replace `Style="height:200px;width:400px;"` with `Height="200px" Width="400px"` to exercise the actual impl parameters.
   - `Pager/Overview/Demo.razor` — the "ASPIRATIONAL" placeholder contradicts mapping status `implemented`. Either (a) remove the placeholder and render a real `SunfishPagination` demo, or (b) downgrade the mapping to `partial` until a real demo exists. Same for `Pagination/Overview/Demo.razor`.

2. **Downgrade Pager mapping to `partial`** until real demos land and accessibility semantics (role/aria-roledescription) match spec.

3. **Resolve folder-vs-mapping drift** (one-time cleanup):
   - Stepper impl lives in `Components/Layout/Stepper/` but is a `navigation` component per mapping — either move or update mapping.
   - TreeView demos live in `Pages/Components/DataDisplay/TreeView/` but mapping says `navigation` — same choice.
   - Carousel impl is in `Components/DataDisplay/` but mapping says `media` — same choice. There is no `Media/` folder anywhere in kitchen-sink.

4. **Fill high-impact API parity gaps** in declining order of user visibility:
   - **Stepper**: add `StepType`, cancellable `OnChange`, `Valid` per step, ARIA keyboard wizard pattern. Stepper is the most spec-drifted implemented component in this slice.
   - **Menu**: add `Orientation` + inline horizontal mode; today's popup-only mode is inconsistent with the spec default.
   - **Toolbar**: add `Overflow*`, `Spacer`, `FillMode`, `Size`.
   - **Carousel**: add `<Template>` slot, 1-based `Page`, ARIA `application`/`carousel`/`slide` semantics.
   - **Breadcrumb**: add `CollapseMode`, `SeparatorIcon`, `Size`, `OnItemClick`.
   - **ContextMenu**: add flat data binding (`IdField`/`ParentIdField`/`UrlField`), `Refresh()`, content-level `Template`.

5. **Demo coverage**: most "Overview" demos exercise only the narrow declarative surface. Tier 2 should add data-bound, keyboard, and accessibility demos that the specs already reference as sibling articles.

### Tier 2 priority order (recommendation)

1. Stepper (worst API drift + validation story is core UX)
2. Pager / Pagination (status vs reality + ARIA role mismatch)
3. Menu (orientation default is architecturally off)
4. Toolbar (demo bug + overflow/spacer gaps)
5. Carousel (demo bug + ARIA gaps + naming drift)
6. Breadcrumb (collapse/separator/size gaps)
7. ContextMenu (flat data + refresh + template gaps)
8. TreeView (polish only — parameter name parity; demo coverage)

---

**Audit completeness:** 7 Navigation + 1 Media = 8 components audited, ~55 spec `.md` files reviewed across the two families. No impl or demo files modified.
