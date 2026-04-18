using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Federation.EntitySync;

/// <summary>
/// Summary of the outcome of a sync round.
/// </summary>
/// <param name="ChangesTransferred">Count of new changes applied locally (pull) or shipped to peer (push).</param>
/// <param name="ChangesAlreadyPresent">Count of received changes that were skipped because the store already had them.</param>
/// <param name="ChangesRejected">Count of received changes that failed signature verification or other validation.</param>
/// <param name="Rejections">Per-change rejection reasons.</param>
public sealed record SyncResult(
    int ChangesTransferred,
    int ChangesAlreadyPresent,
    int ChangesRejected,
    IReadOnlyList<SyncRejection> Rejections);

/// <summary>
/// Records the rejection of a single change during a sync round.
/// </summary>
/// <param name="VersionId">The version that was rejected; <c>default</c> for non-change-specific rejections.</param>
/// <param name="Reason">Human-readable explanation (useful for logs and diagnostics).</param>
public sealed record SyncRejection(
    VersionId VersionId,
    string Reason);
