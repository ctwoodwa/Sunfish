# Bridge - Solution Accelerator

> **Dual-posture since ADR 0026.** Bridge ships in two install-time postures:
>
> - **Posture A — SaaS shell (default, `Mode=SaaS`).** The generic multi-tenant
>   Blazor Server host described below. ADR 0006 framing. Runs the full
>   Aspire + Postgres + DAB + SignalR + Wolverine stack.
> - **Posture B — managed relay (`Mode=Relay`).** Paper §6.1 tier-3 peer-
>   coordination service; §17.2 sustainable-revenue SKU. Stateless,
>   kernel-sync-only, no authority semantics. See the
>   [Relay posture](#relay-posture-adr-0026) section below.
>
> See [ADR 0026](../../docs/adrs/0026-bridge-posture.md) for the decision.

Bridge is the Sunfish reference **SaaS shell accelerator**: a generic
multi-tenant platform host that composes every tier of the Sunfish stack into
a single working solution. Bridge is **not** a vertical app. Its job is shell
concerns — tenant lifecycle, subscription and edition enforcement, bundle
activation, per-tenant feature management, admin backoffice, integration
configuration, and observability. Domain work lives in `blocks-*` modules,
grouped and activated by **business-case bundles**.

Property Management is Bridge's first reference bundle. Asset Management,
Project Management, Facility Operations, and Acquisition / Underwriting
follow as equal peers. See
[ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) for the shell-vs-bundle
split and [ADR 0007](../../docs/adrs/0007-bundle-manifest-schema.md) for
bundle composition semantics. `docs/specifications/sunfish-platform-specification.md`
§6 remains the Property Management bundle specification (pending a phrasing
reconciliation pass — the technical content stands).

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

## Relay posture (ADR 0026)

Posture B — the paper §6.1 tier-3 **managed relay**. Bridge in this mode is
a stateless peer-coordination service: it accepts inbound sync-daemon
connections, runs the handshake ladder, and fan-outs `DELTA_STREAM` and
`GOSSIP_PING` frames to co-tenant peers. It has no authority semantics, no
Postgres, no DAB, no SignalR, no Razor components.

### Run in relay mode

Relay mode bypasses the Aspire AppHost entirely — the relay needs nothing
the AppHost orchestrates. Invoke the `Sunfish.Bridge` project directly:

```bash
cd accelerators/bridge
dotnet run --project Sunfish.Bridge -- --environment Relay
```

`ASPNETCORE_ENVIRONMENT=Relay` causes `appsettings.Relay.json` to layer on
top of `appsettings.json` and flip `Bridge:Mode` to `Relay`. The composition
root in `Program.cs` reads that value once at startup and wires the
`RelayWorker` hosted service plus `Sunfish.Kernel.Sync` +
`Sunfish.Kernel.Security` only — the SaaS wiring is skipped entirely.

### Relay configuration reference

`BridgeOptions.Relay` is bound from the `Bridge:Relay` configuration
section. Defaults live in `appsettings.json`; override via
`appsettings.Relay.json` or `BRIDGE__RELAY__*` environment variables.

| Key | Default | Purpose |
|---|---|---|
| `ListenEndpoint` | `null` (transport default) | Address the relay listens on. Unix-socket path on POSIX, named-pipe name on Windows. |
| `MaxConnectedNodes` | `500` | Inbound-connection cap. Extra connections get `ERROR { Code: RATE_LIMIT_EXCEEDED, Recoverable: false }`. Sizing per paper §17.2 per-relay cost model. |
| `AdvertiseHostname` | `null` | Hostname advertised via future discovery integration. |
| `AllowedTeamIds` | `[]` | Empty = accept any team. Non-empty = only peers whose agreed team-id matches one of these values are accepted; others get an ERROR frame and are disconnected. |

### Running the default SaaS posture

```bash
cd accelerators/bridge
dotnet run --project Sunfish.Bridge.AppHost
```

This starts the full Aspire orchestration (Postgres, Redis, RabbitMQ, DAB,
MockOkta) that Posture A requires. `Bridge:Mode` defaults to `SaaS`.
