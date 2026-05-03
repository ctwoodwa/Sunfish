---
uid: getting-started-overview
title: Overview
description: Learn what Sunfish is and how its provider-first architecture lets you swap design systems without changing component markup.
---

# Overview

Sunfish is a **provider-first Blazor component library** that ships 90+ ready-to-use UI components -- buttons, forms, data grids, dialogs, navigation, charts, and more -- while keeping the visual layer completely pluggable.

## What "provider-first" means

Most component libraries hard-code a single design language into every component. Sunfish takes a different approach: each component delegates its CSS class names, icon rendering, and JavaScript interop to a **provider**. You choose (or build) the provider that matches your design system, register it at startup, and every Sunfish component automatically renders with that look and feel.

```text
Your Razor markup
       |
  SunfishButton, SunfishCard, SunfishDialog ...
       |
  ISunfishCssProvider  /  ISunfishIconProvider  /  ISunfishJsInterop
       |
  FluentUI provider  (or your own)
```

Because the component tree and the styling layer are separate, you can:

- **Switch design systems** by swapping one NuGet package and a single line in `Program.cs`.
- **Create a custom provider** that targets your company's design tokens without forking the library.
- **Share pages and layouts** across projects that use different visual themes.

## Current providers

| Provider | Package | Design system |
| --- | --- | --- |
| Fluent UI | `Sunfish.Providers.FluentUI` | Microsoft Fluent UI 2 |
| Bootstrap | `Sunfish.Providers.Bootstrap` | Bootstrap 5.3 |
| Material 3 | `Sunfish.Providers.Material` | Material Design 3 |

See [Creating a Custom Provider](xref:theming-custom-provider) if you want to build your own.

## Next steps

- [Installation](xref:getting-started-installation) -- add the NuGet packages and register the provider.
- [First Component](xref:getting-started-first-component) -- render your first `SunfishButton`.
