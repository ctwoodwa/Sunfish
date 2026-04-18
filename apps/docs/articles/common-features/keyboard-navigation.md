---
uid: common-features-keyboard-navigation
title: Keyboard Navigation
description: Keyboard interaction patterns across Sunfish components.
---

# Keyboard Navigation

All Sunfish interactive components support full keyboard operation. This section describes the general patterns and per-component key bindings.

## General Patterns

Sunfish follows the [ARIA Authoring Practices Guide](https://www.w3.org/WAI/ARIA/apg/) keyboard conventions throughout:

| Key | General behavior |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to the next / previous focusable element. |
| `Enter` or `Space` | Activate the focused element (button, checkbox, link). |
| `Escape` | Close the active overlay (dropdown, dialog, menu, drawer). |
| Arrow keys | Navigate within a composite widget (list, menu, grid, tree). |
| `Home` / `End` | Jump to the first / last item in a navigable list. |
| `Page Up` / `Page Down` | Move by a page in virtualized lists and grids. |

## Focus Management

Sunfish composite widgets manage focus internally so that the entire widget is a single Tab stop. Once focus enters the widget, arrow keys move between items without pressing Tab repeatedly.

This follows the "roving tabindex" pattern: the currently active item within the widget receives `tabindex="0"` while all other items receive `tabindex="-1"`. Tab leaves the widget; arrow keys stay within it.

## Focus Trap

`SunfishDialog`, `SunfishDrawer`, and `SunfishModal` trap focus within the overlay while they are open. Pressing `Tab` cycles through focusable elements inside the overlay and wraps back to the beginning rather than leaving it.

When the overlay closes, focus is returned to the element that triggered it.

## Keyboard Reference by Component

### Buttons

| Key | Action |
|---|---|
| `Enter` | Activate the button. |
| `Space` | Activate the button. |

`SunfishButtonGroup` — arrow keys move focus between buttons within the group.

### Text Inputs

Sunfish text inputs (`SunfishTextBox`, `SunfishTextArea`, `SunfishNumericTextBox`, `SunfishPasswordBox`) follow standard browser text input behavior. There are no special key bindings beyond those provided by the browser.

### Select, ComboBox, and DropDown

| Key | Action |
|---|---|
| `Enter` or `Space` | Open the dropdown. |
| `Arrow Down` | Open the dropdown or move to the next option. |
| `Arrow Up` | Move to the previous option. |
| `Home` | Move to the first option. |
| `End` | Move to the last option. |
| `Enter` | Select the focused option and close. |
| `Escape` | Close the dropdown without selecting. |
| Printable character | Move focus to the next option starting with that character. |

`SunfishMultiSelect` — `Space` toggles selection on the focused option without closing the dropdown.

### Menu and ContextMenu

| Key | Action |
|---|---|
| `Arrow Down` | Move focus to the next menu item. |
| `Arrow Up` | Move focus to the previous menu item. |
| `Arrow Right` | Open a submenu. |
| `Arrow Left` | Close the current submenu and return to the parent. |
| `Enter` | Activate the focused menu item. |
| `Escape` | Close the menu. |
| `Home` | Move focus to the first item. |
| `End` | Move focus to the last item. |

### DataGrid

| Key | Action |
|---|---|
| `Arrow keys` | Move between cells. |
| `Enter` | Begin editing the focused cell (if editable). |
| `Escape` | Cancel editing and return to navigation mode. |
| `Tab` | Move focus to the next cell; moves to the next row at the end of a row. |
| `Shift+Tab` | Move to the previous cell. |
| `Home` | Move to the first cell in the current row. |
| `End` | Move to the last cell in the current row. |
| `Ctrl+Home` | Move to the first cell in the first row. |
| `Ctrl+End` | Move to the last cell in the last row. |
| `Page Up` / `Page Down` | Scroll by one page vertically. |
| `Space` | Toggle row selection when `SelectionMode` is enabled. |

### Dialog and Drawer

| Key | Action |
|---|---|
| `Escape` | Close the dialog or drawer. |
| `Tab` | Cycle focus forward within the overlay. |
| `Shift+Tab` | Cycle focus backward within the overlay. |

Focus is trapped inside the overlay while it is open. The close button (`aria-label="Close"`) is always reachable by Tab.

### TreeView

| Key | Action |
|---|---|
| `Arrow Down` | Move focus to the next visible node. |
| `Arrow Up` | Move focus to the previous visible node. |
| `Arrow Right` | Expand a collapsed node; move into the first child if already expanded. |
| `Arrow Left` | Collapse an expanded node; move to the parent if already collapsed. |
| `Enter` | Select the focused node. |
| `Space` | Toggle selection (in multi-select mode). |
| `Home` | Move focus to the first root node. |
| `End` | Move focus to the last visible node. |

### Tabs

| Key | Action |
|---|---|
| `Arrow Left` / `Arrow Right` | Move focus between tab headers. |
| `Home` | Move focus to the first tab. |
| `End` | Move focus to the last tab. |
| `Enter` or `Space` | Activate the focused tab and display its panel. |

### Accordion

| Key | Action |
|---|---|
| `Arrow Down` | Move focus to the next accordion header. |
| `Arrow Up` | Move focus to the previous accordion header. |
| `Home` | Move focus to the first header. |
| `End` | Move focus to the last header. |
| `Enter` or `Space` | Expand or collapse the focused section. |

## Disabling Keyboard Navigation

Keyboard navigation can be disabled on individual components using the `Disabled` parameter. Disabled components are removed from the Tab order and do not respond to key events.

```razor
<SunfishButton Disabled="true">Unavailable</SunfishButton>
```

For composite components, use `ReadOnly` to allow focus and keyboard navigation while preventing data mutation.
