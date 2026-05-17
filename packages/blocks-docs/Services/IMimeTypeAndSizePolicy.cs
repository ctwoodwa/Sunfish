using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Docs.Services;

/// <summary>
/// Three-gate upload validator. PR 3's defense-in-depth surface —
/// every upload through <see cref="AttachmentService"/> calls this
/// before persisting bytes.
///
/// <list type="number">
/// <item>MIME whitelist (per-tenant override or <see cref="DefaultMimeWhitelist.Defaults"/>).</item>
/// <item>Per-attachment size cap (<see cref="Models.BlocksDocsOptions.MaxAttachmentBytes"/>).</item>
/// <item>Per-tenant cumulative quota (<see cref="Models.BlocksDocsOptions.TenantQuotaBytes"/>).</item>
/// </list>
///
/// <para>
/// <b>Council review focus:</b> the boundary between catalog metadata
/// and blob persistence. The repository carries the tenant scope on
/// every row; the policy reads <see cref="Models.BlocksDocsOptions"/>
/// per-tenant; the service must call <see cref="ValidateAsync"/>
/// BEFORE handing bytes to the persister.
/// </para>
/// </summary>
public interface IMimeTypeAndSizePolicy
{
    /// <summary>
    /// Validate an upload against MIME whitelist + size cap + tenant
    /// quota. Returns a <see cref="PolicyResult"/> describing acceptance
    /// or the specific rejection reason.
    /// </summary>
    Task<PolicyResult> ValidateAsync(
        TenantId tenantId,
        string sniffedMime,
        long sizeBytes,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IMimeTypeAndSizePolicy.ValidateAsync"/>.</summary>
public sealed record PolicyResult(bool Rejected, PolicyRejection RejectionReason, string? Detail)
{
    /// <summary>Convenience: true when the upload passes all three gates.</summary>
    public bool IsAccepted => !Rejected;

    /// <summary>Constructor for the accept path.</summary>
    public static PolicyResult Accept() => new(false, PolicyRejection.None, null);

    /// <summary>Constructor for a reject path.</summary>
    public static PolicyResult Reject(PolicyRejection reason, string detail) =>
        new(true, reason, detail);
}

/// <summary>Why a <see cref="IMimeTypeAndSizePolicy.ValidateAsync"/> call rejected the upload.</summary>
public enum PolicyRejection
{
    /// <summary>No rejection — accepted.</summary>
    None,

    /// <summary>The sniffed MIME isn't in the tenant's whitelist.</summary>
    Mime,

    /// <summary>The payload exceeds the per-attachment cap.</summary>
    Size,

    /// <summary>The tenant's cumulative quota would be exceeded by this upload.</summary>
    TenantQuota,
}
