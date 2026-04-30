using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.Maintenance.Audit;

/// <summary>
/// W#18 Phase 7 audit-payload factory per ADR 0058 + ADR 0049. Builds
/// <see cref="AuditPayload"/> bodies for the 7 vendor-onboarding event
/// types. Mirrors W#19 / W#22 / W#27 / W#21 factory conventions.
/// </summary>
/// <remarks>
/// <b>TIN PII discipline:</b> none of these body builders surface
/// <c>W9Document</c> TIN bytes or any other PII. The
/// <see cref="AuditEventType.W9DocumentReceived"/> body carries only
/// the document id + the receiving timestamp. TIN-decryption audit
/// emission is handled separately via the existing
/// <see cref="AuditEventType.BookkeeperAccess"/> /
/// <see cref="AuditEventType.TaxAdvisorAccess"/> path (W#32 substrate).
/// </remarks>
internal static class VendorAuditPayloadFactory
{
    /// <summary>Body for <see cref="AuditEventType.VendorCreated"/>.</summary>
    public static AuditPayload VendorCreated(Vendor vendor) =>
        new(new Dictionary<string, object?>
        {
            ["vendor_id"] = vendor.Id.Value,
            ["display_name"] = vendor.DisplayName,
            ["onboarding_state"] = vendor.OnboardingState.ToString(),
            ["specialty_count"] = vendor.Specialties.Count,
            ["status"] = vendor.Status.ToString(),
        });

    /// <summary>Body for <see cref="AuditEventType.VendorMagicLinkIssued"/>.</summary>
    public static AuditPayload VendorMagicLinkIssued(VendorId vendor, VendorMagicLinkId linkId, ActorId issuedBy, DateTimeOffset expiresAt) =>
        new(new Dictionary<string, object?>
        {
            ["vendor_id"] = vendor.Value,
            ["magic_link_id"] = linkId.Value,
            ["issued_by"] = issuedBy.Value,
            ["expires_at"] = expiresAt.ToString("O"),
        });

    /// <summary>Body for <see cref="AuditEventType.VendorMagicLinkConsumed"/>. Captures consumption fingerprint without PII.</summary>
    public static AuditPayload VendorMagicLinkConsumed(VendorId vendor, VendorMagicLinkId linkId, string consumerIp, string userAgent) =>
        new(new Dictionary<string, object?>
        {
            ["vendor_id"] = vendor.Value,
            ["magic_link_id"] = linkId.Value,
            ["consumer_ip"] = consumerIp,
            ["user_agent"] = userAgent,
        });

    /// <summary>Body for <see cref="AuditEventType.VendorOnboardingStateChanged"/>.</summary>
    public static AuditPayload VendorOnboardingStateChanged(VendorId vendor, VendorOnboardingState previous, VendorOnboardingState next, ActorId actor) =>
        new(new Dictionary<string, object?>
        {
            ["vendor_id"] = vendor.Value,
            ["previous_state"] = previous.ToString(),
            ["new_state"] = next.ToString(),
            ["actor"] = actor.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.W9DocumentReceived"/>. NOTE: TIN bytes are NOT surfaced; document-id pointer only.</summary>
    public static AuditPayload W9DocumentReceived(VendorId vendor, W9DocumentId documentId, DateTimeOffset receivedAt) =>
        new(new Dictionary<string, object?>
        {
            ["vendor_id"] = vendor.Value,
            ["w9_document_id"] = documentId.Value,
            ["received_at"] = receivedAt.ToString("O"),
        });

    /// <summary>Body for <see cref="AuditEventType.W9DocumentVerified"/>.</summary>
    public static AuditPayload W9DocumentVerified(VendorId vendor, W9DocumentId documentId, ActorId verifiedBy) =>
        new(new Dictionary<string, object?>
        {
            ["vendor_id"] = vendor.Value,
            ["w9_document_id"] = documentId.Value,
            ["verified_by"] = verifiedBy.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.VendorActivated"/>.</summary>
    public static AuditPayload VendorActivated(VendorId vendor, ActorId actor) =>
        new(new Dictionary<string, object?>
        {
            ["vendor_id"] = vendor.Value,
            ["actor"] = actor.Value,
        });
}
