---
uid: foundation-localfirst-overview
title: Local-First — Overview
description: Offline-capable storage, outbound operation queue, sync engine, and tenant data import/export.
keywords:
  - local first
  - offline
  - sync engine
  - offline queue
  - data export
  - data import
  - ADR 0012
---

# Local-First — Overview

## What this package gives you

`Sunfish.Foundation.LocalFirst` is the offline-first seam for Sunfish. It bundles four related contracts that together support apps that keep working when the network goes away:

- **`IOfflineStore`** — a keyed binary read / write / delete over a local store. Callers serialize their own payloads into bytes.
- **`IOfflineQueue`** — an outbound operation queue for work produced while offline. The sync engine drains this queue when connectivity is back.
- **`ISyncEngine`** (+ `ISyncConflictResolver`) — orchestrates local ↔ remote cycles, streams progress events, and resolves conflicts.
- **`IDataExportService`** / **`IDataImportService`** — tenant data export and import for backup, migration, and tenant portability.

The package source lives at `packages/foundation-localfirst/`. It ships in-memory reference implementations of the store, queue, and a last-writer-wins conflict resolver. Real-world adapters (IndexedDB-backed store in Blazor WebAssembly, SQLite-backed store in a desktop shell, Postgres-backed queue in Bridge) plug into the same contracts.

## When to use it

Reach for `foundation-localfirst` when any of these hold:

- You ship a **lite-mode** deployment that must run without a server.
- You have a **mobile or field scenario** where a user's device drops connectivity for minutes to hours.
- You need **deferred delivery** — the user takes an action in the UI, and the platform guarantees it will reach the backend when possible.
- You need to **export** a tenant's data for backup, compliance, or a migration off the platform, and **import** it back into a fresh environment.

Skip the package for traditional online-first apps — the queue and sync engine add complexity you do not need when the network is assumed to be up.

## Format-agnostic by design

Every byte-shaped contract in this package transports `byte[]`, not a typed payload. Callers choose their own serialization (JSON, MessagePack, CBOR, protobuf) per module, so the framework does not force one serialization policy across the whole platform. Export and import default to `application/json` but accept a free-form `Format` media type on the request — modules that need a denser wire format override per call.

## Contract map

| Contract | Purpose | Default |
|---|---|---|
| [`IOfflineStore`](xref:Sunfish.Foundation.LocalFirst.IOfflineStore) | Read / write / delete / list-prefix over keyed bytes. | `InMemoryOfflineStore` |
| [`IOfflineQueue`](xref:Sunfish.Foundation.LocalFirst.IOfflineQueue) | Enqueue / peek / acknowledge outbound operations. | `InMemoryOfflineQueue` |
| [`ISyncEngine`](xref:Sunfish.Foundation.LocalFirst.ISyncEngine) | One sync cycle per call; streams events. | **Not registered by default** — hosts plug in. |
| [`ISyncConflictResolver`](xref:Sunfish.Foundation.LocalFirst.ISyncConflictResolver) | Merges local and remote versions of a key. | `LastWriterWinsConflictResolver` |
| [`IDataExportService`](xref:Sunfish.Foundation.LocalFirst.IDataExportService) | Queues a tenant-data export, reports progress, streams download. | **Not registered by default.** |
| [`IDataImportService`](xref:Sunfish.Foundation.LocalFirst.IDataImportService) | Consumes a package and merges records into a target tenant. | **Not registered by default.** |

## Export and import shape

The export contract models an asynchronous job:

```csharp
public interface IDataExportService
{
    ValueTask<ExportHandle> StartExportAsync(ExportRequest request, CancellationToken ct = default);
    ValueTask<ExportStatus> GetStatusAsync(Guid exportId, CancellationToken ct = default);
    ValueTask<Stream> OpenDownloadAsync(Guid exportId, CancellationToken ct = default);
}
```

`ExportRequest` carries the target tenant (`null` for system-scope exports), a requested `Format` media type (defaults to `application/json`), and a list of module / scope keys to include (empty means all). `ExportStatus` tracks `Pending → Running → Completed | Failed` with a `ProgressPercent` value; `OpenDownloadAsync` produces the final artifact as a stream.

Import mirrors the shape synchronously from the caller's side:

```csharp
public interface IDataImportService
{
    ValueTask<ImportResult> ImportAsync(
        Stream package,
        ImportOptions options,
        CancellationToken cancellationToken = default);
}
```

`ImportOptions` names the `TargetTenantId`, the expected `Format`, an `OverwriteExisting` flag, and a scope filter. `ImportResult` returns `RecordsImported`, `RecordsSkipped`, `RecordsFailed`, a completion timestamp, and a list of non-fatal `Errors`.

Module-level export / import contributors plug in via a P2 follow-up contract — the current services handle the orchestration shape, not per-module payload logic.

## Registering the defaults

```csharp
using Sunfish.Foundation.LocalFirst;

services.AddSunfishLocalFirst();
```

`AddSunfishLocalFirst` registers the in-memory `IOfflineStore` and `IOfflineQueue`, plus `LastWriterWinsConflictResolver` as the default `ISyncConflictResolver`. The sync engine, export service, and import service are **not** registered by default — they are composition concerns that accelerators wire behind host-specific implementations.

## Out of scope

`foundation-localfirst` does not ship:

- A network transport — adapters bring their own (HTTP, gRPC, WebSockets).
- A peer topology — the engine does not assume star, mesh, or CRDT sync patterns.
- A persistent queue — the shipped queue is in-memory. Durable adapters plug into `IOfflineQueue`.
- A durable export store — `StartExportAsync` returns a handle, but the backing store is a host decision.

## Related

- [Offline Store](offline-store.md)
- [Sync Engine](sync-engine.md)
- [ADR 0012 — Foundation.LocalFirst](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0012-foundation-localfirst.md)
</content>
</invoke>