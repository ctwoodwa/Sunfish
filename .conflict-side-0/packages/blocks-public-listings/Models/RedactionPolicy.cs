using System.Collections.Immutable;

namespace Sunfish.Blocks.PublicListings.Models;

/// <summary>
/// Per-listing redaction policy. The Phase 2 <c>IListingRenderer</c>
/// (deferred) will use this to project listing data per
/// <see cref="RedactionTier"/>.
/// </summary>
public sealed record RedactionPolicy
{
    /// <summary>Address-precision rule.</summary>
    public required AddressRedactionLevel Address { get; init; }

    /// <summary>Whether prospect-tier viewers see financial details (asking rent, security deposit, etc.). Typically <see langword="false"/>.</summary>
    public required bool IncludeFinancialsForProspect { get; init; }

    /// <summary>Whether applicant-tier viewers see asset inventory (appliances, smart locks, etc.). Typically <see langword="false"/> until lease execution.</summary>
    public required bool IncludeAssetInventoryForApplicant { get; init; }

    /// <summary>Custom field-level tier overrides; key is the field name, value is the minimum tier required to see it.</summary>
    public IReadOnlyDictionary<string, RedactionTier> CustomFieldTiers { get; init; } = ImmutableDictionary<string, RedactionTier>.Empty;
}
