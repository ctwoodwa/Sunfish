namespace Sunfish.Kernel.Buckets.LazyFetch;

/// <summary>
/// Default in-memory implementation of <see cref="IBucketStubStore"/>. Keyed by
/// <c>(bucketName, recordId)</c>. Thread-safe for concurrent access.
/// </summary>
public sealed class InMemoryBucketStubStore : IBucketStubStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Bucket, string Record), BucketStub> _stubs = new();

    /// <inheritdoc />
    public bool Upsert(BucketStub stub)
    {
        ArgumentNullException.ThrowIfNull(stub);
        ArgumentException.ThrowIfNullOrEmpty(stub.BucketName);
        ArgumentException.ThrowIfNullOrEmpty(stub.RecordId);

        lock (_gate)
        {
            var key = (stub.BucketName, stub.RecordId);
            var isNew = !_stubs.ContainsKey(key);
            _stubs[key] = stub;
            return isNew;
        }
    }

    /// <inheritdoc />
    public BucketStub? Find(string bucketName, string recordId)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(recordId);

        lock (_gate)
        {
            return _stubs.TryGetValue((bucketName, recordId), out var s) ? s : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<BucketStub> ListByBucket(string bucketName)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);

        lock (_gate)
        {
            return _stubs
                .Where(kv => string.Equals(kv.Key.Bucket, bucketName, StringComparison.Ordinal))
                .Select(kv => kv.Value)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public bool Remove(string bucketName, string recordId)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucketName);
        ArgumentException.ThrowIfNullOrEmpty(recordId);

        lock (_gate)
        {
            return _stubs.Remove((bucketName, recordId));
        }
    }
}
