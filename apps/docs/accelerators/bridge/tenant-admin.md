---
uid: accelerator-bridge-tenant-admin
title: Bridge — Tenant Admin
description: The Bridge tenant-admin surface — account and workspace settings pages, the SettingsSidebar layout, and where each surface maps to packages in the Sunfish stack.
keywords:
  - Bridge
  - tenant admin
  - account settings
  - workspace settings
  - SettingsSidebar
  - admin surface
---

# Bridge — Tenant Admin

## Overview

The Bridge tenant-admin surface is the collection of screens that live under
`/account/*` in the Bridge app. It is where a tenant's operator / owner
manages profile, preferences, notifications, team, billing, bundles, and
integrations. The admin surface is rendered by `AccountLayout` — see
[Shell Model](shell-model.md#settings-shell) for the chrome.

This page documents the shape of the admin surface as it ships today and
which of the screens are MVP vs. stubs. Forward-looking details live in
`accelerators/bridge/ROADMAP.md`.

## Navigation

`AccountLayout` declares the settings sidebar in two groups:

### Account

| Label | Route | Page | Status |
|---|---|---|---|
| Account | `/account/details` | `AccountDetails.razor` | MVP (form scaffold, service wiring pending) |
| Preferences | `/account/preferences` | `Preferences.razor` | MVP scaffold |
| Personalization | `/account/personalization` | `Personalization.razor` | MVP scaffold |
| Notifications | `/account/notifications` | `Notifications.razor` | MVP scaffold; shares `IUserNotificationService` with the shell bell |
| Keyboard shortcuts | `/account/shortcuts` | `Shortcuts.razor` | Scaffolded; awaits `SunfishKeyRecorder` upstream component |

### Workspace

| Label | Route | Page | Status |
|---|---|---|---|
| Team members | `/account/team` | `Team.razor` | Scaffold |
| Billing & plan | `/account/billing` | `Billing.razor` | Scaffold |
| **Bundles & entitlements** | `/account/bundles` | `Bundles.razor` | **Live** — `BundleActivationPanel` + `EntitlementSnapshotBlock` |
| Integrations | `/account/integrations` | `Integrations.razor` | Scaffold |

## What's landed today

### Bundles & entitlements — live

The headline workspace screen. Composed of two blocks from
`packages/blocks-tenant-admin` and `packages/blocks-businesscases`:

- `BundleActivationPanel` — lists registered bundles, edition pickers, and
  an **Activate** button. Shows currently active activations below.
- `EntitlementSnapshotBlock` — renders the resolved
  `TenantEntitlementSnapshot`: active bundle key, edition, modules, and
  feature-default values.

End-to-end flow is documented in
[Bundle Provisioning](bundle-provisioning.md).

### Notification pipeline — shared with the shell

The Notifications admin page reads from the **same**
`IUserNotificationService` that drives the sidebar bell — there is one
canonical store, and the bell + the settings page + any future toast
source are three projections of it.

Architecture (from `Sunfish.Bridge.Client/Notifications/`):

```text
                 ┌──────────────────────────────┐
   any source -> │  IUserNotificationService    │ <- single source of truth
                 │  (InMemoryUserNotification…) │    - CRUD + read state
                 │                              │    - dedupe via CorrelationKey
                 │                              │    - Changed event
                 └──────────────┬───────────────┘
                                │
                 ┌──────────────┴───────────────┐
                 v                              v
   NotificationFeedProjection      IUserNotificationToastForwarder
   (-> NotificationItem for bell)  (-> NotificationModel for toast)
                 │                              │
                 v                              v
        SunfishNotificationBell           ISunfishNotificationService
        (sidebar bell / inbox)           (toast / snackbar host)
```

The Notifications settings page will read the same service and render a
channel × event matrix (MVP), quiet hours, digest cadence, and per-project
overrides.

## What's scaffolded but not wired to services

The remaining admin pages have their routing, layout, and markup scaffolded
against the `SettingsPageHeader` / `SettingsSection` / `SettingsCard`
primitives, but are not yet bound to live services. They read from
hardcoded data in-page.

Planned services (see `accelerators/bridge/ROADMAP.md §6`):

- `ICurrentUserContext` / `DemoCurrentUserContext` — mocked user identity,
  plan tier, admin flag. Every plan / admin gate routes through this.
- `IAccountService` — profile CRUD, sessions, account deletion.
- `IPreferencesService` — theme mode, density, defaults, a11y flags;
  live-applies via `ISunfishThemeService`.
- `INotificationPreferencesService` — channel × event matrix, quiet hours,
  digest, per-project overrides.
- `IPersonalizationService`, `IAssistantSettingsService`,
  `IShortcutsService`, `IConnectorService` — one per remaining page.

All services are expected to land as in-memory mock implementations first,
structured so a DAB/GraphQL-backed implementation can slot in behind the
same interface.

## The `ITenantAdminService` contract

The canonical admin service lives in `packages/blocks-tenant-admin` and is
framework-agnostic:

- `ActivateBundleAsync(ActivateBundleRequest)` — activate a bundle for a
  tenant at a given edition.
- `ListActiveBundlesAsync(TenantId)` — enumerate current activations.
- `GetTenantProfileAsync(TenantId)` / `UpdateTenantProfileAsync(...)` —
  read and mutate the tenant profile.
- `InviteTenantUserAsync(InviteTenantUserRequest)` — add users to a tenant.

Entity types (`TenantProfile`, `TenantUser`, `BundleActivation`) register
themselves into the Bridge DbContext via the ADR 0015
`ISunfishEntityModule` pattern — see
`packages/blocks-tenant-admin/Data/TenantAdminEntityModule.cs`.

## Demo auth — important caveat

The admin surface renders under the demo `ITenantContext`
(`DemoTenantContext`), which returns a hardcoded `demo-tenant` /
`demo-user`. **Nothing on the admin surface enforces real authorization
yet.** `ICurrentUserContext` and plan/role gating are explicit roadmap
items.

## Related ADRs

- **ADR 0006** — what belongs in the shell vs. a bundle.
- **ADR 0007** — bundle manifest schema.
- **ADR 0008** — `ITenantContext` and multi-tenancy.
- **ADR 0015** — module-entity registration (how `blocks-tenant-admin`'s
  entities land in the Bridge DbContext).
