---
uid: accelerator-bridge-shell-model
title: Bridge — Shell Model
description: How the Bridge app shell composes — app chrome, layouts, navigation, notifications, theming, and runtime-switchable design providers.
---

# Bridge — Shell Model

## Overview

The Bridge shell is a **Blazor United** composition: an ASP.NET Core host
project (`Sunfish.Bridge`) that serves a server render, and an interactive
client RCL (`Sunfish.Bridge.Client`) that owns the component tree and runs
interactive operations. A single outer chrome component,
`SunfishAppShell` (from `packages/ui-adapters-blazor`), frames the whole app.

This page documents the shell as it ships today. Forward-looking work on
account-settings pages and services is tracked in
`accelerators/bridge/ROADMAP.md`.

## Layouts

Bridge uses **two layouts**, both declared in
`Sunfish.Bridge.Client/Layout/`:

| Layout | File | Wraps | Used by |
|---|---|---|---|
| `MainLayout` | `MainLayout.razor` | Full app chrome — `SunfishAppShell` with sidebar, brand, nav groups, footer, snackbar host, notification bell | Top-level pages (Home, Board, Tasks, Timeline, Budget, Team, Risk, Account/*) |
| `AccountLayout` | `AccountLayout.razor` | Inner settings frame — left-rail settings nav (`SettingsSidebar`) plus content area. Declares `@layout MainLayout` so the outer shell renders once | All `/account/*` pages |

The nested-layout rule is deliberate: `MainLayout` is the only component
that renders `SunfishAppShell`. `AccountLayout` renders only its inner frame
so the outer shell is not double-nested.

## App chrome — `SunfishAppShell`

`MainLayout.razor` binds `SunfishAppShell` with:

- **Brand** — `SidebarBrand` content (logo + "Sunfish PM" text).
- **Sidebar nav groups** — Overview, Planning (Task Board, Task List,
  Timeline), Governance (Budget, Team Resource, Risk Register), and a
  **Design System** switcher group (Fluent UI, Bootstrap, Material 3).
- **User identity** — `SunfishAccountMenu` in the sidebar footer, with an
  avatar, name, email, sign-out handler, and an appearance toggle
  (Light / Dark / System) wired to `ISunfishThemeService`.
- **Notifications** — `SunfishNotificationBell` in the footer with an
  unread-count badge and a popout panel.
- **Snackbar host** — `SunfishSnackbarHost` mounted once in `ChildContent`
  so every page can raise toasts via `ISunfishNotificationService`.
- **Responsive collapse** — `SidebarCollapsed` is a two-way bound flag; the
  footer layout adapts between an "expanded" (avatar + name + bell row) and
  "collapsed" (stacked, centered) mode.

The shell never owns notification state. The bell's feed items
(`List<NotificationItem>`) are projected on every change from the canonical
`IUserNotificationService.All` via
`NotificationFeedProjection.ToFeedItem`. Bell clicks call back into the
service (`MarkReadAsync`, `MarkAllReadAsync`, `DeleteAllReadAsync`) — the
bell is strictly presentational.

## Runtime-switchable design providers

The sidebar exposes three design providers — **Fluent UI**, **Bootstrap**,
and **Material 3**. A `ProviderSwitcher` service (client-side) plus a small
JS module (`wwwroot/js/provider-switcher.js`) toggles provider stylesheets
at runtime and persists the choice in `localStorage`. On first render
`MainLayout` reads the stored provider and reapplies it.

This is the canonical demonstration of the provider-first architecture
documented in [Getting Started — Overview](../../articles/getting-started/overview.md):
the component tree never changes; the CSS / icon / interop providers do.

## Settings shell

`AccountLayout.razor` renders a two-column settings surface:

- **Left rail** — `SettingsSidebar` driven by a `List<SettingsNavGroup>`:
  - **Account** — Account, Preferences, Personalization, Notifications,
    Keyboard shortcuts.
  - **Workspace** — Team members, Billing & plan, Bundles & entitlements,
    Integrations.
- **Content** — the page body, wrapped in `SunfishThemeProvider` so theme
  tokens flow down.

Mobile: a scrim + toggle button opens the sidebar on small viewports;
Escape closes it.

## Authentication model — **demo only today**

Bridge boots with **demo-only** auth wiring, gated for replacement before
production:

- `DemoTenantContext` — returns a hardcoded `demo-tenant` and `demo-user`.
- `MockOktaService` — a minimal OIDC mock registered as an Aspire project
  (`mock-okta`) and injected via `WithReference` into the web project.
- Both emit a **startup warning** log. The Aspire dashboard banner echoes
  the warning.

Replacement guidance lives in `accelerators/bridge/ROADMAP.md §Auth`. The
seams (`ITenantContext` from `Sunfish.Foundation.Authorization`, the OIDC
endpoint) are deliberately shaped so a real provider (Okta, Microsoft Entra
ID, Auth0) slots in without component rework.

## Real-time updates

Bridge exposes a SignalR hub (`BridgeHub`) and wires Wolverine with a
RabbitMQ transport and a Postgres outbox. The messaging wiring is
documented alongside the Bridge AppHost — see
`Sunfish.Bridge.AppHost/Program.cs` for the `AddRabbitMQ` and
`AddPostgres` resources.

## Observability

Aspire `ServiceDefaults` (`Sunfish.Bridge.ServiceDefaults`) attach
OpenTelemetry tracing, metrics, logs, health checks, and resilience
handlers to every Bridge project. Nothing app-specific is needed at the
page level.

## Key files for orientation

| File | What it shows |
|---|---|
| `Sunfish.Bridge.Client/Layout/MainLayout.razor` | App chrome, nav, bell, footer, provider switcher wiring |
| `Sunfish.Bridge.Client/Layout/AccountLayout.razor` | Settings shell + nav groups |
| `Sunfish.Bridge/Program.cs` | Server DI, `AddInMemoryTenantAdmin()` + `AddInMemoryBusinessCases()` |
| `Sunfish.Bridge.AppHost/Program.cs` | Aspire resources (Postgres, Redis, RabbitMQ, DAB, MockOkta) |
| `Sunfish.Bridge.Client/Notifications/` | Canonical notification pipeline (service + projections) |

## Related ADRs

- **ADR 0006** — Shell scope boundary.
- **ADR 0008** — MultiTenancy; `ITenantContext` contract.
- **ADR 0017** — Web Components + Lit; the shell surface is the migration
  target so Bridge's chrome will later compose standards-based custom
  elements.
- **ADR 0022** — Canonical example catalog + docs taxonomy (these guides).
