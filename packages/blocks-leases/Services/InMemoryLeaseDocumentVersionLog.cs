using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Leases.Models;

namespace Sunfish.Blocks.Leases.Services;

/// <summary>
/// In-memory <see cref="ILeaseDocumentVersionLog"/>. Per-lease list
/// backed by <see cref="ConcurrentDictionary{TKey, TValue}"/>; assigns
/// monotonically-increasing version numbers + stable ids on append.
/// Append-only — there is no Update or Delete operation.
/// </summary>
public sealed class InMemoryLeaseDocumentVersionLog : ILeaseDocumentVersionLog
{
    private readonly ConcurrentDictionary<LeaseId, List<LeaseDocumentVersion>> _byLease = new();

    /// <inheritdoc />
    public Task<LeaseDocumentVersion> AppendAsync(LeaseDocumentVersion entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ct.ThrowIfCancellationRequested();

        var bucket = _byLease.GetOrAdd(entry.Lease, _ => new List<LeaseDocumentVersion>());
        LeaseDocumentVersion stored;
        lock (bucket)
        {
            var nextVersion = bucket.Count + 1;
            stored = entry with
            {
                Id = new LeaseDocumentVersionId(Guid.NewGuid()),
                VersionNumber = nextVersion,
            };
            bucket.Add(stored);
        }
        return Task.FromResult(stored);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LeaseDocumentVersion> ListAsync(LeaseId lease, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_byLease.TryGetValue(lease, out var bucket))
        {
            yield break;
        }
        LeaseDocumentVersion[] snapshot;
        lock (bucket) { snapshot = bucket.OrderBy(v => v.VersionNumber).ToArray(); }
        foreach (var v in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return v;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public Task<LeaseDocumentVersion?> GetLatestAsync(LeaseId lease, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!_byLease.TryGetValue(lease, out var bucket))
        {
            return Task.FromResult<LeaseDocumentVersion?>(null);
        }
        LeaseDocumentVersion? latest;
        lock (bucket)
        {
            latest = bucket.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        }
        return Task.FromResult(latest);
    }

    /// <inheritdoc />
    public Task<LeaseDocumentVersion?> GetAsync(LeaseDocumentVersionId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        foreach (var bucket in _byLease.Values)
        {
            LeaseDocumentVersion? found;
            lock (bucket)
            {
                found = bucket.FirstOrDefault(v => v.Id == id);
            }
            if (found is not null)
            {
                return Task.FromResult<LeaseDocumentVersion?>(found);
            }
        }
        return Task.FromResult<LeaseDocumentVersion?>(null);
    }
}
