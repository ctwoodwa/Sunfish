# ADR 0012 — Foundation.LocalFirst Contracts + Federation Relationship

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Define Sunfish's offline/local-first contracts, their relationship to the existing `federation-*` packages, and the seams deployment modes depend on.

---

## Context

The system vision requires three deployment modes from one codebase — lite/local-first, self-hosted, hosted SaaS — but today Sunfish has no shared abstraction for offline operation, outbound sync queues, conflict resolution, or tenant-owned data export/import. The repo does ship five `federation-*` packages:

- `federation-common` — `ISyncTransport`, `IPeerRegistry`, `ILocalHandlerDispatcher`, `SyncEnvelope`, `PeerId`, `FederationOptions`.
- `federation-entity-sync` — `IEntitySyncer`, `IChangeStore`, `InMemoryEntitySyncer`.
- `federation-capability-sync` — `ICapabilitySyncer`, `ICapabilityOpStore`, RIBLT.
- `federation-blob-replication` — `IpfsBlobStore`, Kubo adapters.
- `federation-pattern-c-tests` — integration tests for a specific worked example from the platform spec.

Federation solves a *harder* problem than local-first: peer-to-peer sync across organizational boundaries with cryptographic capability delegation, CRDT-style change reconciliation, and IPFS blob replication. That is a legitimate Sunfish capability — but the lite / self-hosted modes do not need it. A solo-device app syncing to a central Bridge tenant is local-first but not federated.

Conflating the two forces every local-first scenario to adopt IPFS, Keyhive, and RIBLT. That is the opposite of the ADR 0006 SaaS-shell posture.

---

## Decision

Introduce **`Sunfish.Foundation.LocalFirst`** as a contracts-first package holding the minimum primitives required for offline-capable local operation, outbound sync queuing, conflict resolution, and tenant-owned data export/import. Federation remains a separate, advanced implementation of a subset of these contracts; **LocalFirst does not reference federation packages**, and federation packages continue to compile and ship unchanged.

### Contracts shipped

| Type | Purpose |
|---|---|
| `IOfflineStore` | Keyed binary read / write / delete / list. Deliberately format-agnostic. JSON helpers ship as extension methods. |
| `IOfflineQueue` | Pending outbound operations (enqueue / peek / acknowledge). `OfflineOperation` record carries id, kind, payload, attempt count. |
| `ISyncEngine` | `SyncOnceAsync` + `StreamEventsAsync`. Triggered on demand (user action, connectivity restored, scheduler). |
| `SyncResult`, `SyncEvent`, `SyncEventKind` | Outcome records for a sync cycle and progress stream. |
| `ISyncConflictResolver` | Strategy for merging conflicting versions on sync. Defaults to last-writer-wins; bundles / modules override. |
| `SyncConflict` | Local + remote + optional common-ancestor blobs. |
| `IDataExportService`, `ExportRequest`, `ExportHandle`, `ExportStatus`, `ExportState` | Tenant-owned data export. Asynchronous; producer streams bytes on demand. |
| `IDataImportService`, `ImportOptions`, `ImportResult` | Inverse of export. Preserves tenant data portability across deployment modes. |
| `InMemoryOfflineStore`, `InMemoryOfflineQueue`, `LastWriterWinsConflictResolver` | Reference implementations for tests, demos, and the lite-mode baseline. |

### What LocalFirst is *not*

- Not a CRDT engine. Conflict resolution is pluggable; the default is last-writer-wins. Modules that need richer semantics register their own `ISyncConflictResolver`.
- Not a network transport. `ISyncEngine` does not know about peers, transports, or serialization wire formats — those are impl concerns.
- Not a peer registry. Local-first can be client-server; it does not assume a mesh.
- Not federation. Federation sits at a higher layer and provides additional capabilities (peer mesh, capability delegation, IPFS blobs).

### Relationship to `federation-*` packages

The two layers are intentionally orthogonal:

| Concern | LocalFirst | Federation |
|---|---|---|
| Offline read/write | Yes | Yes (via LocalFirst consumption, once retrofitted) |
| Outbound queue | Yes | Yes (federation has its own change store; may migrate to `IOfflineQueue` as a retrofit) |
| Sync cycle orchestration | Yes (`ISyncEngine`) | Yes (`IEntitySyncer`) — today a parallel abstraction |
| Cross-org peer-to-peer | No | **Yes (federation's distinctive capability)** |
| Cryptographic capability delegation | No | Yes |
| CID-addressed blob replication | No | Yes |
| Node identity / peers | No | Yes |

**Retrofit policy:** federation packages remain first-class. Their internal stores (`IChangeStore`, `ICapabilityOpStore`) can *optionally* be implemented against `IOfflineStore` / `IOfflineQueue` in a follow-up — tracked as a P4 discovery spike, not a blocker for this ADR. The public surfaces of `federation-*` remain stable; no deprecations.

A future `Sunfish.LocalFirst.Federation` adapter package can expose a federation-backed `ISyncEngine` implementation when an accelerator needs both modes simultaneously. It does not exist yet.

### Deployment modes and LocalFirst

Each deployment mode uses LocalFirst differently:

| Mode | Offline store | Sync engine | Conflict resolver |
|---|---|---|---|
| Lite (local-first) | Local file / SQLite | On-demand to a user-elected backup, or none | Last-writer-wins (default) |
| Self-hosted | Shared SQL / Postgres; offline capability optional per module | Client-server sync via HTTPS | Module-specific or LWW |
| Hosted SaaS | Server-side persistence authoritative; offline store per-client if any | Client-initiated sync | Server-authoritative |

Bundle manifests (ADR 0007) declare `deploymentModesSupported`; modules declare their LocalFirst expectations in a future module-manifest extension (P2 follow-up).

### Tenant data portability (export/import)

`IDataExportService` and `IDataImportService` are first-class so that every Sunfish tenant, in every mode, can export their data and re-import it elsewhere. This is the mechanism that makes "your data is yours" a structural property rather than a slogan.

- Export output is an opaque container (JSON bundle by default; bundle-specific formats allowed). Tenant admins access via Bridge admin or a CLI.
- Import accepts an export container and merges into a target tenant. Modules participate by implementing per-scope export contributors and import handlers; contracts for that module participation ship when the first non-trivial module requires it (P2 follow-up — out of scope for this ADR).

### Package layout

- `packages/foundation-localfirst/Sunfish.Foundation.LocalFirst.csproj`.
- Namespace: `Sunfish.Foundation.LocalFirst`.
- References `Sunfish.Foundation` only.
- Added to `Sunfish.slnx` under `/foundation/local-first/`.

---

## Consequences

### Positive

- Lite / self-hosted / hosted modes share one contract surface without forcing IPFS or federation on solo-device scenarios.
- Federation packages are preserved without rewrite; they can migrate internals to LocalFirst contracts on their own cadence.
- Tenant data export/import is a platform primitive, not a bolt-on — makes local-first a structural property.
- Modules depending only on "can I operate offline?" get a narrow contract instead of a federation-flavored one.

### Negative

- Two somewhat overlapping surfaces exist (`ISyncEngine` and `IEntitySyncer`) until the retrofit happens. This is intentional and documented.
- Default `LastWriterWinsConflictResolver` is a footgun for richer data — modules must consciously replace it. ADR calls this out explicitly.
- Export/import module-participation contracts are a P2 follow-up; without them, exports are incomplete for any non-trivial module.

### Follow-ups

1. **Federation retrofit spike** (P4) — evaluate migrating `IChangeStore` and `ICapabilityOpStore` to use `IOfflineStore` and `IOfflineQueue` underneath.
2. **Module-level export/import contributor contracts** (P2) — per-scope export payload composition and import handlers.
3. **Mode capability descriptors** — expressed on bundle manifests (ADR 0007's `deploymentModesSupported`) and on module manifests (ADR follow-up when module manifests ship).
4. **SQLite-backed `IOfflineStore`** for lite mode — adapter package, not part of this ADR.
5. **Reference lite-mode app** — an `apps/kitchen-sink-lite` that exercises the full LocalFirst path and demonstrates export/import.
6. **`LocalFirst.Federation` adapter** — when an accelerator needs federation-backed sync exposed through `ISyncEngine`.

---

## References

- ADR 0005 — Type-Customization Model (extension-field and template overlays must survive offline → sync cycles).
- ADR 0006 — Bridge Is a Generic SaaS Shell.
- ADR 0007 — Bundle Manifest Schema (`deploymentModesSupported`).
- ADR 0011 — Bundle Versioning (rollback and data-safety rules inform conflict resolver defaults).
- Martin Kleppmann, *Local-First Software* principles — user ownership, offline access, multi-device, privacy.
- `packages/federation-common`, `packages/federation-entity-sync`, `packages/federation-capability-sync`, `packages/federation-blob-replication` — preserved as-is under this ADR.
