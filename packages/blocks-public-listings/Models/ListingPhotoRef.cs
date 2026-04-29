namespace Sunfish.Blocks.PublicListings.Models;

/// <summary>
/// A single photo attached to a public listing. Photos are encrypted at
/// rest under per-tenant keys and served via CDN; the substrate stores
/// only the blob reference.
/// </summary>
public sealed record ListingPhotoRef
{
    /// <summary>Stable identifier.</summary>
    public required ListingPhotoRefId Id { get; init; }

    /// <summary>Opaque blob reference resolvable by the host's blob store; the substrate doesn't interpret it.</summary>
    public required string BlobRef { get; init; }

    /// <summary>Display order within the listing (0-based).</summary>
    public required int OrderIndex { get; init; }

    /// <summary>Alt text for accessibility + SEO.</summary>
    public required string AltText { get; init; }

    /// <summary>Minimum capability tier required to see this photo (Phase 2 renderer enforces).</summary>
    public required RedactionTier MinimumTier { get; init; }
}
