# Bridge Platform Alignment

> **Zone-C Hybrid per ADR 0031.** Bridge is the paper's Zone-C implementation —
> hosted-node-as-SaaS with per-tenant data-plane isolation ([ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md),
> which supersedes [ADR 0026](../../docs/adrs/0026-bridge-posture.md)). The
> tables below track adoption split across three planes plus the new browser
> shell:
>
> - **Control plane** — the shared multi-tenant operator layer (signup,
>   billing, admin backoffice, tenant registry). Inherits today's Aspire +
>   Postgres + DAB + SignalR + Wolverine infrastructure. Holds no team data.
> - **Data plane** — per-tenant `local-node-host` processes, each with its
>   own SQLCipher DB and subdomain. Ciphertext-only per paper §17.2.
> - **Relay tier** — shared stateless peer-coordination service; paper §6.1
>   tier-3 / §17.2 managed relay. No persistence, no authority.
> - **Browser shell** — per-tenant Blazor Server app (Wave 5.3).
>
> Most kernel-primitive, decentralization, Property-Management, and
> input-modality rows sit in the Control plane column. The Relay tier column
> is meaningful only for rows in *Relay-tier primitives*; a new
> *Data-plane primitives* section tracks per-tenant hosted-node concerns;
> *Browser-shell primitives* tracks the Wave 5.3 browser app.

This document tracks Bridge's adoption of Sunfish platform primitives as defined in
`docs/specifications/sunfish-platform-specification.md`. Per
[ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) (superseded by
[ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) via
[ADR 0026](../../docs/adrs/0026-bridge-posture.md)), **Bridge's control plane
is a generic multi-tenant operator layer, not a vertical reference
implementation.** It hosts business-case bundles
([ADR 0007](../../docs/adrs/0007-bundle-manifest-schema.md)) at the data-plane
level — each tenant's hosted-node peer carries bundle state. Property
Management is the first reference bundle.

The Relay tier — paper's [§6.1](../../_shared/product/local-node-architecture-paper.md)
tier-3 peer-coordination service and §17.2 sustainable-revenue SKU — has
narrow responsibilities by design:

- Accept inbound sync-daemon transport connections.
- Verify peer attestations at handshake.
- Fan out `DELTA_STREAM` / `GOSSIP_PING` frames within team scope.
- Enforce connection quota (`MaxConnectedNodes`) and team allowlist (`AllowedTeamIds`).
- Remain stateless — no persistence, no authority.

**How to read this document:**

- Rows under *Core Kernel Primitives* and *Decentralized Primitives* track
  Bridge's control-plane adoption. The Relay tier does not own these primitives;
  it is a pass-through transport. The Data plane (per-tenant hosted-node)
  holds these primitives at the tenant scope — tracked in *Data-plane primitives*.
- Rows under *Property Management MVP Coverage* track the **Property Management
  bundle's** completeness, not Bridge's. A tenant without the PM bundle enabled
  is not expected to satisfy any PM-MVP row. The Relay tier hosts no bundles and
  these rows are N/A there.
- Rows under *Relay-tier primitives* track the relay only.
- Rows under *Data-plane primitives* track per-tenant hosted-node concerns.
- Rows under *Browser-shell primitives* track the Wave 5.3 Blazor Server app.

Gaps are tracked here with target phases.

Legend: 🟢 adopted | 🟡 partially adopted | 🔴 not adopted | ⚪ N/A

## Spec Section 3 - Core Kernel Primitives

| Kernel Primitive | Control plane | Relay tier | Notes |
|---|---|---|---|
| Entity storage (multi-versioned) | 🟡 | ⚪ | Control plane: EF Core entities are versioned via audit columns (CreatedAt/UpdatedAt) but not temporal tables; spec calls for as-of queries. Relay tier: relay stores no entities. |
| Version store (CRDT / Merkle DAG) | 🔴 | ⚪ | Control plane candidate: Automerge-style change log (see `docs/specifications/research-notes/automerge-evaluation.md`). Relay tier: relay forwards deltas but does not own a version store. |
| Schema registry | 🔴 | ⚪ | Control plane: Entities use compile-time types only; no runtime schema registry. Relay tier: schema negotiation is per-peer over the handshake, not relay-held. |
| Permissions (ABAC/RBAC evaluator) | 🟡 | ⚪ | Control plane: Basic RBAC via `Permissions.cs` + `Roles.cs` in `Sunfish.Bridge.Data.Authorization`; no policy language or decision engine. Relay tier: no authority; permissions are enforced at peer endpoints, not the relay. |
| Audit trail | 🟡 | ⚪ | Control plane: `AuditRecord` entity exists; not all mutations emit audit events. Relay tier: relay writes no audit records (stateless); see relay observability row below for the transport-level signal that exists. |
| Event stream | 🔴 | ⚪ | Control plane: Wolverine handles workflow events but no canonical domain event stream for external consumers. Relay tier: fan-out is not a durable stream and deliberately so. |
| Blob store (CID-addressed) | 🔴 | ⚪ | Control plane: Bridge stores no large binaries today. Candidate: `Sunfish.Foundation.Blobs` module with CID v1 + SHA-256 + `FileSystemBlobStore` default; upgrade path to `IpfsBlobStore` for federated deployments. See `docs/specifications/research-notes/ipfs-evaluation.md`. Relay tier: relay is not a blob store. |

## Spec Section 2 - Decentralized Primitives

| Primitive | Control plane | Relay tier | Notes |
|---|---|---|---|
| Cryptographic ownership proofs | 🔴 | ⚪ | Control plane: No crypto primitives in Foundation yet; `DemoTenantContext` uses tenant IDs as strings. Candidate implementation: Keyhive-style Ed25519 + BeeKEM (see `docs/specifications/research-notes/automerge-evaluation.md`). Relay tier: ownership proofs are carried inside frames by peers; the relay is not the authority. |
| Delegation / time-bound access | 🔴 | ⚪ | Control plane: Not implemented. Candidate: Keyhive group-membership graphs (primary) + Macaroon-style ephemeral tokens (supplement for short-lived scenarios). Relay tier: delegation is peer-held; relay does not mint or verify delegations. |
| Federation (peer-to-peer sync) | 🔴 | 🟢 | Control plane: Single-server deployment; no federation endpoints. Relay tier: federation IS the posture — `RelayServer` connects peers in the paper §6.1 tier-3 sense. Candidate future: Automerge-style sync protocol shape adapted for .NET; see evaluation doc for integration paths (sidecar vs native .NET rewrite). |

## Spec Section 6 - Property Management MVP Coverage

> These rows track the **Property Management bundle** (Bridge's first reference
> bundle) under the **Control plane / Data-plane** axis, not Bridge itself.
> See ADR 0006. The Relay tier hosts no bundles, so every PM-MVP row is ⚪
> under the Relay tier and its column is omitted from this table.

| MVP Feature | Control plane | Notes |
|---|---|---|
| Tenant leases | 🔴 | Not modeled; Bridge currently uses generic `TaskItem` entities |
| Rent collection | 🔴 | Not implemented |
| Inspection scheduling & documentation | 🔴 | Bridge has generic tasks but no inspection-specific workflow |
| Maintenance requests + vendor quotes | 🟡 | Generic task board covers request intake; quote flow not implemented |
| Repair tracking + depreciation | 🔴 | Not implemented |
| Tax reporting | 🔴 | Not implemented |
| Contractor delegation | 🟡 | RBAC supports role-based access; time-bound delegation not implemented |
| Compliance audit trails | 🟡 | Generic `AuditRecord` exists; not scoped per jurisdictional requirement |

## Spec Section 7 - Input Modalities

| Modality | Control plane | Relay tier | Notes |
|---|---|---|---|
| Forms | 🟢 | ⚪ | Control plane: SunfishForm + Inputs cover standard data entry. Relay tier: no UI surface. |
| Spreadsheet import/export | 🟡 | ⚪ | Control plane: `SunfishDataSheet` renders spreadsheet UX; no CSV/XLSX import pipeline. Relay tier: no UI surface. |
| Voice transcription | 🔴 | ⚪ | Control plane: Not implemented. Relay tier: out of scope. |
| Sensor data ingestion | 🔴 | ⚪ | Control plane: Not implemented. Relay tier: out of scope. |
| Drone/robot imagery | 🔴 | ⚪ | Control plane: Not implemented. Relay tier: out of scope. |
| Satellite imagery | 🔴 | ⚪ | Control plane: Not implemented. Relay tier: out of scope. |

## Spec Section 8 - Asset Evolution & Versioning

| Capability | Control plane | Relay tier | Notes |
|---|---|---|---|
| Hierarchy mutations (split/merge/re-parent) | 🔴 | ⚪ | Control plane: Entity parent-child is static. Relay tier: relay does not own hierarchies. |
| Temporal as-of queries | 🔴 | ⚪ | Control plane: No point-in-time view support. Relay tier: relay holds no history. |
| Metadata resolution improvements | 🔴 | ⚪ | Control plane: No schema evolution story. Relay tier: schema evolution is peer-held. |

## Spec Section 9 - BIM Integration

| Capability | Control plane | Relay tier | Notes |
|---|---|---|---|
| IFC/Revit import | ⚪ | ⚪ | Control plane: Not in scope for property management MVP; may apply in later verticals (military base, transit, healthcare). Relay tier: relay has no import surface. |

## Spec Section 10 - Multi-Jurisdictional & Multi-Tenant

| Capability | Control plane | Relay tier | Notes |
|---|---|---|---|
| Multi-tenant isolation | 🟡 | ⚪ | Control plane: `ITenantContext` in `Sunfish.Foundation.Authorization`; `DemoTenantContext` is single-tenant; EF query filters are wired. Per-tenant data-plane isolation is Wave 5.2 (one `local-node-host` per tenant). Relay tier: tenancy is expressed via team-scoped fan-out (see *Relay-tier primitives* → Team-scoped fan-out) rather than per-tenant data isolation. |
| Time-bound access (Macaroons) | 🔴 | ⚪ | Control plane: Not implemented. Relay tier: capabilities are peer-held, not relay-issued. |
| Federation patterns | 🔴 | 🟢 | Control plane: Not implemented. Relay tier: managed-relay IS the federation pattern (paper §6.1 tier-3). |
| Jurisdictional routing | 🔴 | 🔴 | Control plane: Not implemented. Relay tier: a single relay does not today route across jurisdictional boundaries; candidate for relay-mesh work under Platform Phase D. |

## Relay-tier primitives

> Rows in this section apply only to the **Relay tier** (shared stateless
> peer-coordination service). The Control plane does not expose these
> primitives — its transport surface is HTTPS + SignalR, not the sync-daemon
> transport. All rows are therefore ⚪ under Control plane and the column is
> omitted from this table.

| Primitive | Relay tier | Notes |
|---|---|---|
| Inbound transport (Unix socket / named pipe) | 🟢 | Provided by `UnixSocketSyncDaemonTransport` (POSIX path or Windows named pipe); wired in the relay composition root. |
| Handshake verification | 🟡 | Ed25519 HELLO signature wiring is in progress per the follow-up wave (paper-alignment plan). Today the relay uses a deterministic zero-id and accepts handshakes with stub-signed HELLO per `HandshakeProtocol` remarks; verification hardens in the next wave. |
| Team-scoped fan-out | 🟢 | `RelayServer` derives the peer's effective team-id from the first granted stream (`CapabilityResult.Granted[0]`) and fans `DELTA_STREAM` / `GOSSIP_PING` frames to same-team peers only. Peers with no granted streams land in the empty-team bucket (safe default, no cross-team leakage). |
| Connection quota enforcement | 🟢 | `RelayOptions.MaxConnectedNodes` (default 500, sized per paper §17.2). Over-limit connections are rejected pre-handshake with `ERROR { Code: RATE_LIMIT_EXCEEDED, Recoverable: false }`. |
| Team allowlist | 🟢 | `RelayOptions.AllowedTeamIds`. Empty = accept any team; non-empty = post-handshake gate that disconnects peers whose agreed team-id is not on the list. |
| Observability (connection counts, throughput metrics) | 🔴 | Only structured ILogger events today. No OpenTelemetry metrics for connected-count, bytes-fanned-out, handshake-failures, or per-team usage. Tracked below under Relay-tier next steps. |
| Health + alive endpoints | 🟢 | `/health` and `/alive` are wired in Relay posture only (the Aspire ServiceDefaults are not loaded in this posture; the endpoints are published directly by the relay host). |
| Statelessness | 🟢 | Relay holds only the in-memory `_connections` map; nothing is persisted. Crash = peers reconnect. This is a posture invariant, not a gap. |
| Graceful drain on shutdown | 🔴 | `RelayServer.StopAsync` cancels the accept loop and drops connections; it does not wait for peers to observe disconnect or give them a chance to re-home to another relay. Tracked under Relay-tier next steps. |

## Data-plane primitives (per-tenant hosted node)

> Rows in this section track **per-tenant data-plane concerns** — the
> `apps/local-node-host` process Bridge spawns per tenant per ADR 0031 /
> Wave 5.2. All rows land 🔴 until Wave 5.2 begins; the column is omitted
> from the Control plane and Relay tier tables because these concerns do
> not live there.

### Wave 5.2 Zone-C data-plane architecture (ADR 0031 §Zone C)

Bridge's SaaS posture composes four orchestration surfaces, registered
by `AddBridgeOrchestration` / `AddBridgeOrchestrationHealth` /
`AddBridgeOrchestrationSupervisor` and wired to the rest of the
composition in `Sunfish.Bridge/Program.cs::ConfigureSaasPosture`:

- **`ITenantRegistry` (Wave 5.2.B)** — façade over `SunfishBridgeDbContext`
  that owns signup, trust-level, and lifecycle transitions (`SuspendAsync`,
  `ResumeAsync`, `CancelAsync`). Publishes `TenantLifecycleEvent`s to
  `ITenantRegistryEventBus` after every mutation. Holds no team data
  beyond `{tenant_id, plan, team_public_key}` per paper §17.2.
- **`ITenantProcessSupervisor` (Wave 5.2.C.1)** — per-tenant process
  supervisor. `StartAsync` picks an ephemeral port, creates the data
  directory via `TenantPaths.NodeDataDirectory`, derives a per-tenant
  seed via `TenantSeedProvider` (HKDF over the install-level root seed),
  and spawns `local-node-host` via `Process.Start`. `PauseAsync`,
  `ResumeAsync`, `StopAndEraseAsync(mode)` round out the lifecycle.
  Per-tenant gate serializes same-tenant concurrent operations;
  cross-tenant operations are fully parallel.
- **`TenantHealthMonitor` (Wave 5.2.D)** — hosted service that polls
  every live tenant's `/health` endpoint every `HealthPollInterval`.
  Transitions to `Unhealthy` after `HealthFailureStrikeCount` consecutive
  failures, recovers to `Healthy` on the first subsequent 200, fires
  `HealthChanged` events consumed by the coordinator.
- **`TenantLifecycleCoordinator` (Wave 5.2.C.1 + 5.2.E)** — hosted
  service that subscribes to both the registry event bus and the health
  monitor. Drives supervisor transitions. On `StartAsync` (i.e.
  AppHost boot) it re-reads `ListActiveAsync()` and re-spawns every
  Active tenant — the Wave 5.2 Resume Protocol covering the "supervisor
  state is in-memory" anti-pattern called out in the decomposition plan.

Bridge's AppHost (`Sunfish.Bridge.AppHost/Program.cs`) passes two
environment variables to `bridge-web` so the supervisor has what it
needs before any tenant signup: `Bridge__Orchestration__TenantDataRoot`
(defaults to `{TempPath}/sunfish-bridge-tenants`) and
`Bridge__Orchestration__LocalNodeExecutablePath` (resolved from
`Projects.Sunfish_LocalNodeHost.ProjectPath` metadata at AppHost startup).
The `local-node-host` project is a `ProjectReference` of the AppHost
csproj so the dll exists by the time bridge-web boots; the Aspire
`AddProject<Projects.Sunfish_LocalNodeHost>` boot path (Wave 5.2.C.2)
remains carved-out pending stop-work #3 on Aspire resource-graph
mutability.

The Wave 5.2.E three-tenant smoke test
(`accelerators/bridge/tests/Sunfish.Bridge.Tests.Integration/Wave52/`)
exercises the full loop against a direct Bridge composition (not
`DistributedApplicationTestingBuilder` — that requires a container
runtime). Each test tenant gets a distinct ephemeral port, a distinct
data directory, and a cryptographically-distinct seed; the restart
test proves the Resume Protocol re-spawns tenants from
authoritative DB state and on-disk directories survive.

| Primitive | Data plane | Notes |
|---|---|---|
| Per-tenant `local-node-host` spawn orchestration | 🟡 | Wave 5.2.C.1 landed — `TenantProcessSupervisor` spawns one child per tenant via `Process.Start` with per-tenant data root and ephemeral-port health endpoint. Wave 5.2.E wires it to `Bridge.AppHost` and exercises it via a three-tenant smoke test. Aspire `AddProject` boot path (5.2.C.2) still gated on stop-work #3. See `_shared/product/wave-5.2-decomposition.md` §4. |
| Per-tenant SQLCipher DB | 🔴 | Wave 5.2. Dedicated DB per tenant at `{DataDirectory}/tenants/{tenant_id}/sunfish.db`; paper §11.2 Layer 1 (encrypted at rest). |
| Per-tenant subdomain routing | 🔴 | Wave 5.2 / 5.3. `acme.sunfish.example.com` → tenant `acme`'s hosted-node + browser-shell. Operator admin at `admin.sunfish.example.com`. |
| Per-tenant role-attestation issuance | 🔴 | Wave 5.2. Tenant admin opt-in issues attestation to the hosted-node peer (paper §11.3) — required for the Attested-hosted-peer trust level. |
| Trust-level configuration (Relay-only / Attested / No hosted peer) | 🟢 | Wave 5.1 landed — `TenantRegistration.TrustLevel` is recorded at signup by `TenantRegistry.CreateAsync` and enforced at spawn by `TenantProcessSupervisor.StartAsync` (5.2.C.1 refuses to spawn `NoHostedPeer` tenants). |
| Per-tenant seed isolation | 🟢 | Wave 5.2 stop-work #1 resolved — `TenantSeedProvider` HKDF-derives a per-tenant 32-byte Ed25519 seed from the install-level root seed and injects it to the child via `LocalNode__RootSeedHex`. Two tenants on one Bridge host derive cryptographically independent keys. |
| Per-tenant health-probe surface | 🟢 | Wave 5.2.D landed — `TenantHealthMonitor` polls each live tenant's `/health` endpoint every `HealthPollInterval`, transitions to `Unhealthy` after `HealthFailureStrikeCount` consecutive failures, fires `HealthChanged` consumed by `TenantLifecycleCoordinator`. |
| Registry → supervisor event routing | 🟢 | Wave 5.2.B + 5.2.C.1 landed — `TenantLifecycleCoordinator` subscribes to `ITenantRegistryEventBus` and drives supervisor `StartAsync` / `PauseAsync` / `ResumeAsync` / `StopAndEraseAsync` on matching lifecycle transitions. |
| Startup-rebuild / Resume Protocol | 🟡 | Wave 5.2.E added — `TenantLifecycleCoordinator.StartAsync` re-reads `ITenantRegistry.ListActiveAsync()` at host boot and re-spawns every Active tenant (supervisor is in-memory, so this covers AppHost restart). Covered by `AppHostRestart_PreservesTenantStateAndDisk` integration test. Endpoint routing persistence (stop-work #5) is still Wave 5.3 territory. |
| Relay-allowlist refresh | 🟡 | Wave 5.2.E added — `BridgeRelayAllowlistRefresher` hosted service re-reads `ListActiveAsync` every `RelayRefreshInterval` and updates `RelayOptions.AllowedTeamIds`. Fail-closed sentinel on empty active set. Posture-agnostic: no-ops in SaaS posture when the relay isn't co-hosted. |
| Ciphertext-only event-log persistence | 🔴 | Wave 5.2. Hosted-node peer persists the tenant's event-log ciphertext for catch-up-on-reconnect; operator cannot decrypt unless the tenant admin issues an attestation. Paper §17.2 invariant. |
| Tenant lifecycle (create, pause, delete) | 🟢 | Wave 5.2.B + 5.2.C.1 landed — `TenantRegistry.SuspendAsync` / `ResumeAsync` / `CancelAsync` publish events; supervisor acts on them via the coordinator. `DeleteMode.RetainCiphertext` moves the disk to `{TenantDataRoot}/graveyard/{id}/{cancelledAt}`; `SecureWipe` removes it. In-flight-gossip drain is a Wave 5.5 concern. |

## Browser-shell primitives (Wave 5.3)

> Rows in this section track the **per-tenant Blazor Server browser shell**
> — a new product surface introduced by ADR 0031 / Wave 5.3. All rows land
> 🔴 until Wave 5.3 begins.

| Primitive | Browser shell | Notes |
|---|---|---|
| Per-tenant Blazor Server app at `{tenant}.sunfish.example.com` | 🔴 | Wave 5.3. New deployable per tenant subdomain; shares composition with Bridge adapters but hosts only the browser surface. |
| Passphrase-derived device-key bootstrap | 🔴 | Wave 5.3 default. Passphrase → Argon2id → device key → unwrap role-key bundle in memory. Paper §11.2 Layer 2 equivalent for the browser. |
| WebAuthn-hardened key bootstrap (opt-in) | 🔴 | Wave 5.3 opt-in. Enterprise-tier device-key bootstrap via platform authenticator; deferred ADR for regulated-industry-only path. |
| QR-from-phone fallback bootstrap | 🔴 | Wave 5.3. Admin-initiated invites reuse Anchor's QR-onboarding (Wave 3.4) bundle shape. |
| Ephemeral in-memory node (session-scoped) | 🔴 | Wave 5.3. Role keys + CRDT state held in memory only; wipe on tab close / logout. No persistent browser local-node in v1 (OPFS path deferred to v2 per ADR 0031 open question). |
| WebSocket sync-transport to hosted-node peer | 🔴 | Wave 5.3. Browser ↔ per-tenant `local-node-host` over authenticated WebSocket; frames are identical to sync-daemon-protocol §3 transport. |
| Founder + joiner flows via browser | 🔴 | Wave 5.4. Adapts Anchor's QR-bundle flow for browser-first signup; operator = tenant record; tenant-side = founder bundle generation on first admin device. |

## Next Steps (by target migration phase or future)

### Control-plane next steps

- **Post-Phase 9 (immediate):** None - Bridge is a functional demo as shipped.
- **Platform Phase A (asset modeling - new migration phase):** Expand kernel primitives for temporal entities + asset hierarchies; swap Bridge's generic `TaskItem` entity for a Property/Unit/Fixture hierarchy.
- **Platform Phase B (decentralization - new migration phase):** Introduce crypto primitives in Foundation; adopt Keyhive-inspired group-membership capability model (see `docs/specifications/research-notes/automerge-evaluation.md` for the Keyhive-vs-Macaroons reconciliation); rewire `DemoTenantContext` to use real Ed25519 signed claims. Adopt Automerge's Merkle-DAG change-log semantics and sync-protocol shape without integrating the Automerge library directly (no .NET binding exists as of April 2026; integration via sidecar is an option for a later phase). Initial implementation is a .NET-native version store + crypto + sync inspired by Automerge + Keyhive.
- **Platform Phase B-blobs (parallel with decentralization):** Build `Sunfish.Foundation.Blobs` with CID v1 + SHA-256 + `FileSystemBlobStore` default. Bridge adopts it for any binary ingestion (avatars, document attachments, future drone imagery per spec Section 7). Plumbing ready for `IpfsBlobStore` backend when federation comes online. See `docs/specifications/research-notes/ipfs-evaluation.md`.
- **Platform Phase C (input modalities - new migration phase):** Build the ingestion pipeline per spec Section 7; wire voice/sensor/drone ingestion into Bridge as optional inputs.
- **Platform Phase D (federation - new migration phase):** Define federation protocol; implement peer-to-peer sync using Automerge-style sync protocol shape for structured entities, and stand up a private IPFS network + IPFS-Cluster for multi-jurisdictional blob replication. Blob-side and entity-side federation operate over libp2p-compatible transports but remain operationally separate processes. Demonstrate a cross-jurisdictional scenario (landlord + code-enforcement agency share inspection data).

These phases are future work beyond the current migration scope (Phases 1-9). They become concrete plan documents when prioritized.

### Relay-tier next steps

- **Real Ed25519 wiring on HELLO (follow-up wave, near-term).** Replace the current zero-id/stub-signed handshake with Ed25519 signature verification over `node_id ‖ schema_version ‖ sent_at` per `docs/specifications/sync-daemon-protocol.md` §3.1 and §9.1. Reject `HELLO_SIGNATURE_INVALID` and `HELLO_TIMESTAMP_STALE` per the spec. This is the handshake-verification row above moving from 🟡 to 🟢.
- **Metrics + OpenTelemetry integration.** Emit `relay.connected_count`, `relay.handshake_failures_total`, `relay.fanout_bytes_total`, and per-team gauges. Hook into Aspire `ServiceDefaults`-equivalent OTEL wiring (or a relay-specific shim, since the Relay tier does not load the full Control-plane ServiceDefaults). Moves the observability row from 🔴 to 🟢.
- **Rate-limiting per-peer.** Today `MaxConnectedNodes` is a global cap; a misbehaving peer can still flood the fan-out path with frames. Add per-peer token-bucket rate limits on `DELTA_STREAM` / `GOSSIP_PING` ingress and document the resulting `ERROR { Code: RATE_LIMIT_EXCEEDED, Recoverable: true }` semantics.
- **Graceful drain on shutdown before RelayWorker exits.** Extend `RelayServer.StopAsync` to (a) refuse new inbound connections, (b) send a `GOSSIP_PING`-style drain hint to connected peers, and (c) wait for peers to observe the hint (bounded timeout) before force-closing. Moves the drain row from 🔴 to 🟢.
- **SLA-backed production deployment per paper §17.2.** Stand up operationally-hardened managed-relay infrastructure behind the sustainable-revenue SKU: uptime SLA, NAT-traversal support, first-line support contact. This is the revenue-SKU activation gate and depends on the four operational items above.
