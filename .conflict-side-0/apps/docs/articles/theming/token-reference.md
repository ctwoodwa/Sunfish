---
uid: theming-token-reference
title: Token Reference
description: Complete reference for all --sunfish-* CSS custom properties used across Sunfish providers.
---

# Token Reference

All Sunfish providers expose a shared set of `--sunfish-*` CSS custom properties on `:root`. These tokens are the single source of truth consumed by every component's SCSS. Overriding a token at the `:root` level (or on any ancestor element) changes the appearance of all components that use it.

Providers define their own default values for each token. The tables below show the defaults shipped by the three built-in providers.

---

## Colors

### Brand colors

| Token | FluentUI default | Bootstrap default | Material 3 default |
| --- | --- | --- | --- |
| `--sunfish-color-primary` | `#0078D4` | `#0d6efd` | `#6750A4` |
| `--sunfish-color-primary-hover` | `#106EBE` | `#0b5ed7` | `#7965AF` |
| `--sunfish-color-primary-active` | `#005A9E` | `#0a58ca` | `#4F378B` |
| `--sunfish-color-primary-subtle` | `#EFF6FC` | `#cfe2ff` | `#EADDFF` |
| `--sunfish-color-secondary` | `#2B88D8` | `#6c757d` | `#625B71` |
| `--sunfish-color-secondary-hover` | `#1A6EB5` | `#5c636a` | `#7A7289` |
| `--sunfish-color-secondary-subtle` | `#EFF6FC` | `#e2e3e5` | `#E8DEF8` |

### Semantic colors

| Token | FluentUI default | Bootstrap default | Material 3 default |
| --- | --- | --- | --- |
| `--sunfish-color-success` | `#107C10` | `#198754` | `#386A20` |
| `--sunfish-color-success-subtle` | `#DFF6DD` | `#d1e7dd` | `#C3EFAD` |
| `--sunfish-color-warning` | `#FFB900` | `#ffc107` | `#6E4A00` |
| `--sunfish-color-warning-subtle` | `#FFF4CE` | `#fff3cd` | `#FFE169` |
| `--sunfish-color-danger` | `#D13438` | `#dc3545` | `#B3261E` |
| `--sunfish-color-danger-subtle` | `#FDE7E9` | `#f8d7da` | `#F9DEDC` |
| `--sunfish-color-info` | `#0078D4` | `#0dcaf0` | `#006781` |
| `--sunfish-color-info-subtle` | `#EFF6FC` | `#cff4fc` | `#B6EAFF` |

### Surface and background

| Token | FluentUI default | Bootstrap default | Material 3 default |
| --- | --- | --- | --- |
| `--sunfish-color-background` | `#FAF9F8` | `#ffffff` | `#FFFBFE` |
| `--sunfish-color-surface` | `#ffffff` | `#ffffff` | `#FFFBFE` |
| `--sunfish-color-surface-variant` | `#F3F2F1` | `#f8f9fa` | `#E7E0EC` |
| `--sunfish-color-subtle-background` | `#F5F5F5` | `#f8f9fa` | `#F4EFF4` |
| `--sunfish-color-overlay` | `rgba(0,0,0,0.4)` | `rgba(0,0,0,0.5)` | `rgba(0,0,0,0.32)` |

### Text and border

| Token | FluentUI default | Bootstrap default | Material 3 default |
| --- | --- | --- | --- |
| `--sunfish-color-text` | `#323130` | `#212529` | `#1C1B1F` |
| `--sunfish-color-text-secondary` | `#605E5C` | `#6c757d` | `#49454F` |
| `--sunfish-color-text-disabled` | `#A19F9D` | `#adb5bd` | `#1C1B1F61` |
| `--sunfish-color-text-on-primary` | `#ffffff` | `#ffffff` | `#ffffff` |
| `--sunfish-color-border` | `#D2D0CE` | `#dee2e6` | `#79747E` |
| `--sunfish-color-border-strong` | `#8A8886` | `#adb5bd` | `#49454F` |
| `--sunfish-color-disabled-background` | `#F3F2F1` | `#e9ecef` | `#E7E0EC` |
| `--sunfish-color-disabled-text` | `#A19F9D` | `#adb5bd` | `#1C1B1F61` |

---

## Typography

### Font family

| Token | FluentUI default | Bootstrap default | Material 3 default |
| --- | --- | --- | --- |
| `--sunfish-font-family` | `"Segoe UI", system-ui, sans-serif` | `system-ui, -apple-system, sans-serif` | `"Roboto", system-ui, sans-serif` |
| `--sunfish-font-family-mono` | `"Cascadia Code", "Consolas", monospace` | `"SFMono-Regular", "Consolas", monospace` | `"Roboto Mono", "Consolas", monospace` |

### Font size

| Token | Value (all providers) |
| --- | --- |
| `--sunfish-font-size-xs` | `0.75rem` (12px) |
| `--sunfish-font-size-sm` | `0.875rem` (14px) |
| `--sunfish-font-size-md` | `1rem` (16px) |
| `--sunfish-font-size-lg` | `1.125rem` (18px) |
| `--sunfish-font-size-xl` | `1.25rem` (20px) |
| `--sunfish-font-size-2xl` | `1.5rem` (24px) |
| `--sunfish-font-size-3xl` | `1.875rem` (30px) |
| `--sunfish-font-size-4xl` | `2.25rem` (36px) |

Font size values are identical across all three providers because they are defined in the shared Sunfish.Core token layer.

### Font weight

| Token | Value (all providers) |
| --- | --- |
| `--sunfish-font-weight-regular` | `400` |
| `--sunfish-font-weight-medium` | `500` |
| `--sunfish-font-weight-semibold` | `600` |
| `--sunfish-font-weight-bold` | `700` |

### Line height

| Token | Value (all providers) |
| --- | --- |
| `--sunfish-line-height-tight` | `1.25` |
| `--sunfish-line-height-normal` | `1.5` |
| `--sunfish-line-height-relaxed` | `1.75` |

---

## Spacing

The spacing scale is shared across all providers.

| Token | Value |
| --- | --- |
| `--sunfish-space-xxs` | `0.25rem` (4px) |
| `--sunfish-space-xs` | `0.5rem` (8px) |
| `--sunfish-space-sm` | `0.75rem` (12px) |
| `--sunfish-space-md` | `1rem` (16px) |
| `--sunfish-space-lg` | `1.5rem` (24px) |
| `--sunfish-space-xl` | `2rem` (32px) |
| `--sunfish-space-2xl` | `3rem` (48px) |
| `--sunfish-space-3xl` | `4rem` (64px) |

---

## Shape

| Token | FluentUI default | Bootstrap default | Material 3 default |
| --- | --- | --- | --- |
| `--sunfish-radius-sm` | `2px` | `0.25rem` | `4px` |
| `--sunfish-radius-md` | `4px` | `0.375rem` | `8px` |
| `--sunfish-radius-lg` | `8px` | `0.5rem` | `12px` |
| `--sunfish-radius-xl` | `16px` | `1rem` | `16px` |
| `--sunfish-radius-full` | `9999px` | `9999px` | `9999px` |

---

## Elevation

Box shadows encode depth. Dark mode overrides are co-located in each provider's `_elevation.scss`.

| Token | FluentUI default | Bootstrap default | Material 3 default |
| --- | --- | --- | --- |
| `--sunfish-shadow-sm` | `0 1px 2px rgba(0,0,0,0.12)` | `0 0.125rem 0.25rem rgba(0,0,0,0.075)` | `0 1px 2px rgba(0,0,0,0.3)` |
| `--sunfish-shadow-md` | `0 2px 8px rgba(0,0,0,0.14)` | `0 0.5rem 1rem rgba(0,0,0,0.15)` | `0 2px 6px rgba(0,0,0,0.15)` |
| `--sunfish-shadow-lg` | `0 4px 16px rgba(0,0,0,0.14)` | `0 1rem 3rem rgba(0,0,0,0.175)` | `0 4px 8px rgba(0,0,0,0.2)` |
| `--sunfish-shadow-xl` | `0 8px 24px rgba(0,0,0,0.18)` | `0 1rem 3rem rgba(0,0,0,0.2)` | `0 6px 10px rgba(0,0,0,0.2)` |

---

## Motion

| Token | Value (all providers) |
| --- | --- |
| `--sunfish-transition-fast` | `100ms ease` |
| `--sunfish-transition-normal` | `200ms ease` |
| `--sunfish-transition-slow` | `350ms ease` |

Use these on `transition` properties rather than hard-coding durations, so a future density or accessibility setting can adjust all transitions centrally.

---

## Z-index

| Token | Value (all providers) |
| --- | --- |
| `--sunfish-z-dropdown` | `1000` |
| `--sunfish-z-sticky` | `1020` |
| `--sunfish-z-fixed` | `1030` |
| `--sunfish-z-overlay` | `1040` |
| `--sunfish-z-modal` | `1050` |
| `--sunfish-z-popover` | `1060` |
| `--sunfish-z-tooltip` | `1070` |
| `--sunfish-z-toast` | `1080` |

---

## Focus

| Token | FluentUI default | Bootstrap default | Material 3 default |
| --- | --- | --- | --- |
| `--sunfish-focus-ring` | `0 0 0 2px #fff, 0 0 0 4px #0078D4` | `0 0 0 0.25rem rgba(13,110,253,0.25)` | `0 0 0 3px #6750A4` |
| `--sunfish-focus-ring-offset` | `2px` | `0` | `2px` |

---

## Overriding tokens

Tokens can be overridden at any CSS scope. To change the primary color for a single section without affecting the rest of the application:

```css
.my-section {
  --sunfish-color-primary: #E91E63;
  --sunfish-color-primary-hover: #C2185B;
  --sunfish-color-primary-subtle: #FCE4EC;
}
```

To override globally via `SunfishTheme`, set `SunfishColorPalette.Primary` and `SunfishThemeProvider` will emit the token to `:root` automatically. See [Theming Overview](xref:theming-overview) for the full theme API.

## See also

- [Theming Overview](xref:theming-overview) -- `SunfishTheme` and `SunfishThemeProvider`.
- [Dark Mode](xref:theming-dark-mode) -- how dark overrides are layered on top of these tokens.
- [Providers](xref:theming-providers) -- how providers implement these tokens in SCSS.
