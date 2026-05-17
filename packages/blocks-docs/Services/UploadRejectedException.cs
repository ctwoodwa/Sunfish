using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Thrown by <see cref="IAttachmentService.UploadAsync"/> when the
/// three-gate <see cref="IMimeTypeAndSizePolicy"/> rejects the upload.
/// Carries the structured rejection reason so callers can branch
/// (return 415 vs 413 vs 507 on an HTTP surface, e.g.).
///
/// <para>
/// <b>SE-6 council amendment.</b> The public <see cref="Exception.Message"/>
/// and <see cref="Detail"/> fields are scrubbed of tenant identifiers —
/// they contain only the actionable rejection reason and the sniffed
/// MIME / sizes. Tenant scope is recovered from the internal-only
/// <see cref="TenantIdInternal"/> field (for audit-log correlation)
/// and never exposed to a remote caller.
/// </para>
/// </summary>
public sealed class UploadRejectedException : Exception
{
    /// <summary>Which gate rejected the upload.</summary>
    public PolicyRejection RejectionReason { get; }

    /// <summary>The scrubbed (tenant-id-free) detail string. Safe to surface to remote callers.</summary>
    public string Detail => Message;

    /// <summary>
    /// Tenant id captured for audit-log correlation. NOT included in
    /// <see cref="Exception.Message"/> / <see cref="Detail"/>. Hosting agents
    /// should write this to an internal log channel only.
    /// </summary>
    public TenantId? TenantIdInternal { get; }

    /// <summary>Construct from a structured reason + scrubbed human-readable detail.</summary>
    public UploadRejectedException(PolicyRejection rejectionReason, string detail, TenantId? tenantIdInternal = null)
        : base(detail)
    {
        RejectionReason = rejectionReason;
        TenantIdInternal = tenantIdInternal;
    }
}
