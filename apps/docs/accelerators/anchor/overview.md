---
uid: accelerator-anchor-overview
title: Anchor — Overview
description: Anchor is Sunfish's local-first desktop reports and admin dashboard accelerator — the on-device counterpart to the Bridge SaaS shell, scaffolded as a .NET MAUI Blazor Hybrid app with scope deliberately deferred behind the Web Components migration.
keywords:
  - Anchor
  - accelerator
  - MAUI Blazor Hybrid
  - local-first
  - desktop
  - admin dashboard
---

# Anchor — Overview

## What Anchor is

Anchor is the Sunfish reference **local-first desktop reports and admin
dashboard** accelerator. It is the on-device counterpart to
[Bridge](../bridge/overview.md) (the multi-tenant SaaS shell). Where
Bridge is "same component surface, deployed as a hosted SaaS," Anchor is
"same component surface, deployed on-device, offline-by-default."

Anchor is a .NET **MAUI Blazor Hybrid** application. It embeds a
`BlazorWebView` into a MAUI host, so every Sunfish Razor component that
runs in Bridge also runs in Anchor — no component duplication.

Anchor exists to make one platform claim real: **local-first is a
first-class deliverable, not just a principle** (Sunfish Vision Pillar 1).
If a Sunfish primitive only works in the hosted case, it is not really
local-first. Anchor is the conformance test.

## Status — scaffolded, scope deferred

**Anchor is scaffolded. Scope is deliberately deferred.** The current
state is a solution entry, a project file, MauiProgram wiring, and a
single placeholder Home page. Landing real scope now would either:

1. Produce Blazor-only code that has to be rewritten during the
   [ADR 0017](https://github.com/) Web Components / Lit migration, or
2. Pre-commit to bundle-selection and auth-model decisions before the
   migration surfaces the right answers.

So Anchor's scope is paused until the Web Components migration reaches
the relevant milestones. The deferred deliverables are tracked in
`accelerators/anchor/README.md`; each one triggers its own intake when
its milestone lands.

## Bridge vs. Anchor — the shape comparison

|   | Bridge | Anchor |
|---|---|---|
| **Shape** | Multi-tenant SaaS shell | Single-user desktop dashboard |
| **Deployment** | Hosted (Aspire / Azure / self-hosted server) | On-device (`.msix`, `.dmg`, Mac Catalyst, iOS, Android) |
| **Target user** | End-users working inside a tenant | Owner / administrator / auditor |
| **Data locus** | Hosted Postgres, per-tenant | Local SQLite via `Sunfish.Foundation.LocalFirst` (sync opt-in) |
| **Network required** | Yes | No (offline-by-default) |
| **UI composition** | Same component surface | Same component surface |
| **Auth** | Tenant-scoped OIDC | Device-bound credentials (TBD; single-tenant mode) |

## Why MAUI Blazor Hybrid

The Anchor accelerator was built on MAUI Blazor Hybrid as its first host
because it:

1. **Reuses Bridge's Razor components verbatim** via `BlazorWebView` —
   zero component duplication.
2. **Is pure .NET** — matches the existing toolchain; contributors only
   need one stack.
3. **Is forward-compatible with Web Components** — the embedded WebView
   (Edge WebView2 on Windows, WKWebView on macOS/iOS, Chrome on Android)
   handles custom elements natively, so the ADR 0017 Web Components
   migration will land here without reshelling.
4. **Covers mobile from day one** — iPad / Android tablet as
   inspection-in-the-field surfaces are plausible futures for the
   small-landlord and small-medical-office reference verticals; paying
   for mobile as a bonus of the desktop choice is cheap insurance.

Tradeoffs accepted: larger binary size (~80MB vs. ~10MB for Tauri 2),
longer build times, MAUI workload setup overhead. These are acceptable
for a first-party desktop accelerator. A leaner alternative
(Photino.Blazor or Tauri 2) can ship as a **second** desktop accelerator
later if a specific deployment demands smaller binaries.

See [MAUI Blazor Hybrid](maui-blazor-hybrid.md) for platform-by-platform
details and the current target-framework matrix.

## Where Anchor lives

```text
accelerators/anchor/
  App.xaml, App.xaml.cs            MAUI application entry
  MainPage.xaml, MainPage.xaml.cs  Host page that frames the BlazorWebView
  MauiProgram.cs                   MAUI DI + AddMauiBlazorWebView
  Components/                      Blazor side (_Imports, Routes, Layout, Pages)
    Pages/Home.razor               Placeholder scaffolded page
    Pages/NotFound.razor
  Platforms/                       Platform-specific startup (Windows, MacCatalyst, iOS, Android)
    Windows/, MacCatalyst/, iOS/, Android/
  Resources/                       App icons, splash, fonts, raw assets
  Properties/
  Sunfish.Anchor.csproj            Single-project multi-target MAUI csproj
  README.md
```

## Deferred scope

Per `accelerators/anchor/README.md`, each of these triggers its own
intake when the relevant milestone lands:

- **LocalFirst store wiring** — register
  `Sunfish.Foundation.LocalFirst` contracts, wire embedded SQLite, expose
  export as a first-class operation (ADR 0012).
- **Bundle selection** — which bundles does Anchor compose? For the
  small-landlord reference vertical: `blocks-rent-collection`,
  `blocks-leases`, `blocks-maintenance`, `blocks-accounting`. For
  small-medical-office: TBD.
- **Report catalog** — ties to ADR 0021 (reporting pipeline policy);
  Anchor is the natural home to demo the PDF / XLSX / DOCX / PPTX / CSV
  contract-and-adapter model end-to-end.
- **Audit log surface** — read-only view over the Foundation audit log.
- **Sync toggle** — per-bundle opt-in sync UI against a federated peer
  (ADR 0013).
- **Authentication model** — device-bound credentials, optional
  passphrase, recovery mechanism. Ties to Foundation.MultiTenancy
  contracts in single-tenant mode (ADR 0008).
- **Platform packaging** — `.msix`, `.dmg`, Mac Catalyst notarization,
  App Store submission.
- **Auto-update** — delivery channel (Sparkle / MSIX AppInstaller / OSS
  alternative).
- **Crash reporting** — pre-production OTel pipeline.

## Related ADRs

- **ADR 0006** — Bridge's scope; Anchor is the complementary non-SaaS shell.
- **ADR 0012** — Foundation.LocalFirst contracts (Anchor's data layer).
- **ADR 0013** — Foundation.Integrations (federation relationship for
  optional sync).
- **ADR 0014** — Adapter parity policy (the parity Anchor's multi-platform
  reach exercises).
- **ADR 0016** — App and accelerator naming conventions
  (`Sunfish.Anchor`, flat layout).
- **ADR 0017** — Web Components / Lit technical basis (the migration
  Anchor's scope is deferred behind).
- **ADR 0021** — Reporting pipeline policy (Anchor is the natural demo
  surface).

## Next

- [MAUI Blazor Hybrid](maui-blazor-hybrid.md) — the platform story and
  current target-framework matrix.
- [Reports & Admin Scope](reports-admin-scope.md) — what the eventual
  in-app surfaces are.
- [Getting Started](getting-started.md) — build and launch Anchor today.
