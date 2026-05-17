using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Default <see cref="IDocumentRefService"/>. Wraps
/// <see cref="IDocumentRefRepository"/> with idempotent-link logic +
/// role updates + cross-tenant safety.
///
/// <para>
/// <b>Reconciliation flow.</b> Consumer clusters that hard-delete a
/// parent entity (e.g., invoice voided + purged) should call
/// <see cref="DocumentRefReconciler.TombstoneParentLinksAsync"/> in
/// the same transaction. The service path here is for the live
/// link / unlink mainline; bulk tombstone is the reconciler's domain.
/// </para>
/// </summary>
public sealed class DocumentRefService : IDocumentRefService
{
    private readonly IDocumentRefRepository _refs;
    private readonly IAttachmentRepository _attachments;

    public DocumentRefService(
        IDocumentRefRepository refs,
        IAttachmentRepository attachments)
    {
        _refs = refs ?? throw new ArgumentNullException(nameof(refs));
        _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
    }

    /// <inheritdoc />
    public async Task<DocumentRef> LinkAsync(
        TenantId tenantId,
        AttachmentId attachmentId,
        string clusterCode,
        string parentEntityType,
        string parentEntityId,
        string actor,
        string? attachmentRole = null,
        CancellationToken cancellationToken = default)
    {
        // Tenant-scope safety: confirm the attachment is owned by the caller's tenant.
        var attachment = await _attachments.GetAsync(attachmentId, cancellationToken).ConfigureAwait(false);
        if (attachment is null)
            throw new InvalidOperationException($"Attachment '{attachmentId.Value}' does not exist or is tombstoned.");
        if (attachment.TenantId != tenantId)
            throw new InvalidOperationException(
                $"Cross-tenant link rejected: caller tenant != attachment tenant.");

        // Idempotency: is there already a live link for this parent → attachment?
        var existing = await _refs.FindByParentAsync(tenantId, clusterCode, parentEntityType, parentEntityId, cancellationToken)
            .ConfigureAwait(false);
        var match = existing.FirstOrDefault(r => r.AttachmentId == attachmentId);
        if (match is not null)
        {
            // Role differs → update in place. Otherwise return as-is.
            if (!string.Equals(match.AttachmentRole, attachmentRole, StringComparison.Ordinal))
            {
                var now = Instant.Now;
                var updated = match with
                {
                    AttachmentRole = attachmentRole,
                    UpdatedAtUtc = now,
                    UpdatedBy = actor,
                    Version = match.Version + 1,
                };
                await _refs.UpsertAsync(updated, cancellationToken).ConfigureAwait(false);
                return updated;
            }
            return match;
        }

        var fresh = DocumentRef.Create(
            tenantId: tenantId,
            attachmentId: attachmentId,
            clusterCode: clusterCode,
            parentEntityType: parentEntityType,
            parentEntityId: parentEntityId,
            createdBy: actor,
            attachmentRole: attachmentRole);
        await _refs.UpsertAsync(fresh, cancellationToken).ConfigureAwait(false);
        return fresh;
    }

    /// <inheritdoc />
    public async Task<bool> UnlinkAsync(
        TenantId tenantId,
        AttachmentId attachmentId,
        string clusterCode,
        string parentEntityType,
        string parentEntityId,
        string actor,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var hits = await _refs.FindByParentAsync(tenantId, clusterCode, parentEntityType, parentEntityId, cancellationToken)
            .ConfigureAwait(false);
        var match = hits.FirstOrDefault(r => r.AttachmentId == attachmentId);
        if (match is null) return false;
        return await _refs.SoftDeleteAsync(match.Id, actor, reason, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountLiveLinksToAttachmentAsync(
        TenantId tenantId,
        AttachmentId attachmentId,
        CancellationToken cancellationToken = default)
    {
        var hits = await _refs.FindByAttachmentAsync(tenantId, attachmentId, cancellationToken)
            .ConfigureAwait(false);
        return hits.Count;
    }
}
