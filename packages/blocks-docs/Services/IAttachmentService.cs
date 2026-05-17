using Sunfish.Blocks.Docs.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Upload + version surface for documents. Wraps
/// <see cref="IAttachmentRepository"/> with content-hash deduplication
/// (a second upload of the same bytes within a tenant reuses the
/// existing <see cref="StorageRef"/>) and the
/// <see cref="AttachmentStatus.Superseded"/> replacement-chain
/// discipline.
///
/// <para>
/// <b>PR 2 storage tier:</b> Inline only —
/// <see cref="StorageRef.ForInline"/> for every upload. PR 3
/// (council-gated) introduces <see cref="StorageRefKind.FoundationBlob"/>
/// wiring + the MIME / size policy; this service's surface doesn't
/// change when that lands.
/// </para>
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Upload a fresh attachment. If an existing live attachment in the
    /// same tenant has the same content hash, returns the existing
    /// record (no new row, no duplicate blob); otherwise inserts a new
    /// <see cref="Attachment"/>.
    /// </summary>
    /// <param name="tenantId">Tenant scope.</param>
    /// <param name="bytes">Payload.</param>
    /// <param name="mimeType">Server-supplied MIME (PR 3 wires the sniffer).</param>
    /// <param name="originalFilename">Display-only filename.</param>
    /// <param name="createdBy">Opaque actor handle (typically a <c>PartyId.Value</c> from blocks-people-foundation).</param>
    /// <param name="sensitivity">Sensitivity classification.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<Attachment> UploadAsync(
        TenantId tenantId,
        ReadOnlyMemory<byte> bytes,
        string mimeType,
        string originalFilename,
        string createdBy,
        Sensitivity sensitivity = Sensitivity.Internal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supersede an existing attachment with a new version. Inserts a
    /// new attachment row pointing at <paramref name="priorAttachmentId"/>
    /// via <see cref="Attachment.ReplacesAttachmentId"/>, flips the
    /// prior row to <see cref="AttachmentStatus.Superseded"/> + sets
    /// its <see cref="Attachment.ReplacedByAttachmentId"/> back-pointer.
    ///
    /// <para>
    /// If the new bytes are content-identical to another live
    /// attachment in the tenant, the deduplication still applies — the
    /// caller gets back the existing row, and the prior is superseded
    /// against it.
    /// </para>
    /// </summary>
    Task<Attachment> SupersedeAsync(
        AttachmentId priorAttachmentId,
        ReadOnlyMemory<byte> bytes,
        string mimeType,
        string originalFilename,
        string updatedBy,
        Sensitivity? sensitivity = null,
        CancellationToken cancellationToken = default);
}
