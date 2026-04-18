# Bridge - Solution Accelerator

Bridge is the reference Sunfish solution accelerator: a full-stack
project-management app that composes every tier of the Sunfish stack into a
single working solution. It is also the **property-management vertical
reference implementation** per `docs/specifications/sunfish-platform-specification.md`.

## What Bridge demonstrates

| Tier | How Bridge uses it |
|---|---|
| `packages/foundation` | `IUserNotificationService`, `ITenantContext`, CSS class/style builders |
| `packages/ui-core` | Framework-agnostic contracts (CSS/icon providers, theme service) |
| `packages/ui-adapters-blazor` | Sunfish Blazor components; shell; AppShell; bell; grid |
| `packages/ui-adapters-blazor/Providers/*` | FluentUI, Bootstrap, Material runtime-switchable design providers |
| EF Core 10 / Postgres | `SunfishBridgeDbContext` with per-tenant query filters |
| .NET Aspire 13 | `Sunfish.Bridge.AppHost` orchestrates Postgres, Redis, RabbitMQ, DAB, MockOkta |
| Data API Builder | Postgres schema -> GraphQL via `dab-config.json` |
| SignalR + Wolverine | `BridgeHub` for real-time updates; Wolverine RabbitMQ transport with Postgres outbox |

## Running Bridge locally

```bash
cd accelerators/bridge
dotnet run --project Sunfish.Bridge.AppHost
```

Aspire dashboard -> https://localhost:17123 (port from `launchSettings.json`).

## Demo auth - IMPORTANT

Bridge boots with **demo-only** auth wiring:

- `DemoTenantContext` returns a hardcoded `demo-tenant` / `demo-user`.
- `MockOktaService` provides a minimal OIDC mock.

Both emit a **startup warning** log. See [ROADMAP.md](ROADMAP.md) and
[PLATFORM_ALIGNMENT.md](PLATFORM_ALIGNMENT.md) for replacement guidance before
production deployment.

## Roadmap

- [ROADMAP.md](ROADMAP.md) - settings/account pages and feature work in
  progress or planned.
- [PLATFORM_ALIGNMENT.md](PLATFORM_ALIGNMENT.md) - inventory of current Bridge
  adoption vs the Sunfish platform specification (kernel primitives,
  decentralization, federation).

## Structure

```
accelerators/bridge/
  Sunfish.Bridge/                      Server (Blazor Server host)
  Sunfish.Bridge.Client/               Client RCL (interactive components)
  Sunfish.Bridge.Data/                 EF Core data layer
  Sunfish.Bridge.AppHost/              Aspire orchestration
  Sunfish.Bridge.ServiceDefaults/      Aspire shared defaults (OTEL, health, resilience)
  Sunfish.Bridge.MigrationService/     One-shot EF migration worker
  MockOktaService/                     DEMO ONLY - mock OIDC provider
  dab-config.json                      DAB GraphQL schema config
  tests/                               Unit, integration, performance
```

## Standalone solution

Bridge has its own `Sunfish.Bridge.slnx` - it is **not** part of the root
`Sunfish.slnx`. Run `dotnet build Sunfish.Bridge.slnx` from
`accelerators/bridge/` to build only the accelerator and its Sunfish package
dependencies. Bridge consumes Sunfish packages via relative ProjectReference
(not NuGet PackageReference) while both live in the same git repo.
