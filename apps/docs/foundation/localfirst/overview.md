---
uid: foundation-localfirst-overview
title: Local-First — Overview
description: Offline-capable storage, outbound operation queue, sync engine, and tenant data import/export.
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

Every byte-shaped contract in this package transports `byte[]`, not a typed payload. Callers choose their own serialization (JSON, MessagePack, CBOR, protobuf) per module, so the framework does not force one serialization policy across the whole platform.

## Registering the defaults

```csharp
using Sunfish.Foundation.LocalFirst;

services.AddSunfishLocalFirst();
```

`AddSunfishLocalFirst` registers the in-memory `IOfflineStore`, `IOfflineQueue`, and the default `LastWriterWinsConflictResolver`. The sync engine, export service, and import service are composition concerns — accelerators wire those behind host-specific implementations.

## Out of scope

`foundation-localfirst` does not ship:

- A network transport — adapters bring their own (HTTP, gRPC, WebSockets).
- A peer topology — the engine does not assume star, mesh, or CRDT sync patterns.
- A persistent queue — the shipped queue is in-memory. Durable adapters plug into `IOfflineQueue`.

## Related

- [Offline Store](offline-store.md)
- [Sync Engine](sync-engine.md)
