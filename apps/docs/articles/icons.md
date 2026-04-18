---
title: Icons
description: Browse all 362 hand-crafted SVG icons included with Sunfish.
---

# Sunfish Icons

362 essential icons across 18 categories. 24×24 grid, 2px stroke, round joins, `currentColor` for effortless theming.

> **Interactive demo** — For live component examples (sizing, coloring, icon-in-button, etc.), see the [Sunfish demo site](https://localhost:5301/icons).

## Installation

Install the `Sunfish.Icons` package and call `UseSunfishIcons()` during setup:

```csharp
// Program.cs
builder.Services.AddSunfish(options => options
    .UseSunfishIcons()
);
```

Include the stylesheet in your `App.razor` or `index.html`:

```html
<link rel="stylesheet" href="_content/Sunfish.Icons/css/sunfish-icons.css" />
```

Include the SVG sprite (required for icon rendering):

```html
<!-- Razor component approach — place once in App.razor / MainLayout.razor -->
<SunfishIconSprite />
```

## Basic usage

```razor
<SunfishIcon Name="search" />
```

## Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Name` | `string` | — | Icon name (e.g. `"search"`, `"download"`) |
| `Size` | `IconSize` | `Medium` | `Small` (16px), `Medium` (20px), `Large` (24px), `ExtraLarge` (32px) |
| `ThemeColor` | `IconThemeColor` | `Base` | `Primary`, `Secondary`, `Success`, `Warning`, `Danger`, `Info`, `Inherit` |
| `Flip` | `IconFlip` | `None` | `Horizontal`, `Vertical`, `Both` |
| `AriaLabel` | `string?` | `null` | Accessible label. Set when the icon is the sole content of an interactive element. |

## Common patterns

**Icon with a label** — default for most use cases:

```razor
<SunfishButton>
    <SunfishIcon Name="download" /> Export
</SunfishButton>
```

**Icon-only button** — always provide an `aria-label`:

```razor
<SunfishButton aria-label="Delete item">
    <SunfishIcon Name="trash" />
</SunfishButton>
```

**Sized icon:**

```razor
<SunfishIcon Name="settings" Size="IconSize.Large" />
```

**Themed icon:**

```razor
<SunfishIcon Name="check-circle" ThemeColor="IconThemeColor.Success" />
```

**Flipped icon:**

```razor
<SunfishIcon Name="arrow-right" Flip="IconFlip.Horizontal" />
```

## Icon browser

> Note: The interactive icon browser is planned for a future release. For now, refer to the `Sunfish.Icons` namespace API reference or the kitchen-sink icon demo.
