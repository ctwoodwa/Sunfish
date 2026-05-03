# Sunfish.Foundation.LocalFirst

Offline-capable local-operation primitives — offline store, outbound queue, sync engine, conflict resolver, data export/import.

Contracts plus minimal in-memory references. Implements [ADR 0012](../../docs/adrs/0012-local-first-operation.md).

## What this ships

### Contracts

- **`IOfflineStore`** — local persistence seam for offline operation; reads return whatever was last cached + any local mutations not yet synced.
- **`IOutboundQueue`** — durable queue of operations performed locally that need to be replayed against the server.
- **`ISyncEngine`** — orchestrator that drains the outbound queue + pulls server-side updates + invokes the conflict resolver on collisions.
- **`IConflictResolver`** — strategy for resolving sync collisions (last-write-wins / CRDT-merge / operator-prompt / etc.).
- **`IDataExporter`** + **`IDataImporter`** — per-tenant data export/import for backup, migration, or "take-out" scenarios.

### Reference impls

- **In-memory** versions of every contract (test + non-production-host fixtures).

## When to use this

Anchor (per ADR 0031 / 0032) is the primary consumer — multi-team workspace switching with offline tolerance. Bridge in SaaS posture is online-first; the relay posture (per ADR 0026) leans on the same primitives differently.

Mobile clients (W#23 iOS field-capture, when shipped) will also consume these contracts (or their kernel-substrate equivalents — the Anchor + iOS sync surfaces are converging per the Mission Space Matrix research, W#33).

## ADR map

- [ADR 0012](../../docs/adrs/0012-local-first-operation.md) — local-first operation
- [ADR 0026](../../docs/adrs/0026-bridge-dual-posture.md) — Bridge SaaS vs Relay postures
- [ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) — hybrid hosted-node-as-SaaS
- [ADR 0032](../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) — Anchor multi-team workspace switching

## See also

- [apps/docs Overview](../../apps/docs/foundation/localfirst/overview.md)
- [Sunfish.Kernel.Sync](../kernel-sync/README.md) — kernel-tier sync substrate consumed by this layer
