using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Audit;

/// <summary>
/// W#22 Phase 6 audit-payload factory per ADR 0057 + ADR 0049. Builds
/// <see cref="AuditPayload"/> bodies for the 12 leasing-pipeline event
/// types. Mirrors the W#19 / W#27 factory pattern.
/// </summary>
/// <remarks>
/// <b>FHA-defense invariant at audit tier:</b> none of the body builders
/// here surface <see cref="DemographicProfile"/> fields. The Application
/// audit captures id + tenant + listing + status + fee + actor — the
/// non-protected-class subset visible to compliance review. A test in
/// <c>AuditEmissionTests</c> (W#22 Phase 6) reflects over the body
/// dictionary keys + asserts no demographic field name leaks.
/// </remarks>
internal static class LeasingPipelineAuditPayloadFactory
{
    /// <summary>Body for <see cref="AuditEventType.InquiryAccepted"/>.</summary>
    public static AuditPayload InquiryAccepted(Inquiry inquiry) =>
        new(new Dictionary<string, object?>
        {
            ["inquiry_id"] = inquiry.Id.Value,
            ["tenant"] = inquiry.Tenant.Value,
            ["listing_id"] = inquiry.Listing.Value,
            ["client_ip"] = inquiry.ClientIp.ToString(),
            ["submitted_at"] = inquiry.SubmittedAt.ToString("O"),
        });

    /// <summary>Body for <see cref="AuditEventType.InquiryRejected"/> — emitted before throwing on validation failure.</summary>
    public static AuditPayload InquiryRejected(PublicInquiryRequest request, InquiryValidationFailure failure, string reason) =>
        new(new Dictionary<string, object?>
        {
            ["tenant"] = request.Tenant.Value,
            ["listing_id"] = request.Listing.Value,
            ["client_ip"] = request.ClientIp.ToString(),
            ["failure_category"] = failure.ToString(),
            ["reason"] = reason,
        });

    /// <summary>Body for <see cref="AuditEventType.ProspectPromoted"/>.</summary>
    public static AuditPayload ProspectPromoted(Prospect prospect) =>
        new(new Dictionary<string, object?>
        {
            ["prospect_id"] = prospect.Id.Value,
            ["tenant"] = prospect.Tenant.Value,
            ["originating_inquiry_id"] = prospect.OriginatingInquiry?.Value,
            ["capability_id"] = prospect.Capability.Id.Value,
            ["capability_expires_at"] = prospect.Capability.ExpiresAt.ToString("O"),
            ["promoted_at"] = prospect.PromotedAt.ToString("O"),
        });

    /// <summary>Body for <see cref="AuditEventType.ApplicantPromoted"/>.</summary>
    public static AuditPayload ApplicantPromoted(Application application, ActorId confirmedBy) =>
        new(new Dictionary<string, object?>
        {
            ["application_id"] = application.Id.Value,
            ["tenant"] = application.Tenant.Value,
            ["prospect_id"] = application.Prospect.Value,
            ["confirmed_by"] = confirmedBy.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.ApplicationSubmitted"/>. NOTE: Demographics intentionally omitted (FHA-defense at audit tier).</summary>
    public static AuditPayload ApplicationSubmitted(Application application) =>
        new(new Dictionary<string, object?>
        {
            ["application_id"] = application.Id.Value,
            ["tenant"] = application.Tenant.Value,
            ["prospect_id"] = application.Prospect.Value,
            ["listing_id"] = application.Listing.Value,
            ["status"] = application.Status.ToString(),
            ["fee_amount"] = application.ApplicationFee.Amount,
            ["fee_currency"] = application.ApplicationFee.Currency.ToString(),
            ["submitted_at"] = application.SubmittedAt.ToString("O"),
        });

    /// <summary>Body for <see cref="AuditEventType.ApplicationAccepted"/>, <see cref="AuditEventType.ApplicationDeclined"/>, or <see cref="AuditEventType.ApplicationWithdrawn"/>.</summary>
    public static AuditPayload ApplicationDecision(Application application, ApplicationStatus newStatus, ActorId actor, string? reason) =>
        new(new Dictionary<string, object?>
        {
            ["application_id"] = application.Id.Value,
            ["tenant"] = application.Tenant.Value,
            ["new_status"] = newStatus.ToString(),
            ["actor"] = actor.Value,
            ["reason"] = reason,
        });

    /// <summary>Body for <see cref="AuditEventType.BackgroundCheckCompleted"/>. Findings shapes are NOT included verbatim — only count + outcome (full findings live in the BackgroundCheckResult record itself; audit reference points back).</summary>
    public static AuditPayload BackgroundCheckCompleted(ApplicationId application, BackgroundCheckResult result) =>
        new(new Dictionary<string, object?>
        {
            ["application_id"] = application.Value,
            ["vendor_ref"] = result.VendorRef,
            ["outcome"] = result.Outcome.ToString(),
            ["finding_count"] = result.Findings.Count,
            ["completed_at"] = result.CompletedAt.ToString("O"),
        });
}
