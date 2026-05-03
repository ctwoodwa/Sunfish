---
uid: accelerator-bridge-getting-started
title: Bridge — Getting Started
description: Clone the Sunfish repo, launch the Bridge AppHost via .NET Aspire, verify the dashboard and the shell, and understand the first-login (demo auth) flow.
keywords:
  - Bridge
  - getting started
  - .NET Aspire
  - AppHost
  - demo auth
  - Postgres
---

# Bridge — Getting Started

## Overview

This page walks through running the Bridge accelerator locally — the
prerequisites, the commands, the dashboard URLs, and the demo-only auth
seam you land in on first launch.

Bridge runs under **.NET Aspire 13**. The `Sunfish.Bridge.AppHost`
project orchestrates Postgres, Redis, RabbitMQ, Data API Builder,
`MockOktaService`, a one-shot EF migration worker, and the Bridge web
server. A single `dotnet run` on the AppHost launches the whole stack.

## Prerequisites

- **.NET 11 SDK** (the repo's `global.json` pins the exact preview).
- **Docker Desktop** — required; Aspire uses containers for Postgres,
  Redis, RabbitMQ, and Data API Builder.
- **`dab` CLI (optional)** — if you want to validate `dab-config.json`
  outside the container. The accelerator pins DAB to `1.7.90` to match the
  CLI version.
- **Windows, macOS, or Linux** — Bridge itself is platform-agnostic. Some
  repo-wide targets (Anchor MAUI) are Windows/macOS only, but Bridge is
  not.

## Clone and restore

```bash
git clone https://github.com/<your-org>/sunfish.git
cd sunfish
dotnet restore accelerators/bridge/Sunfish.Bridge.slnx
```

Bridge has its own standalone solution (`Sunfish.Bridge.slnx`) and is
**not** part of the root `Sunfish.slnx`. Restoring the Bridge solution
also restores every Sunfish package Bridge consumes via relative
`ProjectReference`.

## Run the whole stack (Aspire AppHost)

From the repo root:

```bash
dotnet run --project accelerators/bridge/Sunfish.Bridge.AppHost
```

Or from the accelerator folder:

```bash
cd accelerators/bridge
dotnet run --project Sunfish.Bridge.AppHost
```

What starts:

| Resource | Role |
|---|---|
| `sunfishbridgedb-server` / `sunfishbridgedb` | Postgres server + Bridge database with a data volume |
| `bridge-redis` | Redis cache |
| `bridge-rabbit` | RabbitMQ with the management plugin |
| `mock-okta` | DEMO ONLY — minimal OIDC mock for local sign-in |
| `bridge-migrations` | One-shot EF migration worker. Runs once, exits. DAB and the web project `WaitForCompletion` on it |
| `bridge-dab` | Data API Builder 1.7.90 — Postgres schema exposed as GraphQL and as an MCP SQL server at `/mcp` |
| `bridge-web` | Bridge web project (Blazor Server host) |

### URLs

- **Aspire dashboard** — printed on startup as
  `Login to the dashboard at https://localhost:...`. The VS Code launch
  config (`Bridge AppHost (Aspire)`) opens this automatically.
- **Bridge web** — served from the `bridge-web` resource. Open the link in
  the Aspire dashboard; the default binding is on `https://localhost:17101`
  (see `Sunfish.Bridge.AppHost/Properties/launchSettings.json`).
- **DAB GraphQL** — mounted at the port published by the `graphql` endpoint
  on the `bridge-dab` container (visible in the dashboard).
- **DAB MCP SQL server** — `<graphql-endpoint>/mcp`, surfaced via the
  `DAB_MCP_URL` environment variable injected into the web project.

## Run from VS Code (F5)

A launch config is checked in:

- **Bridge AppHost (Aspire)** — builds the AppHost and launches it with
  the Docker container runtime preselected.

See `.vscode/launch.json` for exact settings. Commit `d2435de` added the
F5 configs for kitchen-sink, Bridge, Anchor, and the DocFX serve task.

## First-launch flow (demo auth)

Bridge boots with **demo-only** auth wiring, and the AppHost prints a
warning banner on startup:

```text
==============================================================================
  DEMO AUTH SEAM ACTIVE

  MockOktaService is registered as the OIDC provider. This is for local
  development only. Replace with real Okta / Entra ID / Auth0 configuration
  before production deployment. See accelerators/bridge/ROADMAP.md §Auth.
==============================================================================
```

What this means for you on first launch:

1. Open the Bridge web URL from the Aspire dashboard.
2. You land in the shell as **Avery Chen / avery.chen@example.com** — the
   hardcoded demo user surfaced by `DemoTenantContext`.
3. The tenant is hardcoded to `demo-tenant`.
4. Sign-out returns you to `/` and re-lands you in the same demo identity.

Replacement guidance lives in `accelerators/bridge/ROADMAP.md §Auth` —
swap `DemoTenantContext` for a real `ITenantContext` implementation and
point the OIDC configuration at Okta / Microsoft Entra ID / Auth0.

## Where to go in the UI

Once signed in, the fastest tour of what Bridge demonstrates:

- **Sidebar → Design System** — click Fluent UI / Bootstrap / Material 3 to
  see the same component tree re-skinned live (provider-first architecture).
- **Sidebar bell** — receives the canonical notification feed; try the
  "More options → Delete all read" action.
- **Account menu (sidebar footer) → Settings** or the ⌘, shortcut — opens
  the settings shell.
- **Workspace → Bundles & entitlements** (`/account/bundles`) — activate a
  bundle and watch the entitlement snapshot render. This is the headline
  platform demo.
- **Workspace → Team members / Billing / Integrations** — scaffolds; see
  [Tenant Admin](tenant-admin.md) for what's landed vs. planned.

## Build only (no run)

```bash
dotnet build accelerators/bridge/Sunfish.Bridge.slnx
```

This exercises the full Bridge solution including tests, and is the
cheapest way to verify a package change doesn't break the accelerator's
compile.

## Tests

```bash
dotnet test accelerators/bridge/tests/
```

The `tests/` tree carries unit, integration, and performance suites.
Bundle activation, tenant admin, and the canonical notification pipeline
each have focused unit tests.

## Troubleshooting

- **DAB container fails to start** — verify Docker Desktop is running and
  the `dab-config.json` path resolved by the AppHost is correct.
  `dab-config.json` lives next to `Sunfish.Bridge.slnx` and is bind-mounted
  into the container.
- **Postgres connection errors inside DAB** — DAB reads the connection
  string from `@env('ConnectionStrings__sunfishbridgedb')`, and Aspire
  injects it via `.WithReference(postgres)`. The hostname must be the
  container name (`sunfishbridgedb-server`), **not** `localhost`.
- **Migration service never completes** — check the `bridge-migrations`
  resource logs in the Aspire dashboard; the web project blocks on
  `WaitForCompletion(migrations)`.

## Next

- [Overview](overview.md) — what Bridge is and where it lives.
- [Shell Model](shell-model.md) — what the app chrome is made of.
- [Bundle Provisioning](bundle-provisioning.md) — the platform headline
  demo.
- [Tenant Admin](tenant-admin.md) — the admin surface map.
