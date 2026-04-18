---
title: Overview
page_title: Icon Overview
description: The Sunfish Blazor Icon component renders scalable vector icons from the built-in Tabler Icons library or any custom icon provider.
slug: components/icon/overview
tags: sunfish,blazor,icon,overview
published: True
position: 0
components: ["icon"]
---

# Blazor Icon Overview

The `<SunfishIcon>` component renders SVG icons from the built-in Tabler Icons library or any registered `ISunfishIconProvider`. Icons support size, rotation, flip, animation, and theme-aware color variants.

## Creating a Blazor Icon

```razor
<SunfishIcon Name="home" />
<SunfishIcon Name="user" Size="IconSize.Large" ThemeColor="IconThemeColor.Primary" />
```

## Icon Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Name` | `string` | `""` | Name of the icon in the registered icon set (e.g., `"home"`, `"user"`). |
| `Size` | `IconSize` | `IconSize.Medium` | Display size of the icon. See [IconSize values](#iconsize-enum). |
| `ThemeColor` | `IconThemeColor` | `IconThemeColor.Inherit` | Theme-aware color applied to the icon. See [IconThemeColor values](#iconthemecolor-enum). |
| `Flip` | `IconFlip` | `IconFlip.None` | Flip transformation applied to the icon SVG. See [IconFlip values](#iconflip-enum). |
| `Rotation` | `double?` | `null` | Rotation angle in degrees (e.g., `90`, `180`, `270`). |
| `Spin` | `bool` | `false` | When `true`, a CSS spin animation is applied to the icon. |
| `Class` | `string?` | `null` | Additional CSS classes to apply to the icon element. |
| `Style` | `string?` | `null` | Inline styles to apply to the icon element. |
| `Title` | `string?` | `null` | Accessible title for the SVG (rendered as `<title>`). Recommended for non-decorative icons. |
| `AriaHidden` | `bool` | `true` | When `true`, `aria-hidden="true"` is applied. Set to `false` for standalone icons with semantic meaning. |

## IconSize Enum

Controls the rendered pixel size of the icon via CSS classes.

| Value | Description |
|-------|-------------|
| `IconSize.Small` | Small icon, suitable for inline use with text (~16 px). |
| `IconSize.Medium` | Default icon size (~20 px). |
| `IconSize.Large` | Large icon (~24 px). |
| `IconSize.ExtraLarge` | Extra-large icon for hero sections or feature highlights (~32 px or larger). |

**Example — ExtraLarge icon:**

```razor
<SunfishIcon Name="shield-check" Size="IconSize.ExtraLarge" ThemeColor="IconThemeColor.Success" />
```

## IconFlip Enum

Applies a CSS flip transformation to the SVG element.

| Value | Description |
|-------|-------------|
| `IconFlip.None` | No flip applied (default). |
| `IconFlip.Horizontal` | Flipped along the horizontal axis (left–right mirror). |
| `IconFlip.Vertical` | Flipped along the vertical axis (top–bottom mirror). |
| `IconFlip.Both` | Flipped along both axes (equivalent to 180° rotation). |

**Example — mirrored arrow icon:**

```razor
<SunfishIcon Name="arrow-right" Flip="IconFlip.Horizontal" />
```

## IconThemeColor Enum

Applies a theme-aware CSS color token to the icon.

| Value | Description |
|-------|-------------|
| `IconThemeColor.Base` | Default base color from the active theme. |
| `IconThemeColor.Primary` | Primary theme color. |
| `IconThemeColor.Secondary` | Secondary theme color. |
| `IconThemeColor.Success` | Success / positive color (green by default). |
| `IconThemeColor.Warning` | Warning / cautionary color (yellow/orange by default). |
| `IconThemeColor.Danger` | Danger / error color (red by default). Use this for error states. |
| `IconThemeColor.Info` | Informational color (blue by default). |
| `IconThemeColor.Inherit` | Inherits the color from the parent element (default). |

> **Note:** There is no `IconThemeColor.Error` value. Use `IconThemeColor.Danger` for error states.

**Example — danger icon:**

```razor
<SunfishIcon Name="alert-circle" ThemeColor="IconThemeColor.Danger" />
```

## Accessibility

- Use `Title` for icons that carry semantic meaning (e.g., standalone buttons with no visible label).
- Leave `AriaHidden="true"` (default) for decorative icons that are accompanied by a visible label.
- For icon-only interactive elements, place the descriptive label in the parent element's `aria-label`.

## See Also

- [Theming and CSS Provider](../rootcomponent/overview.md)
