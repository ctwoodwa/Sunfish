namespace Sunfish.Foundation.Blobs;

/// <summary>
/// Content-addressed binary storage. Every blob is identified by a <see cref="Cid"/>;
/// identical bytes always produce the same CID (automatic deduplication). Backends
/// may persist to the filesystem, S3, Postgres, or IPFS; the contract is the same.
/// </summary>
/// <remarks>
/// Out of scope at this primitive layer:
/// <list type="bullet">
///   <item>Encryption — callers that need confidentiality encrypt bytes before <see cref="PutAsync"/>
///   and decrypt after <see cref="GetAsync"/>. Keys come from the capability layer (spec §10.2).</item>
///   <item>Access control — anyone who knows a CID and has network access to the backend
///   can fetch the blob. Confidentiality is encryption or private-network deployment.</item>
///   <item>Versioning — blobs are immutable. "Updating" means producing a new CID;
///   mutability lives at the entity layer (the entity updates its CID reference).</item>
/// </list>
/// See <c>docs/specifications/sunfish-platform-specification.md</c> §3.7 for the full primitive.
/// </remarks>
public interface IBlobStore
{
    /// <summary>
    /// Stores the bytes and returns the canonical CID. Idempotent — calling twice with the
    /// same content is a no-op on the backend (same CID, same storage slot).
    /// </summary>
    ValueTask<Cid> PutAsync(ReadOnlyMemory<byte> content, CancellationToken ct = default);

    /// <summary>
    /// Stores content from a <see cref="Stream"/> without requiring the caller to buffer the
    /// entire payload in memory first. Returns the canonical CID once all bytes are consumed.
    /// Idempotent — calling twice with the same content produces the same CID.
    /// </summary>
    /// <remarks>
    /// The default interface implementation reads the entire stream into a <see cref="MemoryStream"/>
    /// and delegates to <see cref="PutAsync(ReadOnlyMemory{byte}, CancellationToken)"/>. This keeps
    /// every backend functional without source changes. Backends that can stream natively (e.g.
    /// <see cref="FileSystemBlobStore"/>) override this method to avoid the memory copy.
    ///
    /// Follow-up work (G22 subsequent passes):
    /// <list type="bullet">
    ///   <item>IpfsBlobStore — requires Kubo multipart/streaming RPC mechanics.</item>
    ///   <item>S3 backend (if added) — use multipart upload.</item>
    /// </list>
    /// </remarks>
    virtual async ValueTask<Cid> PutStreamingAsync(Stream content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        return await PutAsync(ms.ToArray(), ct);
    }

    /// <summary>
    /// Retrieves bytes by CID. Returns <see langword="null"/> if the blob is not locally
    /// available (implementations may or may not attempt remote fetch depending on backend).
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>?> GetAsync(Cid cid, CancellationToken ct = default);

    /// <summary>
    /// Returns <see langword="true"/> if the blob is locally pinned (guaranteed retrievable
    /// on the next <see cref="GetAsync"/> call without a remote fetch).
    /// </summary>
    ValueTask<bool> ExistsLocallyAsync(Cid cid, CancellationToken ct = default);

    /// <summary>
    /// Marks the blob as retained. Unpinned blobs are eligible for garbage collection by the
    /// backend's reclamation policy. Idempotent — pinning an already-pinned CID is a no-op.
    /// </summary>
    ValueTask PinAsync(Cid cid, CancellationToken ct = default);

    /// <summary>
    /// Removes the retention mark. The backend may GC the blob at any later time.
    /// Does not delete synchronously. Idempotent.
    /// </summary>
    ValueTask UnpinAsync(Cid cid, CancellationToken ct = default);
}
