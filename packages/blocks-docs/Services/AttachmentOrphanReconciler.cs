using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Periodic reconciler that tombstones Active attachments with zero
/// live <see cref="DocumentRef"/>s pointing at them — the catalog-side
/// of orphan-blob GC. The actual blob bytes are reclaimed by a
/// downstream FoundationBlob pass that walks tombstoned rows past
/// retention; this reconciler is responsible only for the status flip
/// from Active → Tombstoned.
///
/// <para>
/// <b>Why a reconciler, not a hook.</b> A reference-counting hook on
/// every <c>SoftDeleteAsync</c> would couple <see cref="IDocumentRefService"/>
/// to the attachment-status path and force the wrong order when a
/// consumer cluster's reconciler tombstones N links in a loop — each
/// link's tombstone would re-check the count and the LAST link's
/// tombstone would flip the attachment. That works, but a periodic
/// scan is simpler, decoupled, and idempotent: a tenant's attachment
/// store converges to "no orphans" after every pass regardless of
/// what link events were emitted in any order.
/// </para>
///
/// <para>
/// <b>Safety posture.</b> The reconciler only tombstones Active rows
/// with zero live links. It NEVER touches Tombstoned or Superseded
/// rows (those are already off the GC path). The
/// <see cref="GracePeriod"/> protects freshly-uploaded attachments
/// that haven't yet been linked — a host that uploads first and links
/// second would otherwise see the in-flight upload get reaped between
/// upload and link.
/// </para>
/// </summary>
public sealed class AttachmentOrphanReconciler
{
    private readonly IAttachmentRepository _attachments;
    private readonly IDocumentRefService _documentRefs;
    private readonly TimeSpan _gracePeriod;

    /// <summary>
    /// The minimum age an Active attachment must be before the
    /// reconciler considers it for orphan tombstoning. Protects the
    /// upload → link race window.
    /// </summary>
    public TimeSpan GracePeriod => _gracePeriod;

    public AttachmentOrphanReconciler(
        IAttachmentRepository attachments,
        IDocumentRefService documentRefs,
        TimeSpan? gracePeriod = null)
    {
        _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
        _documentRefs = documentRefs ?? throw new ArgumentNullException(nameof(documentRefs));
        // 15-minute default: long enough for the upload→link round-trip
        // even on slow hosts; short enough that a forgotten link doesn't
        // burn quota indefinitely.
        _gracePeriod = gracePeriod ?? TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// One reconciliation pass for a single tenant. Walks every Active
    /// attachment older than the <see cref="GracePeriod"/>, asks the
    /// document-ref service for its live-link count, and tombstones
    /// every attachment with a count of zero.
    /// </summary>
    /// <returns>
    /// The number of attachments tombstoned in this pass. Idempotent:
    /// a second call immediately after the first returns 0.
    /// </returns>
    public async Task<int> ReconcileTenantAsync(
        TenantId tenantId,
        string actor,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var cutoff = new Instant(Instant.Now.Value - _gracePeriod);
        var live = await _attachments.ListByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var tombstoned = 0;
        foreach (var a in live)
        {
            // Only Active rows past the grace window are candidates.
            if (a.Status != AttachmentStatus.Active) continue;
            if (a.CreatedAtUtc.Value > cutoff.Value) continue;

            var liveLinkCount = await _documentRefs.CountLiveLinksToAttachmentAsync(tenantId, a.Id, cancellationToken)
                .ConfigureAwait(false);
            if (liveLinkCount > 0) continue;

            // Zero live links + past grace → orphan. Soft-delete the catalog row;
            // blob-bytes reclamation is the downstream FoundationBlob pass.
            if (await _attachments.SoftDeleteAsync(a.Id, actor, reason ?? "orphan-gc", cancellationToken).ConfigureAwait(false))
                tombstoned++;
        }
        return tombstoned;
    }
}
