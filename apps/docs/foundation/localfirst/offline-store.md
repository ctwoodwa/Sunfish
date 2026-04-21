---
uid: foundation-localfirst-offline-store
title: Local-First — Offline Store
description: The IOfflineStore keyed-binary abstraction over local storage, and the in-memory reference implementation.
---

# Local-First — Offline Store

## Surface

`IOfflineStore` is a small, deliberately boring contract: a keyed-binary store with read, write, delete, and a prefix-scan.

```csharp
public interface IOfflineStore
{
    ValueTask<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(string key, byte[] value, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken cancellationToken = default);
}
```

Keys are opaque strings; values are opaque bytes. The contract intentionally does not know anything about records, entities, schemas, or tenants. Callers layer that on top — for example by encoding `tenant/{id}/profile` into the key and serializing the payload as UTF-8 JSON.

The shape mirrors the common surface of IndexedDB (in Blazor WebAssembly), SQLite (in desktop shells), and native secure storage. Adapters that bind `IOfflineStore` to those backends can be swapped per deployment without changing the calling code.

## The reference implementation

`InMemoryOfflineStore` is the shipped reference implementation:

- Backed by a `ConcurrentDictionary<string, byte[]>` — safe for concurrent readers and writers.
- `ListKeysAsync` returns an ordinal-sorted snapshot of keys with the requested prefix.
- Values are stored verbatim; callers own the serialization format.

Use it for tests, demos, and hot-path scenarios where persistence is not required. Real deployments register an adapter that talks to a durable backend.

## Using the store

```csharp
public sealed class OfflineProfileCache
{
    private readonly IOfflineStore _store;

    public OfflineProfileCache(IOfflineStore store) => _store = store;

    public async ValueTask<Profile?> ReadAsync(TenantId tenantId, CancellationToken ct)
    {
        var bytes = await _store.ReadAsync($"tenant/{tenantId}/profile", ct);
        return bytes is null ? null : JsonSerializer.Deserialize<Profile>(bytes);
    }

    public ValueTask WriteAsync(TenantId tenantId, Profile profile, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(profile);
        return _store.WriteAsync($"tenant/{tenantId}/profile", bytes, ct);
    }

    public ValueTask<IReadOnlyList<string>> ListTenantKeysAsync(TenantId tenantId, CancellationToken ct)
        => _store.ListKeysAsync($"tenant/{tenantId}/", ct);
}
```

## Pairing with the offline queue

The store handles the "what does the app know right now?" question. The queue handles the "what work still needs to reach the backend?" question. They are deliberately separate so modules can write to either without coupling the two.

`OfflineOperation` + `IOfflineQueue` look like:

```csharp
public sealed record OfflineOperation
{
    public required Guid Id { get; init; }
    public required string Kind { get; init; }          // caller-defined topic
    public required byte[] Payload { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public int AttemptCount { get; init; }
}

public interface IOfflineQueue
{
    ValueTask EnqueueAsync(OfflineOperation operation, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<OfflineOperation>> PeekPendingAsync(int max = 100, CancellationToken cancellationToken = default);
    ValueTask AcknowledgeAsync(Guid operationId, CancellationToken cancellationToken = default);
    ValueTask<int> CountAsync(CancellationToken cancellationToken = default);
}
```

Producers enqueue operations; the sync engine peeks, delivers, and acknowledges. `InMemoryOfflineQueue` is the reference implementation — safe for concurrent enqueues, ordered by enqueue time, and ready for replacement by a durable adapter.

## Related

- [Overview](overview.md)
- [Sync Engine](sync-engine.md)
