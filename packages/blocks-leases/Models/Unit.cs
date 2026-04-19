using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// A rentable unit (apartment, house, commercial space, etc.).
/// Property hierarchy and amenities list are deferred to follow-up work.
/// </summary>
public sealed record Unit
{
    /// <summary>Canonical entity identifier for this unit.</summary>
    public required EntityId Id { get; init; }

    /// <summary>Human-readable address or unit description.</summary>
    public required string Address { get; init; }

    /// <summary>Number of bedrooms, if applicable.</summary>
    public int? BedroomCount { get; init; }

    /// <summary>Base asking rent for the unit, if set.</summary>
    public decimal? BaseRent { get; init; }
}
