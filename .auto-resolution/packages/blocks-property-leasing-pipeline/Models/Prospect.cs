using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>
/// Email-verified prospect — capability-tier-1 promotion from Anonymous
/// per ADR 0043 addendum. A <see cref="Prospect"/> can view Prospect-tier
/// redacted listing data (per ADR 0059 redaction policy) and submit an
/// <see cref="Application"/>.
/// </summary>
/// <remarks>
/// The macaroon-backed capability is held by <see cref="Capability"/>
/// (per ADR 0032). Tenant scope, accessible-listings, and the
/// email-verified caveat are all enforced via macaroon caveats.
/// </remarks>
public sealed record Prospect
{
    /// <summary>Unique identifier for this prospect.</summary>
    public required ProspectId Id { get; init; }

    /// <summary>Owning tenant.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>The originating <see cref="Inquiry"/> if applicable; null when promoted directly via criteria-acknowledgement flow.</summary>
    public InquiryId? OriginatingInquiry { get; init; }

    /// <summary>Email address proven via the verification-link flow.</summary>
    public required string VerifiedEmail { get; init; }

    /// <summary>The macaroon-backed capability minted at promotion time.</summary>
    public required ProspectCapability Capability { get; init; }

    /// <summary>UTC timestamp of promotion.</summary>
    public required DateTimeOffset PromotedAt { get; init; }
}
