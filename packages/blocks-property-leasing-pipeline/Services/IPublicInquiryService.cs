using System.Net;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Services;

/// <summary>
/// Boundary contract that the Bridge route family (per ADR 0059) calls
/// after the 5-layer inbound defense passes. The route maps its local
/// <c>PublicInquiryFormPost</c> primitive to <see cref="PublicInquiryRequest"/>
/// at the controller layer (per ADR 0059 amendment A1) and invokes
/// <see cref="SubmitInquiryAsync"/> with an
/// <see cref="AnonymousCapability"/>.
/// </summary>
/// <remarks>
/// Phase 1 ships the contract surface — this unblocks W#28 Phase 5
/// (Bridge route family + 5-layer defense). Phase 2 wires the
/// in-memory implementation that creates an <see cref="Inquiry"/> in
/// <see cref="InquiryStatus.Submitted"/> + dispatches the email-verification
/// flow.
/// </remarks>
public interface IPublicInquiryService
{
    /// <summary>
    /// Accepts a verified-defense inquiry submission from the Bridge
    /// route. Returns the persisted <see cref="Inquiry"/>; status is
    /// <see cref="InquiryStatus.Submitted"/> until the prospect proves
    /// the email via the verification flow.
    /// </summary>
    /// <param name="request">Domain-shape inquiry mapped from the route's form-post primitive.</param>
    /// <param name="capability">Anonymous capability minted by the listing surface (per ADR 0043 addendum).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Inquiry> SubmitInquiryAsync(
        PublicInquiryRequest request,
        AnonymousCapability capability,
        CancellationToken ct);
}

/// <summary>
/// Domain-shape representation of an inquiry-form submission. The
/// route-local <c>PublicInquiryFormPost</c> primitive (defined in
/// <c>accelerators/bridge</c>) is mapped to this shape at the
/// controller boundary; the route-local primitive never crosses the
/// block boundary (ADR 0059 amendment A1).
/// </summary>
public sealed record PublicInquiryRequest
{
    /// <summary>Owning tenant (resolved from the listing slug).</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>The listing being inquired about.</summary>
    public required PublicListingId Listing { get; init; }

    /// <summary>Prospect's name as entered on the form.</summary>
    public required string ProspectName { get; init; }

    /// <summary>Prospect's email — drives the verification flow.</summary>
    public required string ProspectEmail { get; init; }

    /// <summary>Optional phone number for SMS callbacks.</summary>
    public string? ProspectPhone { get; init; }

    /// <summary>Free-text message body.</summary>
    public required string MessageBody { get; init; }

    /// <summary>Source IP captured by the Bridge route.</summary>
    public required IPAddress ClientIp { get; init; }

    /// <summary>User-agent header captured by the Bridge route.</summary>
    public required string UserAgent { get; init; }
}

/// <summary>
/// Bearer-capability minted by the public-listing surface for an
/// Anonymous viewer. Carries listing-scope + tenant-scope but no
/// email-verified caveat (that promotion lands when the prospect proves
/// the email and is upgraded to <see cref="Models.Prospect"/> per ADR
/// 0043 addendum).
/// </summary>
public sealed record AnonymousCapability
{
    /// <summary>The opaque capability token (typically a macaroon per ADR 0032).</summary>
    public required string Token { get; init; }

    /// <summary>UTC timestamp the capability was minted.</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>UTC timestamp of expiry; typically short.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
