# Wave 5.2 — Bridge Per-Tenant Data-Plane Orchestration: Decomposition Plan

**Status:** Draft | **Date:** 2026-04-23
**Source:** Plan-agent output, 2026-04-23 dispatch.

---

## 1. Current State Audit

### 1.1 `Bridge.AppHost` today

`accelerators/bridge/Sunfish.Bridge.AppHost/Program.cs` (63 lines) spawns exactly five resources via `DistributedApplication.CreateBuilder`:

| Resource | Aspire method | Notes |
|---|---|---|
| `sunfishbridgedb-server` | `AddPostgres` + `AddDatabase` | Control-plane Postgres with a data volume. |
| `bridge-redis` | `AddRedis` | Control-plane cache / SignalR backplane. |
| `bridge-rabbit` | `AddRabbitMQ().WithManagementPlugin()` | Wolverine transport. |
| `mock-okta` | `AddProject<Projects.MockOktaService>` | Demo OIDC — replace before prod. |
| `bridge-migrations` | `AddProject<Projects.Sunfish_Bridge_MigrationService>().WithReference(postgres).WaitFor(postgres)` | One-shot EF migration runner. |
| `bridge-dab` | `AddContainer("bridge-dab", "mcr.microsoft.com/azure-databases/data-api-builder", "1.7.90")` + `WithBindMount` + `WithHttpEndpoint` + `WaitForCompletion(migrations)` | DAB 1.7.90. |
| `bridge-web` | `AddProject<Projects.Sunfish_Bridge>("bridge-web").WithReference(postgres / redis / rabbit / okta).WithEnvironment("DAB_GRAPHQL_URL",…).WaitForCompletion(migrations)` | Single Blazor Server instance. |

Relevant observations:
- There is no `Sunfish.LocalNodeHost` `ProjectReference` in the AppHost `.csproj` — Bridge.AppHost cannot currently call `AddProject<Projects.Sunfish_LocalNodeHost>`. Adding the reference is a prerequisite to the `AddProject` route.
- AppHost has no loop / collection spawn today; every resource is a single named call.
- No relay resource is spawned from AppHost. Relay posture runs inside the same `bridge-web` project when `BridgeOptions.Mode == Relay`, via a separate `appsettings.Relay.json`.

### 1.2 `apps/local-node-host` config surface

`apps/local-node-host/Program.cs` (lines 29–80) binds configuration section `LocalNode` into `LocalNodeOptions` and then derives an Ed25519 root identity from a 32-byte seed loaded by `LocalNodeRootSeedReader.Read(IHostEnvironment)`.

| Config path | Shape | Default | Consumer |
|---|---|---|---|
| `LocalNode:NodeId` | string (GUID) | fresh GUID | diagnostics |
| `LocalNode:TeamId` | string (GUID?) | `null` | legacy single-team bootstrap in `MultiTeamBootstrapHostedService` |
| `LocalNode:DataDirectory` | string | platform default (`%LOCALAPPDATA%\Sunfish\LocalNode` on Windows) | every per-team registration in `AddSunfishDefaultTeamRegistrar` |
| `LocalNode:MultiTeam:Enabled` | bool | `false` | switches to multi-team mode |
| `LocalNode:MultiTeam:TeamBootstraps[].TeamId` | Guid | — | first entry becomes `IActiveTeamAccessor.Active` |
| `LocalNode:MultiTeam:TeamBootstraps[].DisplayName` | string? | `null` | forwarded to `ITeamContextFactory.GetOrCreateAsync` |
| Root seed | 32 bytes via `LocalNodeRootSeedReader` | zero-seed in Development, throws otherwise | `TeamSubkeyDerivation` + `SqlCipherKeyDerivation` |

Notably: the root seed is not read from `LocalNodeOptions` — it is read by `LocalNodeRootSeedReader.Read(builder.Environment)` which hard-throws outside `Development`. Wave 5.2 per-tenant spawn therefore either (a) runs with `ASPNETCORE_ENVIRONMENT=Development` until Wave 6.7 keystore-backed loader lands, or (b) ships a bridge-specific injection path. This is a **stop-work candidate** (see §8).

No sync-daemon transport listener is exposed by `local-node-host` today — Wave 2.1 deferred. `TeamPaths.TransportEndpoint` pins per-team Unix socket / named-pipe strings, but no code currently binds to them.

`local-node-host` does not host an HTTP endpoint today, meaning **there is no Aspire-visible health check** yet — Wave 5.2 must add one.

### 1.3 `TenantRegistry` — what Wave 5.1 shipped

`Sunfish.Bridge/Services/TenantRegistry.cs` exposes six methods:

| Method | Effect |
|---|---|
| `GetBySlugAsync(slug)` / `GetByIdAsync(id)` | Reads |
| `CreateAsync(slug, displayName, plan)` | Inserts `Pending` row, `TeamPublicKey = null` |
| `SetTeamPublicKeyAsync(id, pk)` | Pending → Active on first write |
| `UpdateTrustLevelAsync(id, level)` | Mutates `RelayOnly` / `AttestedHostedPeer` / `NoHostedPeer` |
| `ListActiveAsync()` | Lists `Status == Active` |

**Missing for 5.2:** no `SuspendAsync`, no `ResumeAsync`, no `CancelAsync` / `DeleteAsync`. The `TenantStatus` enum has `Pending | Active | Suspended | Cancelled` — transitions Suspended, Cancelled have no service-level code path yet. Wave 5.2 must land both the mutations and the side-effecting orchestration they trigger.

### 1.4 Relay team-id routing — already resolved

`Sunfish.Bridge/Relay/RelayServer.cs` (line 260) populates `ConnectedNode.TeamId` from the handshake result and a team allowlist is enforced at line 241 (`_options.AllowedTeamIds`). The cross-team fan-out guard at line 340 (`StringComparison.Ordinal` match on `peer.Node.TeamId == sender.Node.TeamId`) already isolates gossip per team at the relay.

**Implication:** the originally-suspected stop-work "how does the relay know which tenant's process to forward to?" is **not needed** — the relay fan-outs by team-id using the existing Wave 6.3.C handshake fields. What Wave 5.2 *does* need is a per-tenant hosted-node peer that dials the relay with its tenant's team-id so it can participate in the tenant's gossip mesh.

---

## 2. Target State per Lifecycle Operation

### 2.1 Create tenant

Trigger: `TenantRegistry.SetTeamPublicKeyAsync` transitions Pending → Active for the first time (ADR 0031 Wave 5.4 "founder flow" completion).

| Concern | Target |
|---|---|
| Tenant data root | `{bridge_data_root}/tenants/{TenantId:D}/` |
| Node data directory | `{bridge_data_root}/tenants/{TenantId:D}/node/` (becomes `LocalNodeOptions.DataDirectory` for the spawned child) |
| Per-tenant team-id | The team id used inside the spawned child is the `TenantId` itself (tenant ↔ team is 1:1 in Wave 5.2) |
| Spawn command | Aspire resource `tenant-node-{TenantId:D8}` |
| Health | HTTP `/health` on a per-tenant port assigned by Aspire |
| Trust-level effect | `RelayOnly` / `AttestedHostedPeer` / `NoHostedPeer` — spawn or skip accordingly |

### 2.2 Pause tenant

Trigger: operator call or billing non-payment → `TenantRegistry.SuspendAsync(tenantId)` (new).

| Concern | Target |
|---|---|
| Process | Stopped via `ITenantProcessSupervisor.PauseAsync(tenantId)` |
| Disk | Retained. No mutation of `{bridge_data_root}/tenants/{TenantId:D}/` |
| Relay | `AllowedTeamIds` filter reloads from `TenantRegistry.ListActiveAsync` on a timer; suspended tenants drop from the active list |
| Status transition | `Active → Suspended`. Idempotent |

### 2.3 Resume tenant

Trigger: billing cleared / operator unpause → `TenantRegistry.ResumeAsync(tenantId)`.

| Concern | Target |
|---|---|
| Process | Aspire resource restarted with the same spawn spec + env vars |
| Disk | Untouched (SQLCipher DB, event log, bucket manifests as pre-pause) |
| Relay | Next `ListActiveAsync` refresh re-includes the tenant in `AllowedTeamIds` |
| Status transition | `Suspended → Active`. Fails on `Cancelled` |

### 2.4 Delete tenant

Trigger: explicit operator cancel or grace-period expiry → `TenantRegistry.CancelAsync(tenantId, DeleteMode mode)`.

| Concern | `RetainCiphertext` (default) | `SecureWipe` |
|---|---|---|
| Process | Stopped | Stopped |
| Disk | Moved to `{bridge_data_root}/graveyard/{TenantId:D}/` | Recursive delete of `{bridge_data_root}/tenants/{TenantId:D}/` |
| Keystore | `SqlCipherKeyDerivation` output is derivable from seed + team id, not stored — no keystore entry to wipe for 5.2 | Same |
| Relay | Tenant drops from `AllowedTeamIds` | Same |
| Subdomain | Tenant-subdomain routing layer (Wave 5.3 prereq) consults `TenantRegistry`; `Cancelled` returns 410 Gone | Same |
| Status transition | `Active \| Suspended → Cancelled` | Same |

### 2.5 Spawn contract — env vars + paths

The per-tenant `local-node-host` child receives:

```
LocalNode__NodeId              = {deterministic GUID derived from TenantId}
LocalNode__TeamId              = {TenantId:D}
LocalNode__DataDirectory       = {bridge_data_root}/tenants/{TenantId:D}/node
LocalNode__MultiTeam__Enabled  = false
ASPNETCORE_ENVIRONMENT         = Development (until Wave 6.7 keystore-backed seed; stop-work #1)
ASPNETCORE_URLS                = http://+:{aspire-assigned-port}
```

Plus (new) `LocalNode__Relay__{Endpoint,Token,AttestationMode}` for the relay dial-out — but Wave 2.1 sync-daemon transport is a prerequisite for that to do anything (stop-work #2).

---

## 3. Sub-Task Breakdown (5 agents, DAG)

### DAG shape

```
5.2.A (BridgeOrchestrationOptions + TenantPaths + supervisor contract)
    │
    ├─▶ 5.2.B (TenantRegistry lifecycle methods: Suspend/Resume/Cancel)
    │       │
    │       └─▶ 5.2.C (TenantProcessSupervisor: Aspire child-resource orchestration)
    │                 │
    │                 └─▶ 5.2.E (AppHost integration + E2E three-tenant smoke test)
    │
    └─▶ 5.2.D (Health + monitoring: /health in local-node-host, Aspire resource-health bridge)
                  │
                  └─▶ 5.2.E
```

### 5.2.A — Orchestration options + tenant-paths helper + supervisor contract

**Scope:** Lock in string conventions, options surface, interface every downstream task consumes. No behaviour yet.

**Files touched:**
- `accelerators/bridge/Sunfish.Bridge/Orchestration/BridgeOrchestrationOptions.cs` (NEW) — `TenantDataRoot`, `MaxConcurrentTenants` (50), `RelayEndpoint` (nullable for Wave 2.1 deferral), `RelayRefreshInterval` (30s), `LocalNodeExecutablePath`.
- `accelerators/bridge/Sunfish.Bridge/Orchestration/TenantPaths.cs` (NEW) — mirrors `TeamPaths`. `TenantRoot`, `NodeDataDirectory`, `GraveyardRoot`.
- `accelerators/bridge/Sunfish.Bridge/Orchestration/ITenantProcessSupervisor.cs` (NEW) — `StartAsync`, `PauseAsync`, `ResumeAsync`, `StopAndEraseAsync`, `GetStateAsync`, `event StateChanged`.
- `accelerators/bridge/Sunfish.Bridge/Orchestration/TenantProcessState.cs` (NEW) — `Unknown | Starting | Running | Unhealthy | Paused | Cancelled | Failed`.
- `accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/Orchestration/TenantPathsTests.cs` (NEW).

**Effort:** ~4 hours.

### 5.2.B — `TenantRegistry` lifecycle methods

**Scope:** Close DB-side gap — `Suspend | Resume | Cancel` at `ITenantRegistry`.

**Files touched:**
- `TenantRegistry.cs` (EXTEND) — `SuspendAsync`, `ResumeAsync`, `CancelAsync`. Idempotent; throws on illegal transitions.
- `TenantRegistration.cs` (EXTEND) — nullable `SuspendedReason`, `CancelledAt`. **Requires EF migration.**
- `DeleteMode.cs` (NEW).
- `TenantRegistryLifecycleTests.cs` (NEW) — transition-matrix coverage.

**Risk:** Pre-existing `CreateAsync` uses pre-check + catch-DbUpdateException for in-memory provider vs Npgsql. Lifecycle methods must mirror.

**Effort:** ~6-8 hours.

### 5.2.C — `TenantProcessSupervisor` implementation

**Scope:** Spawn / stop / restart via Aspire. Consumes `ITenantRegistry` mutations via a new `ITenantRegistryEventBus`.

**Files touched:**
- `TenantProcessSupervisor.cs` (NEW) — `ConcurrentDictionary<Guid, TenantProcessHandle>`. Resource names `tenant-node-{tenantId:N}[..8]`.
- `TenantProcessHandle.cs` (NEW, internal).
- `TenantLifecycleCoordinator.cs` (NEW) — hosted service subscribing to registry mutations. Owns `RelayRefreshInterval` timer.
- `TenantRegistry.cs` (EXTEND) — publish mutation events via `ITenantRegistryEventBus`.
- `TenantProcessSupervisorTests.cs` (NEW).

**Risk:** Aspire 13.2 runtime resource-mutation capability is load-bearing (stop-work #3).

**Effort:** ~14-16 hours.

### 5.2.D — Health + monitoring surface

**Scope:** `/health` in `local-node-host` + Aspire resource-health bridge.

**Files touched:**
- `apps/local-node-host/Program.cs` (EXTEND) — embed Kestrel listener via hosted service (keeps non-web-host SDK; adds `FrameworkReference Microsoft.AspNetCore.App`).
- `LocalNodeHealthCheck.cs` (NEW) — Healthy if active team + gossip started.
- `HostedHealthEndpoint.cs` (NEW) — Kestrel + minimal-API wrapped in `IHostedService`.
- `TenantHealthMonitor.cs` (NEW) — polls every 10s; three-strike unhealthy.
- `LocalNodeHealthCheckTests.cs` (NEW).

**Effort:** ~10 hours.

### 5.2.E — AppHost integration + E2E smoke test

**Scope:** Wire supervisor + health monitor; three-tenant E2E.

**Files touched:**
- `Sunfish.Bridge.AppHost.csproj` (EXTEND) — add `ProjectReference` to `Sunfish.LocalNodeHost`.
- `Sunfish.Bridge.AppHost/Program.cs` (EXTEND) — register `TenantBootstrapResource`.
- `ThreeTenantSmokeTest.cs` (NEW) in `Sunfish.Bridge.Tests.Integration/Wave52/` — `DistributedApplicationTestingBuilder`. Create 3 tenants, assert running + health. Kill one, assert two healthy. Second case: `AppHostRestart_PreservesTenantStateAndDisk`.
- `appsettings.json` (EXTEND) — `Bridge:Orchestration:TenantDataRoot` default.
- `PLATFORM_ALIGNMENT.md` (EXTEND).

**Effort:** ~10-12 hours.

**Total effort:** ~44-50 agent-hours across 5 dispatchable chunks.

---

## 4. Aspire Integration Decision

**Decision: hybrid.** `AddProject<Projects.Sunfish_LocalNodeHost>` at AppHost boot (via `IDistributedApplicationLifecycleHook` reading `TenantRegistry.ListActiveAsync()`) for pre-existing tenants, plus direct `System.Diagnostics.Process` fallback via `ITenantProcessSupervisor` for post-boot signups.

**Rationale:** AppHost restart per tenant signup is operationally unacceptable. `AddProject` preserved for existing tenants at boot because dashboard integration + `WithReference(postgres)` env injection are genuine value. Post-boot, supervisor falls back to `Process.Start` and records endpoint in a Bridge-owned `ITenantEndpointRegistry`.

**Future path:** if Aspire 14 ships dynamic resource add, Wave 5.5 collapses the two paths.

---

## 5. Tenant Data-Dir Layout — Lock-In

**Strings live in `accelerators/bridge/Sunfish.Bridge/Orchestration/TenantPaths.cs` — static helper, mirrors `TeamPaths`.**

| Slot | Convention |
|---|---|
| Tenant root | `{TenantDataRoot}/tenants/{TenantId:D}/` |
| Node data directory | `{TenantDataRoot}/tenants/{TenantId:D}/node/` |
| SQLCipher DB (computed by child) | `{TenantDataRoot}/tenants/{TenantId:D}/node/teams/{TenantId:D}/sunfish.db` |
| Event log | `{TenantDataRoot}/tenants/{TenantId:D}/node/teams/{TenantId:D}/events/` |
| Bucket manifests | `{TenantDataRoot}/tenants/{TenantId:D}/node/teams/{TenantId:D}/buckets/` |
| Graveyard on `RetainCiphertext` | `{TenantDataRoot}/graveyard/{TenantId:D}/{cancelledAt:yyyyMMdd-HHmmss}/` |
| Default `TenantDataRoot` (Windows) | `%LOCALAPPDATA%\Sunfish\Bridge\tenants` |
| Default `TenantDataRoot` (POSIX) | `/var/lib/sunfish/bridge/tenants` |

**Note on doubly-nested `{TenantId:D}`:** outer is Bridge-scope (supervisor owns); inner is node-scope (kernel-runtime owns). They happen to share the GUID because Wave 5.2 maps tenant 1:1 to team. Future cross-tenant collab can break the symmetry without restructure.

---

## 6. Health + Monitoring

**Healthy:** HTTP 200 on `/health`, `IActiveTeamAccessor.Active is not null`, `IGossipDaemon.State == Started`.
**Degraded:** HTTP 200 but gossip not started.
**Unhealthy:** HTTP 5xx, timeout > 2s, or three consecutive polls failed.

**Observability:** Structured log scope carrying `tenant_id` on every control-plane log line; OpenTelemetry metric `bridge.tenants.running` (gauge).

---

## 7. Anti-Pattern Audit (top 3)

1. **#1 Unvalidated assumptions — HIGH:** Aspire 13.2 runtime resource-graph mutability (load-bearing). Validate before 5.2.C dispatches.
2. **#7 Delegation without contracts — MEDIUM:** `ITenantRegistryEventBus` contract must be pinned in 5.2.A, not negotiated between B and C.
3. **#15 Premature precision — MEDIUM:** `MaxConcurrentTenants = 50`, `RelayRefreshInterval = 30s`, poll 10s, three-strike rule — all tuned in Wave 5.5.

Secondary: **#6 Missing Resume Protocol:** supervisor state is in-memory; Bridge AppHost restart loses handles. 5.2.C's initial-state rebuild reads `ListActiveAsync` at boot. Test in 5.2.E.

---

## 8. Stop-Work Items (for BDFL review)

1. **Root-seed provisioning for per-tenant children.** `LocalNodeRootSeedReader.Read` throws outside Development; zero-seed in Development means every tenant child derives identical keys → breaks ciphertext-per-tenant isolation. **Recommend:** Bridge-specific `ILocalNodeSeedProvider` abstraction that synthesizes a deterministic-but-unique-per-tenant seed from a Bridge-install-level secret + TenantId. Replaces `LocalNodeRootSeedReader`.
2. **Relay dial-out is a no-op until Wave 2.1.** Per-tenant children boot, serve `/health`, but cannot gossip until sync-daemon transport lands. **Recommend:** ship 5.2 with partial E2E test (process lifecycle + health + disk isolation); track gossip convergence as 5.2.F follow-up.
3. **Aspire 13.2 dynamic-resource-graph mutability** — validate via Aspire docs / issue tracker before 5.2.C dispatches.
4. **Process-per-tenant vs container-per-tenant.** Recommend process for 5.2; container upgrade path is Wave 5.5.
5. **Subdomain → process mapping for Wave 5.3.** 5.2 ships `ITenantEndpointRegistry` (persistence-on-restart semantics). Subdomain routing middleware is 5.3.

**User candidate #3 (relay team-id routing) is RESOLVED** — `RelayServer.cs:260+340` already handles it.

---

## 9. Summary

**Decomposition shape:** 5 sub-agents in 4-tier DAG. Tier 1: 5.2.A (contracts, ~4h). Tier 2: 5.2.B (registry lifecycle, ~6-8h) ‖ 5.2.D (health, ~10h). Tier 3: 5.2.C (supervisor, ~14-16h). Tier 4: 5.2.E (AppHost + E2E, ~10-12h).

**DAG:** 5.2.A → (5.2.B ‖ 5.2.D) → 5.2.C → 5.2.E.

**Total effort:** ~44-50 agent-hours. Wave 5.1 existing tests stay green (lifecycle methods are additions, not mutations). Full Wave 5 exit criterion (three tenants gossiping) can only be partially validated until Wave 2.1 lands.
