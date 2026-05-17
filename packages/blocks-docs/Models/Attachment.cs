using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Models;

/// <summary>
/// A canonical attachment — the file itself + its catalog metadata. Each
/// row is **immutable post-upload**: a "new version" is a fresh
/// <see cref="Attachment"/> with <see cref="ReplacesAttachmentId"/>
/// pointing at the prior row, whose <see cref="Status"/> flips to
/// <see cref="AttachmentStatus.Superseded"/>.
///
/// <para>
/// <b>Content-hash deduplication.</b> <see cref="ContentHash"/> is the
/// lowercase-hex sha-256 of the underlying bytes. Two uploads with the
/// same content land on different <see cref="AttachmentId"/>s (so the
/// owner / consumer context is preserved per upload) but point at the
/// same blob — PR 2's service does the dedup on the way to PR 3's
/// <c>IBlobStore</c>.
/// </para>
///
/// <para>
/// <b>Filename safety.</b> <see cref="OriginalFilename"/> is for display
/// only; the service-layer path-sanitizes it on the way in (no slashes,
/// no path traversal, no shell-escape characters). Trust the
/// <see cref="MimeType"/> field, not the filename extension.
/// </para>
/// </summary>
public sealed record Attachment
{
    /// <summary>Stable identifier.</summary>
    public required AttachmentId Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Where the blob lives — discriminated union over the three storage tiers.</summary>
    public required StorageRef StorageRef { get; init; }

    /// <summary>Lowercase-hex sha-256 of the underlying bytes.</summary>
    public required string ContentHash { get; init; }

    /// <summary>Server-sniffed MIME type. Filename extension is not trusted.</summary>
    public required string MimeType { get; init; }

    /// <summary>Bytes, as observed at upload time.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Display-only original filename (service path-sanitizes on the way in).</summary>
    public required string OriginalFilename { get; init; }

    /// <summary>Optional thumbnail blob — null in v1 (thumbnail generation is a follow-on).</summary>
    public StorageRef? ThumbnailRef { get; init; }

    /// <summary>Sensitivity classification — drives sharing / export policy.</summary>
    public Sensitivity Sensitivity { get; init; } = Sensitivity.Internal;

    /// <summary>Lifecycle state.</summary>
    public required AttachmentStatus Status { get; init; }

    /// <summary>Optional pointer to a prior attachment this one replaces.</summary>
    public AttachmentId? ReplacesAttachmentId { get; init; }

    /// <summary>Optional back-pointer to the attachment that replaced this one.</summary>
    public AttachmentId? ReplacedByAttachmentId { get; init; }

    // ── CRDT envelope ──
    public required Instant CreatedAtUtc { get; init; }
    public string? CreatedBy { get; init; }
    public Instant UpdatedAtUtc { get; init; }
    public string? UpdatedBy { get; init; }
    public Instant? DeletedAtUtc { get; init; }
    public string? DeletedBy { get; init; }
    public string? DeletedReason { get; init; }
    public required long Version { get; init; }
    public IReadOnlyDictionary<string, long> RevisionVector { get; init; }
        = new Dictionary<string, long>();

    /// <summary>
    /// Construct a freshly-uploaded attachment. PR 2's service is the
    /// expected caller; this static helper exists so tests and importers
    /// can also build canonical instances without manual envelope wiring.
    /// </summary>
    public static Attachment Create(
        TenantId tenantId,
        StorageRef storageRef,
        string contentHash,
        string mimeType,
        long sizeBytes,
        string originalFilename,
        string? createdBy = null,
        AttachmentId? id = null,
        Sensitivity sensitivity = Sensitivity.Internal,
        Instant? createdAtUtc = null)
    {
        if (storageRef is null) throw new ArgumentNullException(nameof(storageRef));
        if (string.IsNullOrWhiteSpace(contentHash))
            throw new ArgumentException("ContentHash is required.", nameof(contentHash));
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MimeType is required.", nameof(mimeType));
        if (sizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "SizeBytes must be non-negative.");

        var now = createdAtUtc ?? Instant.Now;
        return new Attachment
        {
            Id = id ?? AttachmentId.NewId(),
            TenantId = tenantId,
            StorageRef = storageRef,
            ContentHash = contentHash,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            OriginalFilename = originalFilename,
            Sensitivity = sensitivity,
            Status = AttachmentStatus.Active,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            Version = 1,
        };
    }
}
