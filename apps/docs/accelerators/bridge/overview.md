---
uid: accelerator-bridge-overview
title: Bridge — Overview
description: Bridge is the Sunfish reference SaaS shell accelerator — a generic multi-tenant Blazor Server host that composes the full Sunfish stack around tenant lifecycle, bundle activation, and admin backoffice concerns.
---

# Bridge — Overview

## What Bridge is

Bridge is the Sunfish reference **SaaS shell accelerator**. It is a generic
multi-tenant platform host built on Blazor Server + Blazor United (server + an
interactive client RCL), EF Core 10 on Postgres, .NET Aspire 13, and the
Sunfish UI adapter stack. Its job is the shell — tenant lifecycle,
subscription and edition enforcement, bundle activation, per-tenant feature
management, admin backoffice, integration configuration, and observability.

Bridge is deliberately **not** a vertical product. It is not a
property-management app. It is not a project-management app. Domain work
lives in `blocks-*` modules, which are grouped and activated per tenant by
**business-case bundles**.

This decision is formalized in [ADR 0006 — Bridge Is a Generic SaaS Shell,
Not a Vertical App](../../articles/adrs/). Bridge's first reference bundle is
Property Management; Asset Management, Project Management, Facility
Operations, and Acquisition / Underwriting ship as equal-peer reference
bundles against the same shell.

## When to use the Bridge accelerator

Reach for Bridge when you want to:

- Ship a **multi-tenant SaaS product** and need a working shell to build
  against — tenant context, per-tenant query filters, tenant-scoped feature
  flags, account settings, bundle catalog — without re-deriving any of it.
- **Evaluate Sunfish end-to-end** — Bridge is the reference consumer of
  `packages/foundation`, `packages/ui-core`, `packages/ui-adapters-blazor`,
  the Sunfish design providers (Fluent UI, Bootstrap, Material), and the
  `blocks-*` modules. If a Sunfish primitive exists, Bridge uses it.
- **Prototype a new `blocks-*` module** against a real host. New blocks can
  register their entities (ADR 0015) and surface into the Bridge admin shell
  without a bespoke hosting project.
- **Validate the platform story** — Aspire orchestration, Data API Builder
  (GraphQL), Wolverine + RabbitMQ messaging with a Postgres outbox, SignalR
  hubs, OpenTelemetry via ServiceDefaults — all wired and running together.

Bridge is **not** the right accelerator if you need an offline desktop app
(see [Anchor](../anchor/overview.md)), a pure component-library playground
(see the kitchen-sink app in `apps/kitchen-sink`), or a thin CLI/worker.

## Where Bridge lives

Bridge lives at `accelerators/bridge/` in the Sunfish repo and is organized
as a standalone solution:

```text
accelerators/bridge/
  Sunfish.Bridge/                  Blazor Server host project
  Sunfish.Bridge.Client/           Interactive RCL (Razor Class Library)
  Sunfish.Bridge.Data/             EF Core 10 data layer + SunfishBridgeDbContext
  Sunfish.Bridge.AppHost/          .NET Aspire orchestration
  Sunfish.Bridge.ServiceDefaults/  Shared OTel / health / resilience defaults
  Sunfish.Bridge.MigrationService/ One-shot EF migration worker
  MockOktaService/                 DEMO ONLY — mock OIDC provider
  dab-config.json                  Data API Builder schema config
  tests/                           Unit, integration, performance
  Sunfish.Bridge.slnx              Standalone solution file
```

Bridge has its own `Sunfish.Bridge.slnx` — it is **not** part of the root
`Sunfish.slnx`. It consumes Sunfish packages via relative `ProjectReference`
entries (not NuGet `PackageReference`) while both live in the same git repo.

## How Bridge maps to the Sunfish stack

| Sunfish tier | How Bridge uses it |
|---|---|
| `packages/foundation` | `ITenantContext`, `IUserNotificationService`, CSS class/style builders, local-first contracts |
| `packages/ui-core` | Framework-agnostic CSS / icon / JS-interop provider contracts and the theme service |
| `packages/ui-adapters-blazor` | Every Sunfish component — `SunfishAppShell`, the notification bell, the data grid, account shell primitives |
| `packages/ui-adapters-blazor/Providers/*` | FluentUI, Bootstrap, Material 3 — switchable at runtime via the sidebar "Design System" group |
| `packages/blocks-tenant-admin` | Tenant profile, tenant users, `BundleActivationPanel` |
| `packages/blocks-businesscases` | `IBusinessCaseService`, `EntitlementSnapshotBlock`, entitlement resolution |
| EF Core 10 / Postgres | `SunfishBridgeDbContext` with per-tenant query filters and outbox tables |
| .NET Aspire 13 | `Sunfish.Bridge.AppHost` orchestrates Postgres, Redis, RabbitMQ, Data API Builder, MockOkta |
| Data API Builder 1.7.90 | Exposes Postgres schema as GraphQL **and** as an MCP SQL server (DML tools) at `/mcp` |
| Wolverine + RabbitMQ | Messaging with a Postgres outbox for transactional consistency |
| SignalR | `BridgeHub` for real-time updates into the client RCL |

## Relationship to Anchor

Bridge and [Anchor](../anchor/overview.md) are sibling accelerators that
deliberately exercise the **same component surface** from two different
deployment shapes — Bridge as a hosted multi-tenant SaaS shell, Anchor as a
local-first on-device dashboard. If a Sunfish primitive works only in the
Bridge (hosted) shape, it is not really a primitive. Bridge is the
production target; Anchor is the conformance test.

## What Bridge does not own

Bridge does not own any business entity. If a feature references "Lease,"
"Unit," "Invoice," "Asset," "WorkOrder," or any other business concept, it
belongs in a `blocks-*` module, not in Bridge. This is the policy rule
established in ADR 0006 — PR reviewers reject business-entity leakage into
Bridge as a matter of course.

## Related ADRs

- **ADR 0006** — Bridge Is a Generic SaaS Shell, Not a Vertical App.
- **ADR 0007** — Bundle Manifest Schema (what a business-case bundle *is*).
- **ADR 0008** — Foundation.MultiTenancy (the tenant-context abstraction Bridge binds to).
- **ADR 0009** — Foundation.FeatureManagement (the feature-flag abstraction used for per-tenant entitlements).
- **ADR 0015** — Module-Entity Registration Pattern (how `blocks-*` register EF entities into the Bridge DbContext).

## Next

- [Shell Model](shell-model.md) — what the Bridge shell renders and how it composes.
- [Bundle Provisioning](bundle-provisioning.md) — how a tenant activates a bundle and what that unlocks.
- [Tenant Admin](tenant-admin.md) — the admin surface.
- [Getting Started](getting-started.md) — run Bridge locally.
