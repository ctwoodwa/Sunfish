# Bridge Platform Alignment

> **ADR 0026 — dual-posture note.** Bridge now ships in two postures per
> [ADR 0026](../../docs/adrs/0026-bridge-posture.md). The tables below track
> **Posture A (SaaS shell)** and **Posture B (managed relay)** separately.
> Posture A has per-tenant authority semantics — it owns entities, evaluates
> permissions, stores versions, and hosts business-case bundles. Posture B
> is a paper §6.1 tier-3 / §17.2 managed relay: stateless peer-coordination
> only, no authority semantics. Most kernel-primitive, decentralization,
> Property-Management, and input-modality rows are therefore **⚪ N/A** in
> Posture B; a new *Relay-specific primitives* section tracks the rows that
> are meaningful for Posture B.

This document tracks Bridge's adoption of Sunfish platform primitives as defined in
`docs/specifications/sunfish-platform-specification.md`. Per
[ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) (superseded by
[ADR 0026](../../docs/adrs/0026-bridge-posture.md)), **Bridge in Posture A is
a generic multi-tenant SaaS shell, not a vertical reference implementation.**
It hosts business-case bundles ([ADR 0007](../../docs/adrs/0007-bundle-manifest-schema.md));
Property Management is its first reference bundle.

Posture B — the managed relay — is the paper's [§6.1](../../_shared/product/local-node-architecture-paper.md)
tier-3 peer-coordination service and §17.2 sustainable-revenue SKU. Its
responsibilities are narrow by design:

- Accept inbound sync-daemon transport connections.
- Verify peer attestations at handshake.
- Fan out `DELTA_STREAM` / `GOSSIP_PING` frames within team scope.
- Enforce connection quota (`MaxConnectedNodes`) and team allowlist (`AllowedTeamIds`).
- Remain stateless — no persistence, no authority.

**How to read this document:**

- Rows under *Core Kernel Primitives* and *Decentralized Primitives* track
  Bridge's own adoption — shell-level concerns in Posture A. Posture B does
  not own these primitives; the relay is a pass-through transport.
- Rows under *Property Management MVP Coverage* track the **Property Management
  bundle's** completeness, not Bridge's. A Bridge tenant without the PM bundle
  enabled is not expected to satisfy any PM-MVP row. Posture B hosts no
  bundles (ADR 0026 consequence) and these rows are N/A there.
- Rows under *Relay-specific primitives* track **Posture B only**; they do
  not apply to Posture A.

Gaps are tracked here with target phases.

Legend: 🟢 adopted | 🟡 partially adopted | 🔴 not adopted | ⚪ N/A

## Spec Section 3 - Core Kernel Primitives

| Kernel Primitive | Posture A (SaaS shell) | Posture B (managed relay) | Notes |
|---|---|---|---|
| Entity storage (multi-versioned) | 🟡 | ⚪ | Posture A: EF Core entities are versioned via audit columns (CreatedAt/UpdatedAt) but not temporal tables; spec calls for as-of queries. Posture B: relay stores no entities. |
| Version store (CRDT / Merkle DAG) | 🔴 | ⚪ | Posture A candidate: Automerge-style change log (see `docs/specifications/research-notes/automerge-evaluation.md`). Posture B: relay forwards deltas but does not own a version store. |
| Schema registry | 🔴 | ⚪ | Posture A: Entities use compile-time types only; no runtime schema registry. Posture B: schema negotiation is per-peer over the handshake, not relay-held. |
| Permissions (ABAC/RBAC evaluator) | 🟡 | ⚪ | Posture A: Basic RBAC via `Permissions.cs` + `Roles.cs` in `Sunfish.Bridge.Data.Authorization`; no policy language or decision engine. Posture B: no authority; permissions are enforced at peer endpoints, not the relay. |
| Audit trail | 🟡 | ⚪ | Posture A: `AuditRecord` entity exists; not all mutations emit audit events. Posture B: relay writes no audit records (stateless); see relay observability row below for the transport-level signal that exists. |
| Event stream | 🔴 | ⚪ | Posture A: Wolverine handles workflow events but no canonical domain event stream for external consumers. Posture B: fan-out is not a durable stream and deliberately so. |
| Blob store (CID-addressed) | 🔴 | ⚪ | Posture A: Bridge stores no large binaries today. Candidate: `Sunfish.Foundation.Blobs` module with CID v1 + SHA-256 + `FileSystemBlobStore` default; upgrade path to `IpfsBlobStore` for federated deployments. See `docs/specifications/research-notes/ipfs-evaluation.md`. Posture B: relay is not a blob store. |

## Spec Section 2 - Decentralized Primitives

| Primitive | Posture A (SaaS shell) | Posture B (managed relay) | Notes |
|---|---|---|---|
| Cryptographic ownership proofs | 🔴 | ⚪ | Posture A: No crypto primitives in Foundation yet; `DemoTenantContext` uses tenant IDs as strings. Candidate implementation: Keyhive-style Ed25519 + BeeKEM (see `docs/specifications/research-notes/automerge-evaluation.md`). Posture B: ownership proofs are carried inside frames by peers; the relay is not the authority. |
| Delegation / time-bound access | 🔴 | ⚪ | Posture A: Not implemented. Candidate: Keyhive group-membership graphs (primary) + Macaroon-style ephemeral tokens (supplement for short-lived scenarios). Posture B: delegation is peer-held; relay does not mint or verify delegations. |
| Federation (peer-to-peer sync) | 🔴 | 🟢 | Posture A: Single-server deployment; no federation endpoints. Posture B: federation IS the posture — `RelayServer` connects peers in the paper §6.1 tier-3 sense. Candidate future: Automerge-style sync protocol shape adapted for .NET; see evaluation doc for integration paths (sidecar vs native .NET rewrite). |

## Spec Section 6 - Property Management MVP Coverage

> These rows track the **Property Management bundle** (Bridge's first reference
> bundle) under **Posture A only**, not Bridge itself. See ADR 0006. Posture B
> hosts no bundles (ADR 0026), so every PM-MVP row is ⚪ under Posture B and
> the column is omitted from this table.

| MVP Feature | Posture A (SaaS shell) | Notes |
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

| Modality | Posture A (SaaS shell) | Posture B (managed relay) | Notes |
|---|---|---|---|
| Forms | 🟢 | ⚪ | Posture A: SunfishForm + Inputs cover standard data entry. Posture B: no UI surface. |
| Spreadsheet import/export | 🟡 | ⚪ | Posture A: `SunfishDataSheet` renders spreadsheet UX; no CSV/XLSX import pipeline. Posture B: no UI surface. |
| Voice transcription | 🔴 | ⚪ | Posture A: Not implemented. Posture B: out of scope. |
| Sensor data ingestion | 🔴 | ⚪ | Posture A: Not implemented. Posture B: out of scope. |
| Drone/robot imagery | 🔴 | ⚪ | Posture A: Not implemented. Posture B: out of scope. |
| Satellite imagery | 🔴 | ⚪ | Posture A: Not implemented. Posture B: out of scope. |

## Spec Section 8 - Asset Evolution & Versioning

| Capability | Posture A (SaaS shell) | Posture B (managed relay) | Notes |
|---|---|---|---|
| Hierarchy mutations (split/merge/re-parent) | 🔴 | ⚪ | Posture A: Entity parent-child is static. Posture B: relay does not own hierarchies. |
| Temporal as-of queries | 🔴 | ⚪ | Posture A: No point-in-time view support. Posture B: relay holds no history. |
| Metadata resolution improvements | 🔴 | ⚪ | Posture A: No schema evolution story. Posture B: schema evolution is peer-held. |

## Spec Section 9 - BIM Integration

| Capability | Posture A (SaaS shell) | Posture B (managed relay) | Notes |
|---|---|---|---|
| IFC/Revit import | ⚪ | ⚪ | Posture A: Not in scope for property management MVP; may apply in later verticals (military base, transit, healthcare). Posture B: relay has no import surface. |

## Spec Section 10 - Multi-Jurisdictional & Multi-Tenant

| Capability | Posture A (SaaS shell) | Posture B (managed relay) | Notes |
|---|---|---|---|
| Multi-tenant isolation | 🟡 | ⚪ | Posture A: `ITenantContext` in `Sunfish.Foundation.Authorization`; `DemoTenantContext` is single-tenant; EF query filters are wired. Posture B: tenancy is expressed via team-scoped fan-out (see *Relay-specific primitives* → Team-scoped fan-out) rather than per-tenant data isolation. |
| Time-bound access (Macaroons) | 🔴 | ⚪ | Posture A: Not implemented. Posture B: capabilities are peer-held, not relay-issued. |
| Federation patterns | 🔴 | 🟢 | Posture A: Not implemented. Posture B: managed-relay IS the federation pattern (paper §6.1 tier-3). |
| Jurisdictional routing | 🔴 | 🔴 | Posture A: Not implemented. Posture B: a single relay does not today route across jurisdictional boundaries; candidate for relay-mesh work under Platform Phase D. |

## Relay-specific primitives (Posture B)

> Rows in this section apply only to **Posture B (managed relay)**. Posture A
> does not expose these primitives — its transport surface is HTTPS + SignalR,
> not the sync-daemon transport. All rows are therefore ⚪ under Posture A
> and the column is omitted from this table.

| Primitive | Posture B (managed relay) | Notes |
|---|---|---|
| Inbound transport (Unix socket / named pipe) | 🟢 | Provided by `UnixSocketSyncDaemonTransport` (POSIX path or Windows named pipe); wired in the relay composition root. |
| Handshake verification | 🟡 | Ed25519 HELLO signature wiring is in progress per the follow-up wave (paper-alignment plan). Today the relay uses a deterministic zero-id and accepts handshakes with stub-signed HELLO per `HandshakeProtocol` remarks; verification hardens in the next wave. |
| Team-scoped fan-out | 🟢 | `RelayServer` derives the peer's effective team-id from the first granted stream (`CapabilityResult.Granted[0]`) and fans `DELTA_STREAM` / `GOSSIP_PING` frames to same-team peers only. Peers with no granted streams land in the empty-team bucket (safe default, no cross-team leakage). |
| Connection quota enforcement | 🟢 | `RelayOptions.MaxConnectedNodes` (default 500, sized per paper §17.2). Over-limit connections are rejected pre-handshake with `ERROR { Code: RATE_LIMIT_EXCEEDED, Recoverable: false }`. |
| Team allowlist | 🟢 | `RelayOptions.AllowedTeamIds`. Empty = accept any team; non-empty = post-handshake gate that disconnects peers whose agreed team-id is not on the list. |
| Observability (connection counts, throughput metrics) | 🔴 | Only structured ILogger events today. No OpenTelemetry metrics for connected-count, bytes-fanned-out, handshake-failures, or per-team usage. Tracked below under Posture B Next Steps. |
| Health + alive endpoints | 🟢 | `/health` and `/alive` are wired in Relay posture only (the Aspire ServiceDefaults are not loaded in this posture; the endpoints are published directly by the relay host). |
| Statelessness | 🟢 | Relay holds only the in-memory `_connections` map; nothing is persisted. Crash = peers reconnect. This is a posture invariant, not a gap. |
| Graceful drain on shutdown | 🔴 | `RelayServer.StopAsync` cancels the accept loop and drops connections; it does not wait for peers to observe disconnect or give them a chance to re-home to another relay. Tracked under Posture B Next Steps. |

## Next Steps (by target migration phase or future)

### Posture A (SaaS shell) next steps

- **Post-Phase 9 (immediate):** None - Bridge is a functional demo as shipped.
- **Platform Phase A (asset modeling - new migration phase):** Expand kernel primitives for temporal entities + asset hierarchies; swap Bridge's generic `TaskItem` entity for a Property/Unit/Fixture hierarchy.
- **Platform Phase B (decentralization - new migration phase):** Introduce crypto primitives in Foundation; adopt Keyhive-inspired group-membership capability model (see `docs/specifications/research-notes/automerge-evaluation.md` for the Keyhive-vs-Macaroons reconciliation); rewire `DemoTenantContext` to use real Ed25519 signed claims. Adopt Automerge's Merkle-DAG change-log semantics and sync-protocol shape without integrating the Automerge library directly (no .NET binding exists as of April 2026; integration via sidecar is an option for a later phase). Initial implementation is a .NET-native version store + crypto + sync inspired by Automerge + Keyhive.
- **Platform Phase B-blobs (parallel with decentralization):** Build `Sunfish.Foundation.Blobs` with CID v1 + SHA-256 + `FileSystemBlobStore` default. Bridge adopts it for any binary ingestion (avatars, document attachments, future drone imagery per spec Section 7). Plumbing ready for `IpfsBlobStore` backend when federation comes online. See `docs/specifications/research-notes/ipfs-evaluation.md`.
- **Platform Phase C (input modalities - new migration phase):** Build the ingestion pipeline per spec Section 7; wire voice/sensor/drone ingestion into Bridge as optional inputs.
- **Platform Phase D (federation - new migration phase):** Define federation protocol; implement peer-to-peer sync using Automerge-style sync protocol shape for structured entities, and stand up a private IPFS network + IPFS-Cluster for multi-jurisdictional blob replication. Blob-side and entity-side federation operate over libp2p-compatible transports but remain operationally separate processes. Demonstrate a cross-jurisdictional scenario (landlord + code-enforcement agency share inspection data).

These phases are future work beyond the current migration scope (Phases 1-9). They become concrete plan documents when prioritized.

### Posture B (managed relay) next steps

- **Real Ed25519 wiring on HELLO (follow-up wave, near-term).** Replace the current zero-id/stub-signed handshake with Ed25519 signature verification over `node_id ‖ schema_version ‖ sent_at` per `docs/specifications/sync-daemon-protocol.md` §3.1 and §9.1. Reject `HELLO_SIGNATURE_INVALID` and `HELLO_TIMESTAMP_STALE` per the spec. This is the handshake-verification row above moving from 🟡 to 🟢.
- **Metrics + OpenTelemetry integration.** Emit `relay.connected_count`, `relay.handshake_failures_total`, `relay.fanout_bytes_total`, and per-team gauges. Hook into Aspire `ServiceDefaults`-equivalent OTEL wiring (or a relay-specific shim, since Posture B does not load the full SaaS ServiceDefaults). Moves the observability row from 🔴 to 🟢.
- **Rate-limiting per-peer.** Today `MaxConnectedNodes` is a global cap; a misbehaving peer can still flood the fan-out path with frames. Add per-peer token-bucket rate limits on `DELTA_STREAM` / `GOSSIP_PING` ingress and document the resulting `ERROR { Code: RATE_LIMIT_EXCEEDED, Recoverable: true }` semantics.
- **Graceful drain on shutdown before RelayWorker exits.** Extend `RelayServer.StopAsync` to (a) refuse new inbound connections, (b) send a `GOSSIP_PING`-style drain hint to connected peers, and (c) wait for peers to observe the hint (bounded timeout) before force-closing. Moves the drain row from 🔴 to 🟢.
- **SLA-backed production deployment per paper §17.2.** Stand up operationally-hardened managed-relay infrastructure behind the sustainable-revenue SKU: uptime SLA, NAT-traversal support, first-line support contact. This is the revenue-SKU activation gate and depends on the four operational items above.
