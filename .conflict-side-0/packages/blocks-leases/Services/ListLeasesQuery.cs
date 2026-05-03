using Sunfish.Blocks.Leases.Models;

namespace Sunfish.Blocks.Leases.Services;

/// <summary>
/// Optional filter parameters for <see cref="ILeaseService.ListAsync"/>.
/// All filters are additive (AND). A <see langword="null"/> value means "no filter on that field".
/// </summary>
public sealed record ListLeasesQuery
{
    /// <summary>
    /// When set, only leases in this phase are returned.
    /// </summary>
    public LeasePhase? Phase { get; init; }

    /// <summary>
    /// When set, only leases that include this tenant party are returned.
    /// </summary>
    public PartyId? TenantId { get; init; }

    /// <summary>
    /// Shared empty query that applies no filters.
    /// </summary>
    public static ListLeasesQuery Empty { get; } = new();
}
