---
uid: accelerator-anchor-getting-started
title: Anchor — Getting Started
description: Build and launch the Anchor MAUI Blazor Hybrid accelerator today — prerequisites, target-framework flags, F5 from VS Code, and the scaffolded first-launch experience.
keywords:
  - Anchor
  - MAUI Blazor Hybrid
  - getting started
  - F5 launch
  - accelerator
  - local-first
---

# Anchor — Getting Started

## Overview

This page walks through building and launching Anchor locally. Anchor is
a .NET MAUI Blazor Hybrid app; the current build targets Windows on
Windows hosts and Mac Catalyst on non-Windows hosts. iOS and Android
multi-targeting are scaffolded but commented out until the MAUI 11
workload stabilizes (see
[MAUI Blazor Hybrid](maui-blazor-hybrid.md#target-framework-matrix)).

**Reminder:** Anchor today is a **scaffolded shell** — a placeholder Home
page inside the MAUI BlazorWebView. Launching Anchor right now
demonstrates the shell wiring, not any feature work. The substantive
scope (reports, admin, LocalFirst, sync, packaging) is deliberately
deferred behind the ADR 0017 Web Components migration.

## Prerequisites

### All platforms

- **.NET 11 SDK** — the repo's `global.json` pins the exact preview.
- **MAUI workload** installed:

  ```bash
  dotnet workload install maui
  ```

### Windows (primary dev target)

- **Windows 10 version 1809 (build 17763) or later.**
- **Edge WebView2 Runtime** — preinstalled on modern Windows; confirm via
  Settings → Apps.
- **Visual Studio 2022+** or **VS Code** with the C# Dev Kit.

### macOS

- **macOS 13+** (Ventura or later recommended).
- **Xcode** with command-line tools — required for Mac Catalyst builds.
- Mac Catalyst is the only macOS target enabled today.

### iOS / Android (deferred)

Both targets are scaffolded in `Sunfish.Anchor.csproj` but **commented
out** until MAUI 11 workload ships matching Mono runtime packs. Re-enable
the `<TargetFrameworks>` entry when the workload stabilizes.

## Clone and restore

```bash
git clone https://github.com/<your-org>/sunfish.git
cd sunfish
dotnet restore accelerators/anchor/Sunfish.Anchor.csproj
```

Anchor lives in the main repo and is built as a single-project MAUI csproj.

## Build

On Windows:

```bash
dotnet build accelerators/anchor/Sunfish.Anchor.csproj -f net11.0-windows10.0.19041.0
```

On macOS:

```bash
dotnet build accelerators/anchor/Sunfish.Anchor.csproj -f net11.0-maccatalyst
```

The csproj auto-selects the TFM based on host OS, but passing `-f`
explicitly makes the output location predictable.

## Run

From the repo root, on Windows:

```bash
dotnet run --project accelerators/anchor/Sunfish.Anchor.csproj -f net11.0-windows10.0.19041.0
```

On macOS:

```bash
dotnet run --project accelerators/anchor/Sunfish.Anchor.csproj -f net11.0-maccatalyst
```

MAUI builds can take a minute on first launch while the workload
compiles platform assets.

## Run from VS Code (F5)

A launch config is checked in: **Anchor (MAUI Windows)** (see
`.vscode/launch.json`, added in commit `d2435de`). It builds the project
via the `build-anchor` pre-launch task and launches the compiled `.exe`
from the platform-specific output directory
(`bin/Debug/net11.0-windows10.0.19041.0/win-x64/Sunfish.Anchor.exe`).

On macOS, add a sibling launch config pointing at the Mac Catalyst
output directory.

## First launch

When Anchor starts, you see a native MAUI window hosting a
`BlazorWebView`. Inside the WebView, the Home page renders:

- **Anchor** hero heading.
- Subtitle: "Local-first desktop dashboard for Sunfish — reports, admin,
  and audit. Your data lives on this device."
- A **Local-first · Offline-by-default** status pill.
- A **Scope reserved** explainer pointing at the accelerator README.

That's it for today. The substantive surfaces (report catalog, admin,
LocalFirst store, sync toggle) are the deferred deliverables documented
in [Reports & Admin Scope](reports-admin-scope.md).

## Debugging Blazor inside MAUI

In DEBUG builds, `MauiProgram.cs` calls
`AddBlazorWebViewDeveloperTools()`. Right-click inside the BlazorWebView
and select **Inspect** (Windows) to open Edge DevTools against the
embedded content. The C# side (MAUI host, DI, lifecycle) is debugged via
the standard .NET debugger.

## Dependencies — what Anchor references

Anchor's `.csproj` currently takes three package references:

- `Microsoft.Maui.Controls` — the MAUI runtime.
- `Microsoft.AspNetCore.Components.WebView.Maui` — the Blazor Hybrid
  host.
- `Microsoft.Extensions.Logging.Debug` — DEBUG logging sink.

Sunfish package references (Foundation, UI Core, UI Adapters Blazor,
blocks) will land as the deferred scope unfreezes. See
`accelerators/anchor/README.md` for the deferred-scope checklist.

## Troubleshooting

- **`NU1603` / `NU1608` warnings during restore** — expected. The MAUI
  preview pins `Microsoft.AspNetCore.*` to older preview builds than the
  rest of the repo resolves. The higher build is forward-compatible.
  The csproj explicitly `<NoWarn>`s these; they surface only if you
  restore at a verbose level.
- **MacCatalyst restore fails on Windows** — expected. MacCatalyst is
  Windows-excluded. The csproj conditionally picks the TFM per host OS.
- **App window opens but WebView is blank** — verify the Edge WebView2
  Runtime is installed (Windows) or that Xcode command-line tools are
  available (macOS).
- **Debug tools missing in DEBUG builds** — confirm you're running a
  DEBUG configuration; `AddBlazorWebViewDeveloperTools()` is guarded by
  `#if DEBUG`.

## Packaging (deferred)

Packaging to `.msix` (Windows), `.dmg` (macOS), the Mac App Store, and
the App Store is explicitly deferred. Today's build uses
`<WindowsPackageType>None</WindowsPackageType>` — unpackaged. When
packaging lands, it will be tracked as its own intake against ADR 0016
(naming) and the ADR-to-come on auto-update delivery channels.

## Next

- [Overview](overview.md) — what Anchor is.
- [MAUI Blazor Hybrid](maui-blazor-hybrid.md) — the platform story.
- [Reports & Admin Scope](reports-admin-scope.md) — what ships in the
  WebView once scope unfreezes.
