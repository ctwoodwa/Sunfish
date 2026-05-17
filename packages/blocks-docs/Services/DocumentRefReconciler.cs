using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Reconciler hooks for consumer clusters that hard-delete parent
/// entities. When an invoice / lease / inspection / work-order / bill
/// is removed by its owning cluster, the cluster's handler calls this
/// to tombstone every live <see cref="DocumentRef"/> still pointing at
/// the removed parent — preventing the join table from accumulating
/// "live links to nothing."
///
/// <para>
/// <b>Why a separate class.</b> <see cref="IDocumentRefService"/> is
/// the mainline link / unlink surface; bulk tombstone is a different
/// shape (one parent, N links). Keeping it in a dedicated reconciler
/// keeps the service interface tight and makes the cluster wiring
/// explicit ("hard-delete? call the reconciler").
/// </para>
/// </summary>
public sealed class DocumentRefReconciler
{
    private readonly IDocumentRefRepository _refs;

    public DocumentRefReconciler(IDocumentRefRepository refs)
    {
        _refs = refs ?? throw new ArgumentNullException(nameof(refs));
    }

    /// <summary>
    /// Tombstone every live <see cref="DocumentRef"/> for the given
    /// parent. Idempotent: a second call (after the first has tombstoned
    /// everything) returns 0. The <see cref="Attachment"/>s themselves
    /// are untouched — the caller decides via
    /// <see cref="IDocumentRefService.CountLiveLinksToAttachmentAsync"/>
    /// whether the underlying blob is now an orphan candidate for GC.
    /// </summary>
    /// <returns>The number of links tombstoned in this call.</returns>
    public async Task<int> TombstoneParentLinksAsync(
        TenantId tenantId,
        string clusterCode,
        string parentEntityType,
        string parentEntityId,
        string actor,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var live = await _refs.FindByParentAsync(tenantId, clusterCode, parentEntityType, parentEntityId, cancellationToken)
            .ConfigureAwait(false);
        var tombstoned = 0;
        foreach (var link in live)
        {
            if (await _refs.SoftDeleteAsync(link.Id, actor, reason, cancellationToken).ConfigureAwait(false))
                tombstoned++;
        }
        return tombstoned;
    }
}
