using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Catalog-side surface over <see cref="DocumentRef"/>. Lets consumer
/// clusters create + look up cross-cluster attachment links without
/// importing <see cref="AttachmentService"/> — that would tangle the
/// cluster boundary.
///
/// <para>
/// PR 4 ships the contract + an in-memory implementation. PR 5 wires
/// the docs DI extension to register the repository alongside the
/// existing attachment surfaces; a persistence-backed implementation
/// lands when the foundation-postgres bridge is plumbed.
/// </para>
/// </summary>
public interface IDocumentRefRepository
{
    /// <summary>Insert or update a link. Throws when the row is tombstoned.</summary>
    Task UpsertAsync(DocumentRef documentRef, CancellationToken cancellationToken = default);

    /// <summary>Fetch a link by id. Returns null on missing (whether absent or tombstoned).</summary>
    Task<DocumentRef?> GetAsync(DocumentRefId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverse lookup: which parent entities point at this attachment? Used by
    /// the orphan-blob GC pass + by cluster UIs that need to render "also linked
    /// from N places." Excludes tombstoned links.
    /// </summary>
    Task<IReadOnlyList<DocumentRef>> FindByAttachmentAsync(
        TenantId tenantId,
        AttachmentId attachmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Forward lookup: which attachments does this parent entity own? Cluster
    /// consumers call this to render an entity's attachment list (e.g., invoice
    /// PDFs, lease addenda). Excludes tombstoned links.
    /// </summary>
    Task<IReadOnlyList<DocumentRef>> FindByParentAsync(
        TenantId tenantId,
        string clusterCode,
        string parentEntityType,
        string parentEntityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete a link (sets <c>DeletedAtUtc</c> + <c>DeletedBy</c> +
    /// <c>DeletedReason</c>). Idempotent. Returns false when the id is unknown
    /// or already tombstoned. The linked <see cref="Attachment"/> is untouched —
    /// the caller decides whether it should also be deleted (typically only when
    /// no other live <c>DocumentRef</c>s reference it).
    /// </summary>
    Task<bool> SoftDeleteAsync(
        DocumentRefId id,
        string actor,
        string? reason,
        CancellationToken cancellationToken = default);
}
