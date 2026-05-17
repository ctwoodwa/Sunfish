namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Thrown by <see cref="IAttachmentService.UploadAsync"/> when the
/// three-gate <see cref="IMimeTypeAndSizePolicy"/> rejects the upload.
/// Carries the structured rejection reason so callers can branch
/// (return 415 vs 413 vs 507 on an HTTP surface, e.g.).
/// </summary>
public sealed class UploadRejectedException : Exception
{
    /// <summary>Which gate rejected the upload.</summary>
    public PolicyRejection RejectionReason { get; }

    /// <summary>Construct from a structured reason + human-readable detail.</summary>
    public UploadRejectedException(PolicyRejection rejectionReason, string detail)
        : base(detail)
    {
        RejectionReason = rejectionReason;
    }
}
