---
uid: foundation-localfirst-sync-engine
title: Local-First — Sync Engine
description: The ISyncEngine surface, conflict resolution, triggers, and the queue-plus-replay contract.
keywords:
  - ISyncEngine
  - sync engine
  - SyncResult
  - ISyncConflictResolver
  - conflict resolution
  - ADR 0012
---

# Local-First — Sync Engine

## What the engine does

`ISyncEngine` drives one local ↔ remote reconciliation cycle at a time. It drains the `IOfflineQueue`, fetches remote changes, resolves conflicts through `ISyncConflictResolver`, and returns a `SyncResult` describing what moved and what went wrong. It also streams `SyncEvent` records so user-facing progress UX can subscribe.

```csharp
public interface ISyncEngine
{
    ValueTask<SyncResult> SyncOnceAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<SyncEvent> StreamEventsAsync(CancellationToken cancellationToken = default);
}
```

The engine does **not** own a network transport, a peer topology, or a polling schedule. Those are host concerns — an accelerator chooses HTTP, gRPC, or WebSockets; a scheduler chooses "on app resume" or "every 30 seconds". The contract is narrow on purpose: every host can implement one cycle differently without the consumer code changing.

## The result record

```csharp
public sealed record SyncResult
{
    public int SentCount { get; init; }
    public int ReceivedCount { get; init; }
    public int ConflictCount { get; init; }
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> Errors { get; init; } = [];
}
```

`SentCount` and `ReceivedCount` describe traffic; `ConflictCount` tells the caller how many conflicts the engine detected; `Errors` captures non-fatal issues observed in the cycle (transient transport errors, skipped items). Fatal failures throw rather than populate `Errors`.

## Event stream

`StreamEventsAsync` emits `SyncEvent` records during a cycle:

```csharp
public enum SyncEventKind
{
    Started,
    Progress,
    ConflictDetected,
    Completed,
    Failed,
}

public sealed record SyncEvent
{
    public required SyncEventKind Kind { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public string? Detail { get; init; }
}
```

Consumers typically bind the stream to a progress bar, a toast surface, or a diagnostics log. The event stream is append-only and per-cycle — consumers subscribe with a cancellation token that tracks the lifetime of the UI that cares.

## Triggers

The engine is reactive by contract. A host decides when to call `SyncOnceAsync`:

- On **app launch** / resume.
- On **connectivity restored** — the host wires `NetworkInformation.NetworkAvailabilityChanged` (or the platform equivalent) to trigger a cycle.
- On **user gesture** — a "Sync now" button.
- On a **timer** — for always-on shells that tolerate a small sync cadence.

Every trigger arrives at the same method: one call to `SyncOnceAsync`. The engine is expected to be safe to call while a previous cycle is still running (queue, serialize, or drop — each adapter documents its rule).

## Queue + replay

The outbound half of the cycle drains `IOfflineQueue`:

1. `PeekPendingAsync` returns a batch (oldest first, up to `max`).
2. The engine delivers each `OfflineOperation` to the remote endpoint of its choosing, keyed by `OfflineOperation.Kind`.
3. On success, `AcknowledgeAsync(operationId)` removes the operation from the queue.
4. On a retryable failure, the operation stays in the queue; adapters typically bump `AttemptCount` in their queue implementation so they can apply backoff or fail-after-N-tries policies.

The inbound half applies remote changes to the `IOfflineStore` and detects conflicts.

## Conflict resolution

When both the local store and the remote disagree about the value for a key, the engine emits a `SyncConflict` and asks `ISyncConflictResolver.ResolveAsync` for the merged payload.

```csharp
public sealed record SyncConflict
{
    public required string Key { get; init; }
    public required byte[] LocalVersion { get; init; }
    public required byte[] RemoteVersion { get; init; }
    public byte[]? CommonAncestor { get; init; }
    public DateTimeOffset? LocalModifiedAt { get; init; }
    public DateTimeOffset? RemoteModifiedAt { get; init; }
}

public interface ISyncConflictResolver
{
    ValueTask<byte[]> ResolveAsync(SyncConflict conflict, CancellationToken cancellationToken = default);
}
```

The shipped `LastWriterWinsConflictResolver` picks the version with the later modification timestamp, preferring remote when timestamps are missing or equal. It is a safe baseline, not a universally correct strategy — modules that carry richer data (operational transforms, CRDT-shaped documents, structured audit trails) register their own resolver that understands the payload.

## Related

- [Overview](overview.md)
- [Offline Store](offline-store.md)
