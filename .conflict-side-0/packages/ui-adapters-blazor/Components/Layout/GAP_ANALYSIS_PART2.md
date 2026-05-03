# Gap Analysis Part 2 — Layout Components (Panel through TabStrip)

## 1. SunfishPanel

**Spec:** PanelBar — data-driven accordion with `Data` binding, `ExpandedItems`, `ExpandMode`, hierarchical items, templates, icons, navigation, events, `Rebind()` method.

**Current:** Renders a plain `<div>` with `ChildContent`. No data binding, no expand/collapse, no items, no events.

| Gap | Severity |
|-----|----------|
| No `Data` property or item model binding | **[High]** |
| No expand/collapse behavior (`ExpandedItems`, `ExpandMode`) | **[High]** |
| No header/content template support | **[High]** |
| No hierarchical item rendering | **[High]** |
| No icons or navigation support | **[Medium]** |
| No events (item selection, expand/collapse) | **[Medium]** |
| No `Rebind()` method | **[Low]** |

---

## 2. SunfishRow

**Spec:** No standalone spec. Appears to be a simple layout primitive.

**Current:** Renders a `<div>` with CSS class from `CssProvider.RowClass()` and `ChildContent`.

| Gap | Severity |
|-----|----------|
| No spec to compare against — likely feature-complete as a simple layout wrapper | **[Low]** |

---

## 3. SunfishSplitter

**Spec:** Multiple panes via `<SplitterPane>` children, per-pane `Size`/`Min`/`Max`/`Collapsible`/`Resizable`, `Width`/`Height`, `Orientation`, `AriaLabel`, state management (`GetState`/`SetState`), events, collapse/resize interaction. Reference APIs (Toolbelt, Radzen, Blazorise) additionally expose percentage-based responsive sizing, touch/pointer support, nested splitter layouts, CSS custom properties for bar sizing, and lifecycle events (`OnResizeStart`/`OnResizeEnd`/`OnExpand`).

**Current:** N-pane support via `SunfishSplitterPane` child components. Interactive mouse + pointer drag-to-resize with JS interop for accurate container measurement. Percent sizes preserved during drag (percent↔pixel conversion via `getContainerSize` JS call). Keyboard resize (arrow keys, Shift for larger steps). Collapse/expand via double-click, collapse button, or Enter key. Per-pane `Min`/`Max` constraints, `Collapsible`, `Resizable` flags. `Width`/`Height` parameters. `AriaLabel`, `aria-valuenow/min/max`. State management via `GetState()`/`SetState()`. Events: `OnResize`, `OnResizeStart`, `OnResizeEnd`, `OnCollapse`, `OnExpand`. CSS custom property `--mar-splitter-bar-size`. Touch support via `touch-action: none` and pointer event listeners. Vertical orientation collapse arrows (▲/▼). Legacy 2-pane backwards compat retained.

| Gap | Severity |
|-----|----------|
| ~~Only supports 2 panes~~ **Resolved** — N panes via `SunfishSplitterPane` | ~~High~~ **Done** |
| ~~No interactive resize~~ **Resolved** — mouse drag + pointer events + keyboard | ~~High~~ **Done** |
| ~~No collapse/expand~~ **Resolved** — button, double-click, Enter key | ~~High~~ **Done** |
| ~~No Min/Max constraints~~ **Resolved** — per-pane `Min`/`Max` with pixel + percent | ~~High~~ **Done** |
| ~~No Width/Height~~ **Resolved** | ~~Medium~~ **Done** |
| ~~No state management~~ **Resolved** — `GetState()`/`SetState()` | ~~Medium~~ **Done** |
| ~~No events~~ **Resolved** — `OnResize`, `OnResizeStart`, `OnResizeEnd`, `OnCollapse`, `OnExpand` | ~~Medium~~ **Done** |
| ~~No AriaLabel~~ **Resolved** | ~~Low~~ **Done** |
| No `PersistKey` for automatic localStorage state persistence | **[Low]** |
| No `ResizeObserver` for container-resize recalculation when window resizes | **[Low]** |
| Uses `StackDirection` enum instead of `SplitterOrientation` (acceptable — shared enum) | **[Low]** |

---

## 4. SunfishStack

**Spec:** `Orientation` (default Horizontal), `Spacing`, `HorizontalAlign`, `VerticalAlign`, `Width`, `Height`.

**Current:** Has `Direction` (default Vertical) and `Alignment` (single enum). No `Spacing`, `Width`, `Height`, or separate horizontal/vertical alignment.

| Gap | Severity |
|-----|----------|
| No `Spacing` parameter | **[High]** |
| No `Width`/`Height` parameters | **[Medium]** |
| No separate `HorizontalAlign`/`VerticalAlign` — uses single `StackAlignment` enum | **[Medium]** |
| Default orientation differs (Vertical vs spec's Horizontal) | **[Low]** |
| Parameter named `Direction` instead of `Orientation` | **[Low]** |

---

## 5. SunfishStep

**Spec:** Part of Stepper. Spec defines `StepperStep` with `Icon`, `Label`, `Disabled`, `Optional`, `Valid`, custom template support.

**Current:** Has only `Title` and `ChildContent`. Registers/unregisters with parent stepper.

| Gap | Severity |
|-----|----------|
| No `Icon` parameter | **[High]** |
| No `Label` parameter (uses `Title` instead — naming mismatch) | **[Low]** |
| No `Disabled` parameter | **[Medium]** |
| No `Optional` or validation (`Valid`) support | **[Medium]** |
| No step template support | **[Medium]** |

---

## 6. SunfishStepper

**Spec:** `Value` (current step index with two-way binding), `Orientation`, `Linear` flow, `StepType` (display mode), step validation, templates.

**Current:** Has `ActiveStep` with `ActiveStepChanged`. Renders step indicators with checkmark for completed. No orientation, no linear flow, no display modes.

| Gap | Severity |
|-----|----------|
| No `Orientation` parameter (horizontal/vertical) | **[High]** |
| No `Linear` flow enforcement | **[Medium]** |
| No `StepType` / display mode (labels vs icons vs both) | **[Medium]** |
| No step validation integration | **[Medium]** |
| No step template support | **[Medium]** |
| Step indicators not clickable (no navigation) | **[Medium]** |
| Parameter named `ActiveStep` instead of `Value` | **[Low]** |

---

## 7. SunfishTabStrip

**Spec:** `ActiveTabIndex`/`ActiveTabId`, `TabPosition`, `TabAlignment`, `Size`, `PersistTabContent`, `EnableTabReorder`, `OverflowMode`, `ScrollButtonsPosition`/`Visibility`, `Width`/`Height`, `TabStripSuffixTemplate`, state events, `GetState`/`SetState`/`Refresh`.

**Current:** Implements nearly all spec parameters including `ActiveTabId`, `ActiveTabIndex` (obsolete), `TabPosition`, `TabAlignment`, `Size`, `PersistTabContent`, `EnableTabReorder`, `OverflowMode` with menu, `ScrollButtonsPosition`/`Visibility`, `Width`/`Height`, `TabStripSuffixTemplate`, `OnStateInit`/`OnStateChanged`/`OnTabReorder`, `GetState`/`SetState`/`Refresh`.

| Gap | Severity |
|-----|----------|
| `EnableTabReorder` parameter exists but no drag-and-drop implementation in markup | **[High]** |
| Scroll overflow mode declared but no scroll button rendering in template | **[Medium]** |
| No keyboard navigation (arrow keys between tabs) | **[Medium]** |
| Overflow menu uses inline styles instead of CSS classes | **[Low]** |

---

## 8. TabStripTab

**Spec:** `Title`, `Visible`, `Disabled`, `Closeable`, `Pinnable`, `Pinned`, `HeaderTemplate`, `Content`.

**Current:** Has `Id`, `Title`, `Visible`/`VisibleChanged`, `Disabled`, `Closeable`, `Pinnable`, `Pinned`/`PinnedChanged`, `HeaderTemplate`, `Content`, `ChildContent`.

| Gap | Severity |
|-----|----------|
| No context menu for pin/unpin action | **[Medium]** |
| Component is largely feature-complete relative to spec | — |

---

## Summary

| Component | Overall Gap | Key Issue |
|-----------|------------|-----------|
| SunfishPanel | **Critical** | Placeholder div; entire PanelBar feature set missing |
| SunfishRow | **None** | No spec; appears complete |
| SunfishSplitter | **Low** | Fully interactive N-pane splitter; only PersistKey and ResizeObserver remain |
| SunfishStack | **Medium** | Missing Spacing, Width/Height, split alignment |
| SunfishStep | **Medium** | Missing Icon, Disabled, validation |
| SunfishStepper | **High** | Missing orientation, linear flow, clickable steps |
| SunfishTabStrip | **Low** | Mostly complete; drag reorder not wired up |
| TabStripTab | **Low** | Mostly complete; missing pin context menu |
