using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Catalog-side surface over <see cref="Attachment"/>. PR 1 ships the
/// contract; PR 2 ships <c>InMemoryAttachmentRepository</c> + the
/// content-hash dedup service that wraps it.
/// </summary>
public interface IAttachmentRepository
{
    /// <summary>Insert or update an attachment. Throws on tombstoned target.</summary>
    Task UpsertAsync(Attachment attachment, CancellationToken cancellationToken = default);

    /// <summary>Get an attachment by id. Returns null when missing or tombstoned.</summary>
    Task<Attachment?> GetAsync(AttachmentId id, CancellationToken cancellationToken = default);

    /// <summary>Find attachments matching a content-hash within a tenant — supports PR 2's dedup logic.</summary>
    Task<IReadOnlyList<Attachment>> FindByContentHashAsync(
        TenantId tenantId,
        string contentHash,
        CancellationToken cancellationToken = default);

    /// <summary>List all live (non-tombstoned) attachments in a tenant.</summary>
    Task<IReadOnlyList<Attachment>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tombstone an attachment (sets <c>DeletedAtUtc</c> + <c>DeletedReason</c> + status →
    /// <see cref="AttachmentStatus.Tombstoned"/>). Idempotent. Returns false when the id
    /// is unknown.
    /// </summary>
    Task<bool> SoftDeleteAsync(AttachmentId id, string actor, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sum of <see cref="Attachment.SizeBytes"/> across all live attachments
    /// in <paramref name="tenantId"/>. Supports PR 3's tenant-quota check.
    /// Excludes tombstoned + superseded rows so a tenant's quota reflects
    /// only their current "active" footprint.
    /// </summary>
    /// <remarks>
    /// <b>Council doc-amendment (council D).</b>
    /// <list type="bullet">
    /// <item><b>Active-only semantics.</b> Tombstoned + Superseded rows do
    /// NOT count toward the tenant's quota — they represent dead bytes
    /// that future GC will reclaim and shouldn't gate new uploads.</item>
    /// <item><b>Restore-from-tombstone must re-check.</b> A future API that
    /// can restore a tombstoned attachment to Active MUST re-run
    /// <see cref="IMimeTypeAndSizePolicy.ValidateAsync"/> against the
    /// current Active total at restore time. Otherwise the
    /// "tombstone-old → upload-new → restore-old" pattern lets a tenant
    /// exceed quota. PR 3 ships no public restore API, so this is a
    /// forward-looking contract.</item>
    /// <item><b>Best-effort posture.</b> The check is non-transactional —
    /// under extreme concurrency two uploads may both pass and jointly
    /// exceed the quota by one upload's worth. Multi-writer
    /// persistence-backed implementations MUST enforce quota
    /// transactionally or via a reserved-bytes counter.</item>
    /// </list>
    /// </remarks>
    Task<long> GetTenantTotalSizeBytesAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}
