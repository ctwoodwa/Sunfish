---
uid: getting-started-provider-selection
title: Choosing a Provider
description: How to choose between FluentUI, Bootstrap, and Material 3 providers for your Sunfish application.
---

# Choosing a Provider

Sunfish ships three first-party providers: **FluentUI**, **Bootstrap**, and **Material 3**. All three deliver the same 90+ components with identical APIs — your choice of provider controls the visual design system, token vocabulary, and CSS framework that backs the components.

## Decision Matrix

| Criterion | FluentUI | Bootstrap | Material 3 |
|-----------|----------|-----------|------------|
| Design system | Microsoft Fluent Design | Bootstrap / custom | Google Material Design 3 |
| Token vocabulary | `--sunfish-*` mapped from Fluent tokens | `--sunfish-*` mapped from Bootstrap variables | `--sunfish-*` mapped from M3 color roles |
| Best for | Microsoft ecosystem, Teams/Office-adjacent apps | Existing Bootstrap projects, utility-heavy layouts | Google ecosystem, consumer-facing apps |
| Font (default) | Segoe UI (system) | System font stack | Roboto (requires CDN or self-hosting) |
| Color system | Neutral palette with Fluent accent | Customizable `$primary` | M3 two-layer palette (reference + role) |
| State layers | Subtle overlay tints | Bootstrap hover/active classes | M3 state layer opacity model |
| Dark mode | `[data-sunfish-theme="dark"]` token override | `[data-sunfish-theme="dark"]` token override | `[data-sunfish-theme="dark"]` token override |
| CSS framework dependency | None (Fluent tokens only) | Bootstrap CSS required | None (M3 tokens only) |

## When to Use Each Provider

### FluentUI

Choose FluentUI when:

- You are building Microsoft-ecosystem applications — Microsoft 365, Teams extensions, or SharePoint-integrated tools.
- Your design team works with Figma kits or token sets derived from Microsoft Fluent Design.
- You want a neutral, professional look that aligns with Windows 11 and Microsoft Office visual conventions.
- You prefer a CSS-only dependency with no external framework (Fluent tokens ship as `--sunfish-*` custom properties compiled into the provider stylesheet).

FluentUI uses Segoe UI as its default typeface, which is a system font on Windows and falls back gracefully on other platforms.

### Bootstrap

Choose Bootstrap when:

- You are adding Sunfish to an existing project that already uses Bootstrap CSS.
- Your team is familiar with Bootstrap's utility classes (`d-flex`, `gap-2`, etc.) and you want to continue using them alongside Sunfish components.
- You need rapid layout composition with Bootstrap's grid and spacing utilities.
- You want rich theming via Bootstrap Sass variables (`$primary`, `$border-radius`, `$font-size-base`, etc.).

The Bootstrap provider requires Bootstrap CSS to be present in your project. Sunfish's Bootstrap bridge maps all component styles to Bootstrap tokens and follows Bootstrap's variable conventions. You can customize tokens at the Sass level before compilation.

### Material 3

Choose Material 3 when:

- You are building consumer-facing applications that follow Google's Material Design 3 guidelines.
- Your design team uses the Material 3 Figma kit or the M3 token system.
- You want the M3 state layer opacity model (overlays expressed as opacity levels over a surface, not hard-coded tints).
- You need the M3 two-layer color architecture: reference palette values (raw hues) separated from semantic role tokens (primary, on-primary, surface, etc.).

Material 3 uses Roboto as its default typeface. If you are not self-hosting Roboto, add the Google Fonts CDN link to your `App.razor`. See [Font configuration](#font-configuration) below.

## Feature Parity

All three providers expose the same component surface. There is no component available in FluentUI that is unavailable in Bootstrap or Material 3. Provider selection is a styling and token-vocabulary decision — it does not affect which components you can use or their parameters and events.

## Switching Providers

You can switch providers with two changes:

1. **`Program.cs`** — swap the `.UseFluentUI()` / `.UseBootstrap()` / `.UseMaterial()` call.
2. **`App.razor`** — swap the `<link>` tag to reference the new provider's compiled CSS.

```csharp
// Program.cs — change one line
builder.Services.AddSunfish().UseFluentUI();     // FluentUI
builder.Services.AddSunfish().UseBootstrap();    // Bootstrap
builder.Services.AddSunfish().UseMaterial();     // Material 3
```

```html
<!-- App.razor — swap the stylesheet href -->
<!-- FluentUI -->
<link rel="stylesheet" href="_content/Sunfish.Providers.FluentUI/css/sunfish-fluentui.css" />

<!-- Bootstrap (also requires Bootstrap CSS before this link) -->
<link rel="stylesheet" href="_content/Sunfish.Providers.Bootstrap/css/sunfish-bootstrap.css" />

<!-- Material 3 -->
<link rel="stylesheet" href="_content/Sunfish.Providers.Material/css/sunfish-material.css" />
```

Because all styling is expressed through `--sunfish-*` CSS custom properties and class names (no inline styles emitted from JavaScript), switching providers is safe and does not require any changes to your component markup or C# code.

## Font Configuration

### Roboto (Material 3)

Material 3 uses Roboto. Add the following link to your `<head>` in `App.razor` if you are loading from Google Fonts:

```html
<link rel="preconnect" href="https://fonts.googleapis.com" />
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
<link href="https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap" rel="stylesheet" />
```

To self-host Roboto for Content Security Policy compliance or offline scenarios, download the font files and serve them from your `wwwroot/` folder, then declare a `@font-face` rule in a local CSS file.

### Segoe UI (FluentUI) and System Fonts (Bootstrap)

Both FluentUI and Bootstrap fall back to the operating system's default sans-serif font. No CDN link is required.

## Multi-Provider and Runtime Switching

If you need to let users switch providers at runtime — for example, in a demonstration application — use the `ProviderSwitcher` pattern. See [Runtime Provider Switching](xref:theming-runtime-switching) for the full implementation guide.
