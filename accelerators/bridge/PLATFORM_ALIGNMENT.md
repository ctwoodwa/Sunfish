# Bridge Platform Alignment

> **ADR 0026 — dual-posture note.** Bridge now ships in two postures per
> [ADR 0026](../../docs/adrs/0026-bridge-posture.md). The tables below track
> **Posture A (SaaS shell)**. Posture B (managed relay) has no per-tenant
> authority semantics — the kernel primitives, decentralization rows, and
> Property Management MVP coverage below should be read as **N/A against
> Posture B**. A split per-posture table is a follow-up deliverable; until it
> lands, assume rows on this page describe the SaaS posture only.

This document tracks Bridge's adoption of Sunfish platform primitives as defined in
`docs/specifications/sunfish-platform-specification.md`. Per
[ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) (superseded by
[ADR 0026](../../docs/adrs/0026-bridge-posture.md)), **Bridge in Posture A is
a generic multi-tenant SaaS shell, not a vertical reference implementation.**
It hosts business-case bundles ([ADR 0007](../../docs/adrs/0007-bundle-manifest-schema.md));
Property Management is its first reference bundle.

**How to read this document:**

- Rows under *Core Kernel Primitives* and *Decentralized Primitives* track
  Bridge's own adoption — shell-level concerns.
- Rows under *Property Management MVP Coverage* track the **Property Management
  bundle's** completeness, not Bridge's. A Bridge tenant without the PM bundle
  enabled is not expected to satisfy any PM-MVP row.

Gaps are tracked here with target phases.

Legend: 🟢 adopted | 🟡 partially adopted | 🔴 not adopted | ⚪ N/A

## Spec Section 3 - Core Kernel Primitives

| Kernel Primitive | Bridge Status | Notes |
|---|---|---|
| Entity storage (multi-versioned) | 🟡 | EF Core entities are versioned via audit columns (CreatedAt/UpdatedAt) but not temporal tables; spec calls for as-of queries |
| Version store (CRDT / Merkle DAG) | 🔴 | Candidate: Automerge-style change log (see `docs/specifications/research-notes/automerge-evaluation.md`) |
| Schema registry | 🔴 | Entities use compile-time types only; no runtime schema registry |
| Permissions (ABAC/RBAC evaluator) | 🟡 | Basic RBAC via `Permissions.cs` + `Roles.cs` in `Sunfish.Bridge.Data.Authorization`; no policy language or decision engine |
| Audit trail | 🟡 | `AuditRecord` entity exists; not all mutations emit audit events |
| Event stream | 🔴 | Wolverine handles workflow events but no canonical domain event stream for external consumers |
| Blob store (CID-addressed) | 🔴 | Bridge stores no large binaries today. Candidate: `Sunfish.Foundation.Blobs` module with CID v1 + SHA-256 + `FileSystemBlobStore` default; upgrade path to `IpfsBlobStore` for federated deployments. See `docs/specifications/research-notes/ipfs-evaluation.md`. |

## Spec Section 2 - Decentralized Primitives

| Primitive | Bridge Status | Notes |
|---|---|---|
| Cryptographic ownership proofs | 🔴 | No crypto primitives in Foundation yet; `DemoTenantContext` uses tenant IDs as strings. Candidate implementation: Keyhive-style Ed25519 + BeeKEM (see `docs/specifications/research-notes/automerge-evaluation.md`) |
| Delegation / time-bound access | 🔴 | Not implemented. Candidate: Keyhive group-membership graphs (primary) + Macaroon-style ephemeral tokens (supplement for short-lived scenarios) |
| Federation (peer-to-peer sync) | 🔴 | Single-server deployment; no federation endpoints. Candidate: Automerge-style sync protocol shape adapted for .NET; see evaluation doc for integration paths (sidecar vs native .NET rewrite) |

## Spec Section 6 - Property Management MVP Coverage

> These rows track the **Property Management bundle** (Bridge's first reference
> bundle), not Bridge itself. See ADR 0006.

| MVP Feature | Bridge Status | Notes |
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

| Modality | Bridge Status | Notes |
|---|---|---|
| Forms | 🟢 | SunfishForm + Inputs cover standard data entry |
| Spreadsheet import/export | 🟡 | `SunfishDataSheet` renders spreadsheet UX; no CSV/XLSX import pipeline |
| Voice transcription | 🔴 | Not implemented |
| Sensor data ingestion | 🔴 | Not implemented |
| Drone/robot imagery | 🔴 | Not implemented |
| Satellite imagery | 🔴 | Not implemented |

## Spec Section 8 - Asset Evolution & Versioning

| Capability | Bridge Status | Notes |
|---|---|---|
| Hierarchy mutations (split/merge/re-parent) | 🔴 | Entity parent-child is static |
| Temporal as-of queries | 🔴 | No point-in-time view support |
| Metadata resolution improvements | 🔴 | No schema evolution story |

## Spec Section 9 - BIM Integration

| Capability | Bridge Status | Notes |
|---|---|---|
| IFC/Revit import | ⚪ | Not in scope for property management MVP; may apply in later verticals (military base, transit, healthcare) |

## Spec Section 10 - Multi-Jurisdictional & Multi-Tenant

| Capability | Bridge Status | Notes |
|---|---|---|
| Multi-tenant isolation | 🟡 | `ITenantContext` in `Sunfish.Foundation.Authorization`; `DemoTenantContext` is single-tenant; EF query filters are wired |
| Time-bound access (Macaroons) | 🔴 | Not implemented |
| Federation patterns | 🔴 | Not implemented |
| Jurisdictional routing | 🔴 | Not implemented |

## Next Steps (by target migration phase or future)

- **Post-Phase 9 (immediate):** None - Bridge is a functional demo as shipped.
- **Platform Phase A (asset modeling - new migration phase):** Expand kernel primitives for temporal entities + asset hierarchies; swap Bridge's generic `TaskItem` entity for a Property/Unit/Fixture hierarchy.
- **Platform Phase B (decentralization - new migration phase):** Introduce crypto primitives in Foundation; adopt Keyhive-inspired group-membership capability model (see `docs/specifications/research-notes/automerge-evaluation.md` for the Keyhive-vs-Macaroons reconciliation); rewire `DemoTenantContext` to use real Ed25519 signed claims. Adopt Automerge's Merkle-DAG change-log semantics and sync-protocol shape without integrating the Automerge library directly (no .NET binding exists as of April 2026; integration via sidecar is an option for a later phase). Initial implementation is a .NET-native version store + crypto + sync inspired by Automerge + Keyhive.
- **Platform Phase B-blobs (parallel with decentralization):** Build `Sunfish.Foundation.Blobs` with CID v1 + SHA-256 + `FileSystemBlobStore` default. Bridge adopts it for any binary ingestion (avatars, document attachments, future drone imagery per spec Section 7). Plumbing ready for `IpfsBlobStore` backend when federation comes online. See `docs/specifications/research-notes/ipfs-evaluation.md`.
- **Platform Phase C (input modalities - new migration phase):** Build the ingestion pipeline per spec Section 7; wire voice/sensor/drone ingestion into Bridge as optional inputs.
- **Platform Phase D (federation - new migration phase):** Define federation protocol; implement peer-to-peer sync using Automerge-style sync protocol shape for structured entities, and stand up a private IPFS network + IPFS-Cluster for multi-jurisdictional blob replication. Blob-side and entity-side federation operate over libp2p-compatible transports but remain operationally separate processes. Demonstrate a cross-jurisdictional scenario (landlord + code-enforcement agency share inspection data).

These phases are future work beyond the current migration scope (Phases 1-9). They become concrete plan documents when prioritized.
