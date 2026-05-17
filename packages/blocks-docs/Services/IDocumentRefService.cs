using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Service-layer surface over <see cref="IDocumentRefRepository"/>.
/// Consumer clusters (blocks-financial-ar, blocks-financial-ap,
/// blocks-leases, …) call this rather than touching the repository
/// directly — the service folds in idempotency + role-conflict
/// resolution + orphan-detection helpers that the repository contract
/// deliberately keeps out.
///
/// <para>
/// <b>Idempotency contract.</b> <see cref="LinkAsync"/> with the same
/// <c>(tenant, attachment, cluster, parent-type, parent-id)</c> tuple
/// returns the existing live <see cref="DocumentRef"/> rather than
/// creating a duplicate. This lets cluster handlers idempotently
/// re-emit "attach this PDF to this invoice" without needing to track
/// whether the link already exists. The role hint is updated in-place
/// when it changes — older role wins is a footgun for renames.
/// </para>
///
/// <para>
/// <b>Tenant scope.</b> Every method requires a <see cref="TenantId"/>.
/// The service rejects cross-tenant operations (link from tenant A to
/// attachment owned by tenant B) by failing the link with
/// <see cref="InvalidOperationException"/>. The repository alone can't
/// catch this — it doesn't know the attachment's tenant — so the check
/// lives here.
/// </para>
/// </summary>
public interface IDocumentRefService
{
    /// <summary>
    /// Create or re-find the link from <paramref name="parentEntityId"/>
    /// in <paramref name="clusterCode"/>/<paramref name="parentEntityType"/>
    /// to <paramref name="attachmentId"/>. Idempotent: a second call
    /// returns the existing row. When <paramref name="attachmentRole"/>
    /// differs from the stored role, the row's role is updated.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the attachment is missing, tombstoned, or owned by a
    /// different tenant than the caller.
    /// </exception>
    Task<DocumentRef> LinkAsync(
        TenantId tenantId,
        AttachmentId attachmentId,
        string clusterCode,
        string parentEntityType,
        string parentEntityId,
        string actor,
        string? attachmentRole = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tombstone the link identified by the <c>(tenant, attachment, cluster,
    /// parent-type, parent-id)</c> tuple. Idempotent: returns true when a
    /// live link was tombstoned, false when none existed (or it was already
    /// tombstoned).
    /// </summary>
    Task<bool> UnlinkAsync(
        TenantId tenantId,
        AttachmentId attachmentId,
        string clusterCode,
        string parentEntityType,
        string parentEntityId,
        string actor,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count the live <see cref="DocumentRef"/>s pointing at
    /// <paramref name="attachmentId"/>. Used by the orphan-blob GC pass
    /// — when the count reaches zero, the underlying blob is a candidate
    /// for cleanup (subject to retention policy, tombstone grace, etc.).
    /// </summary>
    Task<int> CountLiveLinksToAttachmentAsync(
        TenantId tenantId,
        AttachmentId attachmentId,
        CancellationToken cancellationToken = default);
}
