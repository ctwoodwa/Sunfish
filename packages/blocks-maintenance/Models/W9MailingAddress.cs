namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Address as printed on an IRS W-9 form (W#18 Phase 4 / ADR 0058).
/// Kept block-local so <c>blocks-maintenance</c> does not couple to
/// <c>blocks-properties</c>'s tenant-property <c>PostalAddress</c>;
/// the two have different validation profiles (W-9 accepts foreign
/// addresses; tenant-property addresses are jurisdiction-scoped).
/// </summary>
/// <param name="Line1">Street line 1 (number + street).</param>
/// <param name="Line2">Optional street line 2 (suite / apt / floor).</param>
/// <param name="City">City.</param>
/// <param name="StateOrProvince">USPS state code or international subdivision.</param>
/// <param name="PostalCode">ZIP / postal code.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code (defaults to <c>"US"</c>).</param>
public sealed record W9MailingAddress(
    string Line1,
    string? Line2,
    string City,
    string StateOrProvince,
    string PostalCode,
    string Country = "US");
