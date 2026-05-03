using Sunfish.Blocks.Leases.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Leases.Services;

/// <summary>
/// Payload for creating a new <see cref="Lease"/> via <see cref="ILeaseService.CreateAsync"/>.
/// </summary>
public sealed record CreateLeaseRequest
{
    /// <summary>The unit to be covered by the lease.</summary>
    public required EntityId UnitId { get; init; }

    /// <summary>All tenant parties on this lease (at least one required).</summary>
    public required IReadOnlyList<PartyId> Tenants { get; init; }

    /// <summary>The landlord party for this lease.</summary>
    public required PartyId Landlord { get; init; }

    /// <summary>Date the lease term begins.</summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>Date the lease term ends.</summary>
    public required DateOnly EndDate { get; init; }

    /// <summary>Monthly rent amount in the base currency.</summary>
    public required decimal MonthlyRent { get; init; }
}
