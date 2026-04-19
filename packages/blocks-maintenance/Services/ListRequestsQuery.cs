using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Optional filter parameters for <see cref="IMaintenanceService.ListRequestsAsync"/>.
/// All filters are additive (AND). A <see langword="null"/> value means "no filter on that field".
/// </summary>
public sealed record ListRequestsQuery
{
    /// <summary>When set, only requests for this property are returned.</summary>
    public EntityId? PropertyId { get; init; }

    /// <summary>When set, only requests with this status are returned.</summary>
    public MaintenanceRequestStatus? Status { get; init; }

    /// <summary>When set, only requests with this priority are returned.</summary>
    public MaintenancePriority? Priority { get; init; }

    /// <summary>Shared empty query that applies no filters.</summary>
    public static ListRequestsQuery Empty { get; } = new();
}
