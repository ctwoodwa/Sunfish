namespace Sunfish.Blocks.Properties.Models;

/// <summary>
/// Locale-tolerant postal address. First-slice scope: free-form
/// <see cref="PostalCode"/> (no normalization), ISO 3166-1 alpha-2
/// <see cref="CountryCode"/>, optional lat/lng. GeoJSON polygon support
/// for parcel boundaries is deferred (cluster intake OQ-P2).
/// </summary>
public sealed record PostalAddress
{
    /// <summary>First address line (street number + street).</summary>
    public required string Line1 { get; init; }

    /// <summary>Optional second address line (apt/suite/unit).</summary>
    public string? Line2 { get; init; }

    /// <summary>City / locality.</summary>
    public required string City { get; init; }

    /// <summary>
    /// Region / state / province. US: state code (e.g. <c>"CA"</c>);
    /// international: free-form region name. Not normalized in first-slice.
    /// </summary>
    public required string Region { get; init; }

    /// <summary>Postal / ZIP code as written; locale-specific, not normalized.</summary>
    public required string PostalCode { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. <c>"US"</c>, <c>"CA"</c>, <c>"GB"</c>).</summary>
    public required string CountryCode { get; init; }

    /// <summary>Optional latitude in decimal degrees; supports showings + mapping.</summary>
    public double? Latitude { get; init; }

    /// <summary>Optional longitude in decimal degrees; pairs with <see cref="Latitude"/>.</summary>
    public double? Longitude { get; init; }
}
