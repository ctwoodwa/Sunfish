using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Blocks.PublicListings.Defense;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.PublicListings.Audit;

/// <summary>
/// W#28 Phase 6 audit-payload factory per ADR 0059 + ADR 0049. Builds
/// <see cref="AuditPayload"/> bodies for the 6 public-listing event
/// types. Mirrors W#22 / W#19 / W#27 / W#21 / W#18 conventions.
/// </summary>
internal static class PublicListingAuditPayloadFactory
{
    /// <summary>Body for <see cref="AuditEventType.PublicListingPublished"/>.</summary>
    public static AuditPayload PublicListingPublished(PublicListing listing) =>
        new(new Dictionary<string, object?>
        {
            ["listing_id"] = listing.Id.Value,
            ["tenant"] = listing.Tenant.Value,
            ["slug"] = listing.Slug,
            ["headline"] = listing.Headline,
        });

    /// <summary>Body for <see cref="AuditEventType.PublicListingUnlisted"/>.</summary>
    public static AuditPayload PublicListingUnlisted(PublicListing listing) =>
        new(new Dictionary<string, object?>
        {
            ["listing_id"] = listing.Id.Value,
            ["tenant"] = listing.Tenant.Value,
            ["slug"] = listing.Slug,
        });

    /// <summary>Body for <see cref="AuditEventType.InquirySubmitted"/>. Captured at the inquiry-form boundary after the 5-layer defense passes.</summary>
    public static AuditPayload InquirySubmitted(TenantId tenant, PublicListingId listing, string clientIp) =>
        new(new Dictionary<string, object?>
        {
            ["tenant"] = tenant.Value,
            ["listing_id"] = listing.Value,
            ["client_ip"] = clientIp,
        });

    /// <summary>Body for <see cref="AuditEventType.InquiryRejected"/>. Carries which defense layer rejected + the categorical reason.</summary>
    public static AuditPayload InquiryRejected(TenantId tenant, PublicListingId listing, InquiryDefenseLayer rejectedAt, string reason) =>
        new(new Dictionary<string, object?>
        {
            ["tenant"] = tenant.Value,
            ["listing_id"] = listing.Value,
            ["rejected_at_layer"] = rejectedAt.ToString(),
            ["reason"] = reason,
        });

    /// <summary>Body for <see cref="AuditEventType.CapabilityPromotedToProspect"/>.</summary>
    public static AuditPayload CapabilityPromotedToProspect(TenantId tenant, ProspectCapability capability, string verifiedEmail) =>
        new(new Dictionary<string, object?>
        {
            ["tenant"] = tenant.Value,
            ["capability_id"] = capability.Id.Value,
            ["accessible_listing_count"] = capability.AccessibleListings.Count,
            ["issued_at"] = capability.IssuedAt.ToString("O"),
            ["expires_at"] = capability.ExpiresAt.ToString("O"),
            // Verified email captured for audit; production filters/deletes on retention schedule.
            ["verified_email"] = verifiedEmail,
        });

    /// <summary>Body for <see cref="AuditEventType.CapabilityPromotedToApplicant"/>.</summary>
    public static AuditPayload CapabilityPromotedToApplicant(TenantId tenant, string applicationId, string actor) =>
        new(new Dictionary<string, object?>
        {
            ["tenant"] = tenant.Value,
            ["application_id"] = applicationId,
            ["actor"] = actor,
        });
}
