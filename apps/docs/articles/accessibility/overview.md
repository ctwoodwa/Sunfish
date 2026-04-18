---
uid: accessibility-overview
title: Accessibility Overview
description: How Sunfish components support accessibility through WCAG compliance, WAI-ARIA attributes, keyboard navigation, and color contrast.
---

# Accessibility Overview

Accessibility is a core concern in Sunfish, not an afterthought. Every interactive component is designed with keyboard operability, screen reader support, and color contrast in mind from the start. Sunfish targets **WCAG 2.2 Level AA** compliance across all components.

## Standards

Sunfish components are developed against the following published standards:

| Standard | Version | Role |
|---|---|---|
| [WCAG](https://www.w3.org/TR/WCAG22/) | 2.2 | Conformance target (Level AA) |
| [WAI-ARIA](https://www.w3.org/TR/wai-aria-1.2/) | 1.2 | Roles, states, and properties |
| [ARIA Authoring Practices Guide](https://www.w3.org/WAI/ARIA/apg/) | Current | Keyboard interaction patterns |

Where a component maps to an established ARIA design pattern (e.g., combobox, dialog, menu, tree), the implementation follows the APG pattern exactly. Where no established pattern exists, the component applies the closest analogous pattern and documents any deviations in its per-component WAI-ARIA spec.

## Keyboard navigation

All interactive Sunfish components support full keyboard operation. Users must never be required to use a pointer device to operate any component.

### General key bindings

| Key | Behavior |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to the next or previous focusable element |
| `Arrow keys` | Navigate within a component (menu items, list items, grid cells, tabs) |
| `Enter` / `Space` | Activate the focused element (button press, menu item selection, checkbox toggle) |
| `Escape` | Close an open overlay, popup, or dialog and return focus to the trigger |
| `Home` / `End` | Jump to the first or last item within a navigable list or grid |
| `Page Up` / `Page Down` | Scroll or page through large data sets where supported |

### Component-specific patterns

Complex components follow the APG keyboard interaction patterns for their ARIA role. For example:

- **Menus** follow the `menu` role pattern: arrow keys navigate items, `Enter` activates, `Escape` closes.
- **DataGrid** follows the grid navigation pattern: arrow keys move between cells, `Enter` enters edit mode, `F2` toggles edit mode, `Escape` cancels editing.
- **TreeView** follows the `tree` role pattern: arrow keys expand/collapse nodes and move between siblings.
- **TabStrip** follows the `tablist` role pattern: arrow keys move between tabs, `Enter` or `Space` activates.
- **Dialog** traps focus within the dialog while open and restores focus to the trigger on close.
- **DatePicker** and **TimePicker** support typed entry in addition to picker navigation.

Refer to each component's WAI-ARIA spec (linked from the component documentation) for the precise key bindings it implements.

## WAI-ARIA support

Components emit appropriate ARIA roles, states, and properties on their rendered HTML. Static elements that carry semantic meaning use native HTML elements where possible (e.g., `<button>`, `<input>`, `<select>`). Where native elements are insufficient, ARIA attributes are applied to custom elements.

Common patterns across components:

- **Roles** — Every component declares the most specific applicable ARIA role (e.g., `role="combobox"`, `role="grid"`, `role="dialog"`).
- **Labels** — Components accept an `AriaLabel` parameter. When used alongside `SunfishLabel`, the label is associated via `aria-labelledby`.
- **States** — Dynamic states such as `aria-expanded`, `aria-selected`, `aria-checked`, `aria-disabled`, and `aria-busy` are kept in sync with the component's runtime state.
- **Live regions** — Components that update content asynchronously (e.g., after a data load) use `aria-live` regions to announce changes to screen readers.

Per-component ARIA attribute details are documented in each component's spec under `docs/component-specs/*/accessibility/wai-aria-support.md`. The [Compliance](xref:accessibility-compliance) page provides a high-level status summary.

## Color contrast

All Sunfish providers maintain the WCAG AA minimum contrast ratios:

- **Normal text** (below 18pt / 14pt bold): minimum 4.5:1 contrast ratio against the background.
- **Large text** (18pt or 14pt bold and above): minimum 3:1 contrast ratio.
- **UI components and graphical objects**: minimum 3:1 contrast ratio against adjacent colors.

Both light and dark theme variants are validated against these ratios. Provider token values are chosen such that semantic roles (primary, error, warning, success) meet or exceed the AA threshold in all their standard usages. AAA compliance (7:1 for normal text) is achieved for body text in default configurations but is not a blanket guarantee across all interactive states.

## Focus indicators

All focusable elements display a visible focus indicator when they receive keyboard focus. Sunfish uses the `--sunfish-focus-ring` CSS custom property to control the focus ring appearance throughout the component library. Each provider defines this token as a **2px solid outline** offset from the element boundary, ensuring the ring does not overlap element content and remains visible on all backgrounds.

Providers do not suppress the browser's native focus outline. Applications consuming Sunfish components should likewise avoid `outline: none` or `outline: 0` in their own stylesheets, as this would remove the visible focus indicator and break keyboard accessibility.

The focus ring color is derived from the provider's primary color token with sufficient contrast against both light and dark surfaces.

## Right-to-Left (RTL) layout

Sunfish supports right-to-left text direction through the `IsRtl` property on `SunfishTheme`:

```csharp
private SunfishTheme rtlTheme = new()
{
    IsRtl = true
};
```

When `IsRtl` is `true`, `SunfishThemeProvider` sets `dir="rtl"` on its root element and applies RTL layout adjustments. Components use **logical CSS properties** (`margin-inline-start`, `padding-inline-end`, `border-inline-start`, etc.) rather than physical directional properties (`left`, `right`). This ensures layout, icon placement, and reading direction mirror correctly without per-component overrides.

Components that render directional icons (e.g., expand/collapse chevrons, navigation arrows) automatically flip their orientation in RTL mode.

## Best practices for application developers

Sunfish components are accessible by default, but applications must use them correctly to preserve that accessibility.

### Use SunfishLabel with form inputs

Always associate a visible label with every form input using `SunfishLabel`. The component sets `for`/`id` associations and participates in `aria-labelledby` chaining:

```razor
<SunfishLabel For="emailInput">Email address</SunfishLabel>
<SunfishTextField Id="emailInput" @bind-Value="email" />
```

Avoid relying on `Placeholder` as the only label — placeholder text disappears on input and is not consistently announced by screen readers.

### Provide meaningful alternative text

For images and icon-only buttons, supply descriptive text via `Alt` or `AriaLabel` parameters:

```razor
<SunfishIconButton Icon="@Icons.Delete" AriaLabel="Delete item" />
```

An icon button with no accessible name is opaque to screen reader users.

### Do not suppress focus indicators

Do not apply `outline: none` to Sunfish component host elements or their descendants. The `--sunfish-focus-ring` token already produces a tasteful, on-brand focus ring. If the default ring conflicts with your design, override the token value rather than removing focus indicators entirely:

```css
:root {
    --sunfish-focus-ring: 2px solid #005fcc;
}
```

### Test with screen readers

Manual testing with at least one screen reader is recommended before shipping. Common test pairings:

- **Windows:** NVDA + Firefox, JAWS + Chrome
- **macOS / iOS:** VoiceOver + Safari
- **Android:** TalkBack + Chrome

Automated accessibility testing tools (axe, Lighthouse) catch structural issues but cannot replace screen reader testing for interaction flows.

### Avoid mouse-only interaction patterns

Do not build page-level interaction patterns that require drag-and-drop or pointer hover as the only means of performing an action. Where drag-and-drop is used (e.g., DataGrid row reordering), provide a keyboard-accessible alternative.

## See also

- [Compliance matrix](xref:accessibility-compliance) -- per-component keyboard, ARIA, and contrast status.
- [WAI-ARIA Authoring Practices Guide](https://www.w3.org/WAI/ARIA/apg/) -- reference patterns for interactive widgets.
- [WCAG 2.2 Quick Reference](https://www.w3.org/WAI/WCAG22/quickref/) -- filterable list of success criteria.
