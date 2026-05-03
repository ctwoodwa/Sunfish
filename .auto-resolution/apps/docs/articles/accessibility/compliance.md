---
uid: accessibility-compliance
title: Accessibility Compliance
description: Component-by-component WCAG 2.2 compliance status, keyboard support, and WAI-ARIA implementation.
---

# Accessibility Compliance

This page provides a component-by-component summary of accessibility support in Sunfish. All components target **WCAG 2.2 Level AA**. See the [Accessibility Overview](xref:accessibility-overview) for the standards, patterns, and best practices that underpin these ratings.

## How to read this table

| Column | Values | Meaning |
|---|---|---|
| **Keyboard** | Yes | Full keyboard operability per the applicable ARIA design pattern |
| | Partial | Keyboard navigation works for core interactions; some advanced interactions (e.g., drag, canvas content) are pointer-only |
| | No | Keyboard operation not implemented |
| **WAI-ARIA** | Yes | Component emits correct roles, states, and properties per WAI-ARIA 1.2 and the ARIA APG |
| | Partial | Core ARIA semantics present; some states or live-region announcements may be incomplete |
| | No | No ARIA attributes emitted |
| **Contrast** | AA | Meets WCAG 2.2 Level AA (4.5:1 normal text, 3:1 large text and UI components) |
| | AAA | Meets WCAG 2.2 Level AAA (7:1 normal text) for default token values |

Detailed ARIA attribute tables for each component are available in the component spec documentation under `docs/component-specs/*/accessibility/wai-aria-support.md`.

---

## Buttons

| Component | Keyboard | WAI-ARIA | Contrast | Notes |
|---|---|---|---|---|
| Button | Yes | Yes | AA | Native `<button>` element; `aria-disabled` when disabled |
| IconButton | Yes | Yes | AA | Requires `AriaLabel` for icon-only usage |
| ButtonGroup | Yes | Yes | AA | `role="group"` with `aria-label` on the container |
| SplitButton | Yes | Yes | AA | Primary action and dropdown trigger are separate focusable elements; dropdown uses `role="menu"` |
| ToggleButton | Yes | Yes | AA | `aria-pressed` reflects pressed state |
| FAB | Yes | Yes | AA | Requires `AriaLabel`; positioned element does not affect tab order |
| Chip | Yes | Yes | AA | Removable chips expose a delete button with `aria-label="Remove [chip text]"` |

---

## Forms

| Component | Keyboard | WAI-ARIA | Contrast | Notes |
|---|---|---|---|---|
| TextField | Yes | Yes | AA | Native `<input>`; supports `aria-describedby` for validation messages |
| TextArea | Yes | Yes | AA | Native `<textarea>`; `aria-invalid` on validation error |
| NumericInput | Yes | Yes | AA | Increment/decrement buttons are keyboard-operable; `role="spinbutton"` with `aria-valuenow/min/max` |
| Select | Yes | Yes | AA | Native `<select>` element used where possible |
| Checkbox | Yes | Yes | AA | Native `<input type="checkbox">`; `aria-checked` for indeterminate state |
| Radio | Yes | Yes | AA | Native `<input type="radio">`; grouped in `<fieldset>`/`<legend>` |
| Switch | Yes | Yes | AA | `role="switch"` with `aria-checked` |
| Slider | Yes | Yes | AA | `role="slider"` with `aria-valuenow/min/max/step` |
| DatePicker | Yes | Yes | AA | Supports typed entry in addition to calendar picker; calendar popup uses `role="dialog"` |
| TimePicker | Yes | Yes | AA | Tumbler columns are navigable with arrow keys; typed input supported |
| ColorPicker | Partial | Yes | AA | Gradient canvas is pointer-driven; palette swatch grid is fully keyboard-navigable |
| AutoComplete | Yes | Yes | AA | `role="combobox"` with `aria-autocomplete="list"` and live listbox |
| ComboBox | Yes | Yes | AA | `role="combobox"` pattern; editable input with dropdown listbox |
| DropDownList | Yes | Yes | AA | `role="combobox"` (read-only); listbox popup |
| MultiSelect | Yes | Yes | AA | `role="combobox"` with multi-select listbox; selected items announced via `aria-selected` |
| FileUpload | Yes | Yes | AA | Drop zone announces accept/reject feedback via `aria-live`; browse button is a native `<button>` |
| SearchBox | Yes | Yes | AA | Native `<input type="search">`; clear button includes `aria-label` |

---

## Data

| Component | Keyboard | WAI-ARIA | Contrast | Notes |
|---|---|---|---|---|
| DataGrid | Yes | Yes | AA | `role="grid"` with cell navigation (arrow keys), row selection, column sorting, and filter menus; enhanced keyboard mode enables inline editing via `Enter`/`F2`/`Escape` |
| TreeView | Yes | Yes | AA | `role="tree"`/`role="treeitem"`; arrow keys expand/collapse and navigate; `aria-expanded`, `aria-selected` |
| ListView | Yes | Yes | AA | `role="listbox"` or `role="list"` depending on selection mode |
| List | Yes | Yes | AA | Semantic `<ul>`/`<li>` or `role="list"` for interactive variants |
| Table | Yes | Yes | AA | Semantic `<table>` with `<th scope>` column headers; sortable headers use `aria-sort` |
| Card | Yes | Yes | AA | Interactive cards are wrapped in a `<button>` or have `role="article"` for informational variants |

---

## Navigation

| Component | Keyboard | WAI-ARIA | Contrast | Notes |
|---|---|---|---|---|
| Menu | Yes | Yes | AA | `role="menu"` / `role="menuitem"`; arrow keys navigate, `Escape` closes, `Enter` activates |
| ContextMenu | Yes | Yes | AA | Triggered by `Shift+F10` or context menu key in addition to right-click |
| Breadcrumb | Yes | Yes | AA | `role="navigation"` with `aria-label="Breadcrumb"`; current page marked with `aria-current="page"` |
| Pagination | Yes | Yes | AA | `role="navigation"` with `aria-label`; current page has `aria-current="page"` |
| TabStrip | Yes | Yes | AA | `role="tablist"`/`role="tab"`/`role="tabpanel"`; arrow keys navigate tabs |
| Toolbar | Yes | Yes | AA | `role="toolbar"`; arrow keys navigate items within the toolbar |
| Stepper | Yes | Yes | AA | Step buttons are keyboard-operable; current step uses `aria-current="step"` |
| Wizard | Yes | Yes | AA | Navigation buttons are keyboard-operable; active step announced via `aria-current` |
| NavBar | Yes | Yes | AA | Landmark navigation using `<nav>` with `aria-label`; mobile menu uses `aria-expanded` |

---

## Layout

| Component | Keyboard | WAI-ARIA | Contrast | Notes |
|---|---|---|---|---|
| Dialog | Yes | Yes | AA | `role="dialog"` with `aria-labelledby` and `aria-describedby`; focus trapped inside while open; focus restored on close |
| Drawer | Yes | Yes | AA | `role="dialog"` or `role="complementary"` depending on usage; `aria-expanded` on trigger |
| Accordion | Yes | Yes | AA | `role="button"` on header triggers with `aria-expanded`; panels use `role="region"` with `aria-labelledby` |
| Panel | Yes | Yes | AA | Collapsible panels follow the disclosure pattern with `aria-expanded` |
| Splitter | Yes | Yes | AA | Divider handle has `role="separator"` with `aria-valuenow/min/max`; arrow keys resize panes |
| Window | Yes | Yes | AA | `role="dialog"`; title bar buttons (minimize, maximize, close) have `aria-label` |
| AppBar | Yes | Yes | AA | Landmark `<header>` with `<nav>` for navigation sections |

---

## Feedback

| Component | Keyboard | WAI-ARIA | Contrast | Notes |
|---|---|---|---|---|
| Alert | Yes | Yes | AA | `role="alert"` for urgent messages; `role="status"` for informational messages |
| Toast | Yes | Yes | AA | Rendered in an `aria-live="polite"` region; close button is keyboard-operable |
| Snackbar | Yes | Yes | AA | `aria-live="polite"`; action button is keyboard-operable |
| Tooltip | Yes | Yes | AA | Associated via `aria-describedby`; shown on both hover and keyboard focus |
| Popover | Yes | Yes | AA | `role="dialog"` for interactive popovers; `Escape` closes and returns focus to trigger |
| ProgressBar | Yes | Yes | AA | `role="progressbar"` with `aria-valuenow/min/max`; indeterminate mode uses `aria-valuetext` |
| Spinner | Yes | Yes | AA | `role="status"` with a visually hidden label announcing loading state |
| Skeleton | Yes | Yes | AA | `aria-hidden="true"` on skeleton elements; `aria-busy="true"` on the container during loading |
| Callout | Yes | Yes | AA | `role="note"` or `role="complementary"` depending on variant |

---

## Charts

| Component | Keyboard | WAI-ARIA | Contrast | Notes |
|---|---|---|---|---|
| Chart | Partial | Partial | AA | Canvas-based rendering; chart series and data points are not individually keyboard-navigable. A text summary (`aria-label` on the `<canvas>`) describes the chart. Interactive legend items are keyboard-operable. Full data table alternative recommended for AA compliance in data-critical contexts. |

---

## Scheduling

| Component | Keyboard | WAI-ARIA | Contrast | Notes |
|---|---|---|---|---|
| Gantt | Yes | Yes | AA | Task bars are keyboard-navigable; `role="grid"` structure with `aria-rowindex`/`aria-colindex`; milestone and summary task variants are announced via `aria-label` |
| AllocationScheduler | Yes | Yes | AA | Cell navigation via arrow keys; `role="grid"` with `aria-rowcount`/`aria-colcount`; inline editing triggered by `Enter`/`F2`; fill handle operation requires pointer (no keyboard alternative currently) |

---

## How to read this table

**Keyboard: Yes** means the component fully implements the keyboard interaction pattern specified by the ARIA Authoring Practices Guide for its role. A user who navigates exclusively by keyboard can perform all component functions.

**Keyboard: Partial** means keyboard navigation covers the primary use cases but one or more advanced interactions (typically drag operations or canvas-rendered content) are not yet keyboard-accessible. These gaps are tracked in the component gap log.

**WAI-ARIA: Yes** means the component emits the correct `role`, `aria-*` states, and `aria-*` properties for its ARIA design pattern, and keeps them synchronized with runtime state. Detailed attribute tables are in the per-component WAI-ARIA spec.

**WAI-ARIA: Partial** means core semantics are present but some dynamic state announcements or live-region updates may be incomplete.

**Contrast: AA** means the component meets WCAG 2.2 Level AA contrast requirements (4.5:1 for normal text, 3:1 for large text and UI components) in all standard provider themes (FluentUI, Bootstrap, Material) in both light and dark mode.

For detailed ARIA attributes, roles, and states per component, see the component spec documentation: `docs/component-specs/<ComponentName>/accessibility/wai-aria-support.md`.

## See also

- [Accessibility Overview](xref:accessibility-overview) -- keyboard patterns, ARIA strategy, focus indicators, and RTL support.
- [WCAG 2.2 Quick Reference](https://www.w3.org/WAI/WCAG22/quickref/) -- success criteria reference.
- [WAI-ARIA Authoring Practices Guide](https://www.w3.org/WAI/ARIA/apg/) -- canonical keyboard and ARIA patterns.
