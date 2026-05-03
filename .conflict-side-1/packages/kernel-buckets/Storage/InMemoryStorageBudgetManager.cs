namespace Sunfish.Kernel.Buckets.Storage;

/// <summary>
/// In-memory reference <see cref="IStorageBudgetManager"/>. Suitable for tests and for the
/// initial non-persistent node host. A persistent manager (SQLite-backed) is future work.
/// </summary>
public sealed class InMemoryStorageBudgetManager : IStorageBudgetManager
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Bucket, string Record), TrackedRecord> _tracked = new();
    private readonly IBucketRegistry _registry;
    private readonly StorageBudget _budget;

    /// <summary>Construct with a default (10 GB) budget.</summary>
    public InMemoryStorageBudgetManager(IBucketRegistry registry)
        : this(registry, new StorageBudget()) { }

    /// <summary>Construct with a caller-supplied budget (e.g. for tests with a tight limit).</summary>
    public InMemoryStorageBudgetManager(IBucketRegistry registry, StorageBudget budget)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _budget = budget ?? throw new ArgumentNullException(nameof(budget));
    }

    /// <inheritdoc />
    public StorageBudget Current => _budget;

    /// <inheritdoc />
    public void Track(TrackedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrEmpty(record.BucketName);
        ArgumentException.ThrowIfNullOrEmpty(record.RecordId);
        if (record.ContentLengthBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(record), "ContentLengthBytes must be >= 0.");
        }

        lock (_gate)
        {
            var key = (record.BucketName, record.RecordId);
            if (_tracked.TryGetValue(key, out var existing))
            {
                _budget.CurrentBytes -= existing.ContentLengthBytes;
            }
            _tracked[key] = record;
            _budget.CurrentBytes += record.ContentLengthBytes;
        }
    }

    /// <inheritdoc />
    public bool Untrack(string bucketName, string recordId)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(recordId);

        lock (_gate)
        {
            var key = (bucketName, recordId);
            if (!_tracked.TryGetValue(key, out var existing))
            {
                return false;
            }
            _budget.CurrentBytes -= existing.ContentLengthBytes;
            _tracked.Remove(key);
            return true;
        }
    }

    /// <inheritdoc />
    public void TouchAccess(string bucketName, string recordId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(recordId);

        lock (_gate)
        {
            var key = (bucketName, recordId);
            if (_tracked.TryGetValue(key, out var existing))
            {
                _tracked[key] = existing with { LastAccessed = now };
            }
        }
    }

    /// <inheritdoc />
    public Task<long> EvictLruAsync(long bytesToReclaim, CancellationToken ct)
    {
        if (bytesToReclaim <= 0)
        {
            return Task.FromResult(0L);
        }

        long reclaimed = 0;
        lock (_gate)
        {
            // Identify lazy-bucket records, oldest access first.
            var candidates = _tracked.Values
                .Where(r => IsLazyBucket(r.BucketName))
                .OrderBy(r => r.LastAccessed)
                .ToList();

            foreach (var candidate in candidates)
            {
                if (reclaimed >= bytesToReclaim) break;
                ct.ThrowIfCancellationRequested();

                var key = (candidate.BucketName, candidate.RecordId);
                if (_tracked.Remove(key))
                {
                    _budget.CurrentBytes -= candidate.ContentLengthBytes;
                    reclaimed += candidate.ContentLengthBytes;
                }
            }
        }
        return Task.FromResult(reclaimed);
    }

    private bool IsLazyBucket(string bucketName)
    {
        var def = _registry.Find(bucketName);
        return def is not null && def.Replication == ReplicationMode.Lazy;
    }
}
