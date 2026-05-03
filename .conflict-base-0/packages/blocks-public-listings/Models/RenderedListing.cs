using Sunfish.Foundation.Integrations.Payments;

namespace Sunfish.Blocks.PublicListings.Models;

/// <summary>
/// Tier-redacted projection of a <see cref="PublicListing"/> served to a
/// viewer through <c>IListingRenderer.RenderForTierAsync</c>. Photos +
/// address + financials are filtered per the listing's
/// <see cref="RedactionPolicy"/>; downstream consumers MUST NOT cross-
/// reference the underlying <see cref="PublicListing"/> (the renderer is
/// the chokepoint).
/// </summary>
public sealed record RenderedListing
{
    /// <summary>Listing id.</summary>
    public required PublicListingId Id { get; init; }

    /// <summary>Address rendered at the precision allowed by the viewer's tier (per <see cref="AddressRedactionLevel"/>).</summary>
    public required string DisplayAddress { get; init; }

    /// <summary>Headline (always public).</summary>
    public required string Headline { get; init; }

    /// <summary>Markdown-formatted body (always public; may be sanitized by the renderer).</summary>
    public required string DescriptionMarkdown { get; init; }

    /// <summary>Photos visible at this tier (filtered by <see cref="ListingPhotoRef.MinimumTier"/>).</summary>
    public required IReadOnlyList<ListingPhotoRef> Photos { get; init; }

    /// <summary>Asking rent; null when redaction policy hides financials at this tier or the listing is in Draft.</summary>
    public required Money? AskingRent { get; init; }

    /// <summary>The tier this projection was rendered at; useful for downstream caching keys.</summary>
    public required RedactionTier ServedAtTier { get; init; }
}
