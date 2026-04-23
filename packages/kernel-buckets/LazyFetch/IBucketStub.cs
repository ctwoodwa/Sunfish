namespace Sunfish.Kernel.Buckets.LazyFetch;

/// <summary>
/// Paper §10.3 stub representation: a placeholder for a record in a lazy-replicated bucket.
/// Carries identifier, last-modified metadata, and a content hash for integrity verification
/// when the full content is ultimately fetched.
/// </summary>
/// <param name="RecordId">Unique record identifier within the bucket's namespace.</param>
/// <param name="BucketName">Bucket this stub belongs to (cross-ref <see cref="BucketDefinition.Name"/>).</param>
/// <param name="LastModified">Wall-clock time (UTC) of the most recent edit observed via gossip.</param>
/// <param name="ContentHash">
/// Cryptographic hash of the full record body (SHA-256 recommended). Used to verify re-fetched
/// content matches what the gossip metadata advertised (paper §10.3: "Content hashes verify
/// re-fetched records.").
/// </param>
/// <param name="ContentLengthBytes">Length of the full record body in bytes, for budget accounting.</param>
public sealed record BucketStub(
    string RecordId,
    string BucketName,
    DateTimeOffset LastModified,
    byte[] ContentHash,
    long ContentLengthBytes);

/// <summary>
/// In-memory store of lazy-bucket stubs. Real fetch-from-peer is deferred to a later wave
/// (wires into the gossip / sync daemon). This surface is the local index the sync daemon
/// queries when deciding "do I already have a stub for this record id?".
/// </summary>
public interface IBucketStubStore
{
    /// <summary>Upsert a stub.</summary>
    /// <returns><c>true</c> if a new stub was added; <c>false</c> if an existing stub was replaced.</returns>
    bool Upsert(BucketStub stub);

    /// <summary>Look up a stub by (bucket, record-id). Returns null if absent.</summary>
    BucketStub? Find(string bucketName, string recordId);

    /// <summary>Enumerate all stubs belonging to the named bucket.</summary>
    IReadOnlyList<BucketStub> ListByBucket(string bucketName);

    /// <summary>Remove a stub. Returns whether anything was removed.</summary>
    bool Remove(string bucketName, string recordId);
}
