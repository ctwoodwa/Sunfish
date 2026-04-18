---
title: ResizableContainer Accessibility
page_title: SunfishResizableContainer Accessibility
description: Accessibility features and keyboard interactions for the ResizableContainer.
slug: resizable-container-accessibility
tags: resizable-container, accessibility, a11y
published: true
position: 4
---

# Accessibility

## Handle Element

The resize handle is a `<button>` element, providing:
- Native keyboard focusability
- Implicit `button` role
- Screen reader announcement via `aria-label`

## ARIA Label

Configure the handle's accessible label with `HandleAriaLabel`:

```razor
<SunfishResizableContainer HandleAriaLabel="Resize editor panel">
    <p>Editor content</p>
</SunfishResizableContainer>
```

Default label is "Resize" if `HandleAriaLabel` is not set.

## Keyboard Interactions

When `KeyboardResizeEnabled` is true (default):

| Key | Action |
|-----|--------|
| Tab | Focus the resize handle |
| Arrow Right | Increase width by 4px |
| Arrow Left | Decrease width by 4px |
| Arrow Down | Increase height by 4px |
| Arrow Up | Decrease height by 4px |
| Shift + Arrow Right | Increase width by 20px |
| Shift + Arrow Left | Decrease width by 20px |
| Shift + Arrow Down | Increase height by 20px |
| Shift + Arrow Up | Decrease height by 20px |

## Focus Behavior

- Handle receives visible focus ring when focused via Tab or programmatically
- Focus does NOT get trapped — Tab moves to the next focusable element
- Focus ring styling follows the active theme provider (FluentUI or Bootstrap)

## Reduced Motion

The component respects `prefers-reduced-motion: reduce`. When active, handle transition animations are disabled.

## Screen Reader Notes

- The handle announces its purpose via aria-label
- No live region is used for size changes (would be noisy during drag)
- Programmatic focus is available via `FocusHandleAsync()` for custom workflows

## Limitations

- Keyboard resize fires discrete start/end events (not continuous like pointer drag)
- Ghost outline mode is visual-only and does not affect screen reader experience
