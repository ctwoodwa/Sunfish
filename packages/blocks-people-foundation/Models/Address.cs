namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>
/// Postal-address value object embedded inside <see cref="PartyAddress"/>.
/// Flat shape rather than a normalized country/region table because (a)
/// rental tax + invoice flows just need the printed form, and (b) we don't
/// want to ship a global subdivision database as v1 baggage. Country is
/// constrained to ISO 3166-1 alpha-2 (e.g. "US", "MX", "PT") so downstream
/// consumers can look up tax / shipping context unambiguously.
/// </summary>
/// <param name="Line1">Street address line 1; required.</param>
/// <param name="City">City; required.</param>
/// <param name="Region">State, province, or other subdivision; required.</param>
/// <param name="PostalCode">ZIP / postcode; required (some countries' postal codes are alphanumeric, so no format enforcement).</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code (two uppercase letters); required.</param>
/// <param name="Line2">Optional street address line 2 (apt/suite/unit).</param>
public sealed record Address(
    string Line1,
    string City,
    string Region,
    string PostalCode,
    string Country,
    string? Line2 = null);
