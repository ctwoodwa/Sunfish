# Bridge - Solution Accelerator

> **Zone-C Hybrid multi-tenant SaaS (ADR 0031).** Bridge is the paper's Zone-C
> implementation — hosted-node-as-SaaS serving multiple commercial tenants with
> per-tenant data-plane isolation. It replaces the prior "dual-posture"
> framing from ADR 0026 (now superseded). The two sibling deployment shapes
> are:
>
> - **Anchor** (`accelerators/anchor/`) — Zone A, local-first desktop.
> - **Bridge** (this accelerator) — Zone C, hybrid multi-tenant SaaS.
>
> See [ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) for
> the multi-tenant isolation model and
> [ADR 0032](../../docs/adrs/0032-multi-team-anchor-workspace-switching.md)
> for the sibling client-side multi-team Anchor model.

Bridge is the Sunfish reference **Zone-C Hybrid accelerator**: a
hosted-node-as-SaaS implementation of paper §17.2 + §20.7 Zone C. It runs
hosted-node peers (ciphertext-only, per paper §17.2) behind a traditional
web layer that handles signup, billing, and a browser-accessible shell per
tenant. Bridge composes every tier of the Sunfish stack into a single working
solution, but unlike a conventional SaaS it does **not** hold authoritative
team data — the paper's ciphertext-at-rest invariant holds across every
Bridge deployment variant. Domain work lives in `blocks-*` modules,
grouped and activated by **business-case bundles** per tenant.

Property Management is Bridge's first reference bundle. Asset Management,
Project Management, Facility Operations, and Acquisition / Underwriting
follow as equal peers. See
[ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) for the shell-vs-bundle
split and [ADR 0007](../../docs/adrs/0007-bundle-manifest-schema.md) for
bundle composition semantics. `docs/specifications/sunfish-platform-specification.md`
§6 remains the Property Management bundle specification (pending a phrasing
reconciliation pass — the technical content stands).

## Zone-C Hybrid architecture (ADR 0031)

Bridge ships as a single deployable system with three logical planes plus a
browser shell. Per-tenant isolation is the **default** posture; a
dedicated-deployment upgrade (Option B) is offered as a contractual
enterprise tier.

### Control plane (shared across all tenants)

Owns operator-facing concerns and nothing else:

- Signup, billing, subscription-tier enforcement.
- Admin backoffice, support tickets, system status.
- Tenant registry: `{tenant_id, plan, billing, support_contacts, team_public_key}`
  records only.
- Inherits today's Bridge infrastructure: Aspire orchestration, Postgres, DAB,
  SignalR, Wolverine.

The control plane holds **no team data**. `ITenantContext` resolves tenants
for control-plane concerns only; it has no authority over team data.

### Data plane (isolated per tenant)

Each tenant gets:

- A dedicated `apps/local-node-host` process.
- A dedicated SQLCipher DB at a per-tenant path.
- A subdomain: `acme.sunfish.example.com`, `globex.sunfish.example.com`.
- A hosted-node peer that participates in the tenant's gossip scope as a
  **ciphertext-only** peer (paper §17.2). It holds the tenant's event-log
  ciphertext for catch-up-on-reconnect but cannot decrypt unless the tenant
  admin explicitly issues it a role attestation.

### Relay tier (shared, stateless)

One `RelayServer` process (scaled horizontally) accepts sync-daemon transport
connections from all tenants. Per Wave 4.2 + [ADR 0029](../../docs/adrs/0029-federation-reconciliation.md),
it fans `DELTA_STREAM` / `GOSSIP_PING` frames team-id-scoped; no persistence,
so catch-up-on-reconnect traffic flows through the relay but is persisted by
each tenant's hosted-node peer rather than the relay.

### Browser shell (new, Wave 5.3)

A new Blazor Server app lives at each tenant's subdomain. The user
authenticates, the browser fetches a wrapped role-key bundle, decrypts role
keys into memory, opens a WebSocket to the tenant's hosted-node peer, and
reads/writes via CRDT ops decrypted in-browser. Session keys wipe on tab
close or logout. No persistent browser local-node in v1.

### Three tenant trust levels (chosen at signup)

| Trust level | Operator can decrypt? | Use case |
|---|---|---|
| **Relay-only** (default) | No — ciphertext only | Maximum privacy; matches paper §17.2 default |
| **Attested hosted peer** (opt-in) | Yes — admin issued attestation | Backup verification, admin-assisted recovery |
| **No hosted peer** | N/A (self-hosted) | Bridge provides only control-plane services |

### Contractual upgrade — Option B (dedicated deployment)

Offered as a paid enterprise tier. When a customer contracts for isolation,
the Bridge stack is cloned to dedicated infra (own Aspire stack, own
Postgres, own relay, own domain). Same codebase, same sync protocol —
workstations running Anchor see no change. IaC templates (Bicep / Terraform /
k8s manifests) ship in Wave 5.5.

## What Bridge demonstrates

| Tier | How Bridge uses it |
|---|---|
| `packages/foundation` | `IUserNotificationService`, `ITenantContext` (control-plane tenant registry), CSS class/style builders |
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

## Relay tier (shared stateless)

The Relay tier is the paper §6.1 tier-3 peer-coordination service that sits
between all tenants. It accepts inbound sync-daemon connections, runs the
handshake ladder, and fan-outs `DELTA_STREAM` and `GOSSIP_PING` frames
team-id-scoped to co-tenant peers. It has no authority semantics, no
persistence, no Postgres, no DAB, no SignalR, no Razor components.

### Run the relay tier standalone

The relay bypasses the Aspire AppHost entirely — it needs nothing the
AppHost orchestrates. Invoke the `Sunfish.Bridge` project directly:

```bash
cd accelerators/bridge
dotnet run --project Sunfish.Bridge -- --environment Relay
```

`ASPNETCORE_ENVIRONMENT=Relay` causes `appsettings.Relay.json` to layer on
top of `appsettings.json` and flip `Bridge:Mode` to `Relay`. The composition
root in `Program.cs` reads that value once at startup and wires the
`RelayWorker` hosted service plus `Sunfish.Kernel.Sync` +
`Sunfish.Kernel.Security` only — the control-plane wiring is skipped
entirely.

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

### Running the full control plane (default)

```bash
cd accelerators/bridge
dotnet run --project Sunfish.Bridge.AppHost
```

This starts the full Aspire orchestration (Postgres, Redis, RabbitMQ, DAB,
MockOkta) that the control plane requires. `Bridge:Mode` defaults to `SaaS`
— per-tenant data-plane orchestration (Wave 5.2) and the browser shell
(Wave 5.3) layer on top of this composition root.
