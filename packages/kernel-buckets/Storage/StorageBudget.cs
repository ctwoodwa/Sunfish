using Sunfish.Kernel.Buckets.LazyFetch;

namespace Sunfish.Kernel.Buckets.Storage;

/// <summary>
/// Paper §10.3 local storage-budget state. Default 10 GB per the paper.
/// </summary>
public sealed class StorageBudget
{
    /// <summary>Maximum bytes the node is willing to spend on cached record bodies.</summary>
    public long MaxBytes { get; set; } = 10L * 1024 * 1024 * 1024; // 10 GB per paper §10.3

    /// <summary>Bytes currently consumed by cached content. Updated by the budget manager.</summary>
    public long CurrentBytes { get; internal set; }

    /// <summary>True when usage exceeds 90% of <see cref="MaxBytes"/>; a cue to begin eviction.</summary>
    public bool NearLimit => CurrentBytes > MaxBytes * 0.9;

    /// <summary>True when usage has reached or exceeded <see cref="MaxBytes"/>.</summary>
    public bool OverLimit => CurrentBytes >= MaxBytes;
}

/// <summary>
/// A tracked record body for eviction purposes. Held by an <see cref="IStorageBudgetManager"/>.
/// </summary>
/// <param name="BucketName">Owning bucket.</param>
/// <param name="RecordId">Record identifier within the bucket.</param>
/// <param name="ContentLengthBytes">Bytes this record contributes to the budget.</param>
/// <param name="LastAccessed">Wall-clock time of the most recent read; drives LRU ordering.</param>
public sealed record TrackedRecord(
    string BucketName,
    string RecordId,
    long ContentLengthBytes,
    DateTimeOffset LastAccessed);

/// <summary>
/// Paper §10.3 storage-budget manager. Tracks which records currently consume the local budget,
/// and exposes an LRU eviction primitive that the sync daemon calls when
/// <see cref="StorageBudget.NearLimit"/> fires.
/// </summary>
/// <remarks>
/// <para>
/// Eviction only removes full content from records whose bucket is
/// <see cref="ReplicationMode.Lazy"/> — paper §10.3: "least-recently-used records in lazy buckets
/// are evicted; stubs are retained." Stubs live in <see cref="IBucketStubStore"/> and are not
/// touched here.
/// </para>
/// </remarks>
public interface IStorageBudgetManager
{
    /// <summary>The current budget snapshot.</summary>
    StorageBudget Current { get; }

    /// <summary>
    /// Begin tracking a record body against the budget. Called by the ingest path after a record
    /// body is written to local storage.
    /// </summary>
    void Track(TrackedRecord record);

    /// <summary>Stop tracking (e.g. the record was deleted through normal means, not eviction).</summary>
    bool Untrack(string bucketName, string recordId);

    /// <summary>Refresh a record's <see cref="TrackedRecord.LastAccessed"/> to now.</summary>
    void TouchAccess(string bucketName, string recordId, DateTimeOffset now);

    /// <summary>
    /// Evict least-recently-used records from lazy buckets until at least
    /// <paramref name="bytesToReclaim"/> bytes have been freed, or no more lazy records remain.
    /// </summary>
    /// <param name="bytesToReclaim">Target number of bytes to reclaim.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Actual bytes reclaimed. May be less than requested if nothing lazy remains.</returns>
    Task<long> EvictLruAsync(long bytesToReclaim, CancellationToken ct);
}
