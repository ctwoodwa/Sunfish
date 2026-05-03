using System.Runtime.CompilerServices;
using System.Text.Json;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Audit;

/// <summary>Zero-dependency in-memory <see cref="IAuditLog"/>.</summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly InMemoryAssetStorage _storage;
    private readonly object _appendLock = new();
    private long _nextId;

    /// <summary>Creates an in-memory audit log backed by the given shared storage.</summary>
    public InMemoryAuditLog(InMemoryAssetStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <inheritdoc />
    public Task<AuditId> AppendAsync(AuditAppend append, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(append);

        // Serialize all appends so the monotonic id allocation + per-entity chain extension
        // stay in lock-step.
        lock (_appendLock)
        {
            var chain = _storage.Audit.GetOrAdd(append.EntityId, static _ => new List<AuditRecord>());
            var prev = chain.Count > 0 ? chain[^1] : null;
            var prevHash = prev?.Hash;
            var prevId = prev?.Id;

            var hash = HashChain.ComputeHash(
                prevHash,
                append.EntityId,
                append.Op,
                append.Actor,
                append.Tenant,
                append.At,
                append.Payload);

            var id = new AuditId(Interlocked.Increment(ref _nextId));
            var record = new AuditRecord(
                Id: id,
                EntityId: append.EntityId,
                VersionId: append.VersionId,
                Op: append.Op,
                Actor: append.Actor,
                Tenant: append.Tenant,
                At: append.At,
                Justification: append.Justification,
                Payload: ClonePayload(append.Payload),
                Signature: append.Signature,
                Prev: prevId,
                Hash: hash);

            chain.Add(record);
            return Task.FromResult(id);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        IEnumerable<AuditRecord> source;
        if (query.Entity is { } entity)
        {
            source = _storage.Audit.TryGetValue(entity, out var chain)
                ? chain.ToArray()
                : Array.Empty<AuditRecord>();
        }
        else
        {
            source = _storage.Audit.Values.SelectMany(static c => c.ToArray());
        }

        var filtered = source
            .Where(r => query.Actor is not { } actor || r.Actor == actor)
            .Where(r => query.Tenant is not { } tenant || r.Tenant == tenant)
            .Where(r => query.Op is not { } op || r.Op == op)
            .Where(r => query.FromInclusive is not { } from || r.At >= from)
            .Where(r => query.ToExclusive is not { } to || r.At < to)
            .OrderBy(r => r.At)
            .ThenBy(r => r.Id.Value);

        int emitted = 0;
        foreach (var record in filtered)
        {
            ct.ThrowIfCancellationRequested();
            yield return record;
            emitted++;
            if (query.Limit is { } limit && emitted >= limit) yield break;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public Task<bool> VerifyChainAsync(EntityId entity, CancellationToken ct = default)
    {
        if (!_storage.Audit.TryGetValue(entity, out var chain))
            return Task.FromResult(true);
        var snapshot = chain.OrderBy(r => r.At).ThenBy(r => r.Id.Value).ToList();
        return Task.FromResult(HashChain.Verify(snapshot));
    }

    private static JsonDocument ClonePayload(JsonDocument payload)
        => JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(payload.RootElement));
}
