namespace Sunfish.Blocks.PublicListings.Capabilities;

/// <summary>
/// Caveat names written by <see cref="MacaroonCapabilityPromoter"/> and
/// consumed by <see cref="MacaroonProspectCapabilityVerifier"/>. Centralized
/// to eliminate the issuer/verifier-string-drift risk per the W#28 Phase 5c-4
/// addendum's "Caveat-name centralization" recommendation.
/// </summary>
internal static class ProspectCaveatNames
{
    public const string CapabilityId = "capability-id";
    public const string Tenant = "tenant";
    public const string Email = "email";
    public const string EmailVerified = "email-verified";
    public const string IssuedFromIp = "issued-from-ip";
    public const string Expires = "expires";
    public const string ListingAllowed = "listing-allowed";
}
