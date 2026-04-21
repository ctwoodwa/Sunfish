---
uid: accelerator-anchor-maui-blazor-hybrid
title: Anchor — MAUI Blazor Hybrid
description: How Anchor composes .NET MAUI and Blazor — the MauiProgram wiring, BlazorWebView, target-framework matrix, and per-platform caveats (Windows, macOS, iOS, Android).
---

# Anchor — MAUI Blazor Hybrid

## Overview

Anchor is a **.NET MAUI Blazor Hybrid** application. That means:

- A MAUI app shell provides the native window, the native lifecycle, and
  platform packaging.
- A `BlazorWebView` is embedded inside the MAUI UI and hosts the **same
  Razor component tree** as Bridge.
- The embedded WebView is the platform's own engine — Edge WebView2 on
  Windows, WKWebView on macOS / iOS, Chrome on Android — so modern web
  features (including custom elements after ADR 0017) work without
  additional runtime.

The architectural win: Anchor and Bridge share the component surface.
Pages, components, and Razor `.razor` files authored for Bridge run
verbatim in Anchor.

## MauiProgram wiring

The MAUI host is a minimal Blazor Hybrid bootstrap
(`accelerators/anchor/MauiProgram.cs`):

```csharp
using Microsoft.Extensions.Logging;

namespace Sunfish.Anchor;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
```

Key calls:

- `UseMauiApp<App>()` — the standard MAUI entry using `App.xaml`.
- `AddMauiBlazorWebView()` — registers the `BlazorWebView` host services
  needed for Razor components to render inside MAUI.
- `AddBlazorWebViewDeveloperTools()` — opens the DevTools / Inspector
  inside the embedded WebView in DEBUG builds. Essential when debugging
  CSS or JS interop.

## Host page

`MainPage.xaml` frames a single `<BlazorWebView>` pointed at the Blazor
`Routes.razor` component. Styling, window chrome, and native controls are
owned by MAUI; everything inside the BlazorWebView is Blazor.

## Razor side

The Blazor side lives in `accelerators/anchor/Components/`:

- `_Imports.razor`
- `Routes.razor` — the router entry.
- `Layout/` — Anchor's layouts (the on-device analogue of Bridge's
  `MainLayout`).
- `Pages/Home.razor` — the placeholder Home page that ships today.
- `Pages/NotFound.razor` — the 404.

The placeholder Home page renders an "Anchor" hero, a **Local-first ·
Offline-by-default** status pill, and a `Scope reserved` explainer
pointing at the accelerator README. When the deferred scope lands, the
page set will expand to the Reports catalog and Admin surfaces documented
in [Reports & Admin Scope](reports-admin-scope.md).

## Target-framework matrix

Anchor's `Sunfish.Anchor.csproj` multi-targets MAUI platforms, but the
set enabled **today** is conditional on the MAUI 10 preview's package
availability:

| Platform | TFM | Enabled today | Notes |
|---|---|---|---|
| Windows | `net11.0-windows10.0.19041.0` | Yes (on Windows hosts) | Primary dev target |
| Mac Catalyst | `net11.0-maccatalyst` | Yes (on non-Windows hosts) | Build on macOS |
| iOS | `net11.0-ios` | Deferred | Re-enable once MAUI 11 workload ships matching Mono runtime packs |
| Android | `net11.0-android` | Deferred | Re-enable once MAUI 11 workload ships matching Mono runtime packs |

The `.csproj` comment explains the current gate:

> MAUI 10 preview: Mono runtime packages for Android/iOS/MacCatalyst are
> not yet published at the version the workload expects, causing restore
> failures on this SDK. Multi-targeting is scaffolded but commented;
> Anchor ships Windows-only until MAUI 10 stabilizes, then re-enables
> mobile targets.

Supported minimum OS versions (when re-enabled):

- **Windows** — 10.0.17763.0
- **iOS** — 15.0
- **Mac Catalyst** — 15.0
- **Android** — 24.0 (API 24)

## XAML source generation

```xml
<MauiXamlInflator>SourceGen</MauiXamlInflator>
```

Anchor enables XAML source generation by default — `.xaml` files are
compiled to C# at build time rather than inflated at runtime. This
improves startup time and catches XAML errors at compile. To revert for a
specific file:

```xml
<MauiXaml Update="MyPage.xaml" Inflator="Default" />
```

## Per-platform notes

### Windows

- Packaging: `<WindowsPackageType>None</WindowsPackageType>` — unpackaged
  build; .msix packaging will land as a deferred deliverable.
- WebView: Edge WebView2. The runtime is present on modern Windows but
  still needs to be verified for downlevel target machines.

### Mac Catalyst

- Windows-excluded; must be built on macOS.
- Runtime identifiers: default is `maccatalyst-x64` except Release, which
  defaults to both architectures. The Mac App Store will **not** accept
  apps with only `maccatalyst-arm64`; specify both architectures or only
  `maccatalyst-x64`.
- Notarization + App Store submission are deferred deliverables.

### iOS (deferred)

- Will re-enable once the MAUI 11 workload ships matching Mono runtime
  packs.
- Target minimum 15.0.

### Android (deferred)

- Same gating as iOS.
- Target minimum API 24.

## Package warnings tolerated

`Sunfish.Anchor.csproj` suppresses:

- `CS1591` — missing XML doc warnings (Anchor is not a public API surface).
- `NU1603` / `NU1608` — the MAUI preview pins `Microsoft.AspNetCore.*` to
  older preview builds than the rest of the repo resolves. The higher
  build is forward-compatible. Revisit once MAUI 11 GA ships.

## Why MAUI Blazor Hybrid as the first host (not the only option)

Anchor ships with MAUI Blazor Hybrid because of the four reasons listed in
[Overview — Why MAUI Blazor Hybrid](overview.md#why-maui-blazor-hybrid).
The choice is **not** exclusive: a Photino.Blazor or Tauri 2 accelerator
can ship later as a sibling if a specific deployment needs a smaller
binary or a different packaging story. The component surface would be
identical — the shell wraps, not the components.

## Related ADRs

- **ADR 0014** — Adapter parity policy. The same Blazor adapter runs in
  both Bridge and Anchor; parity tests verify equivalence.
- **ADR 0017** — Web Components / Lit. When the migration lands, the
  MAUI-hosted WebView will consume the same custom elements as the
  browser-hosted Bridge — no Anchor-specific shell rework required.

## Next

- [Reports & Admin Scope](reports-admin-scope.md) — what ships inside the
  WebView once scope unfreezes.
- [Getting Started](getting-started.md) — build and launch today.
