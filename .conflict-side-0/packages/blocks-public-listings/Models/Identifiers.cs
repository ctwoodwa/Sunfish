namespace Sunfish.Blocks.PublicListings.Models;

/// <summary>Stable identifier for a <see cref="PublicListing"/>.</summary>
/// <param name="Value">Underlying GUID.</param>
public readonly record struct PublicListingId(Guid Value)
{
    /// <summary>Mints a fresh GUID-backed id.</summary>
    public static PublicListingId NewId() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}

/// <summary>Stable identifier for a <see cref="ListingPhotoRef"/>.</summary>
/// <param name="Value">Underlying GUID.</param>
public readonly record struct ListingPhotoRefId(Guid Value)
{
    /// <summary>Mints a fresh GUID-backed id.</summary>
    public static ListingPhotoRefId NewId() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
