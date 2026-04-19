using Sunfish.Blocks.Maintenance.Models;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Optional filter parameters for <see cref="IMaintenanceService.ListWorkOrdersAsync"/>.
/// All filters are additive (AND). A <see langword="null"/> value means "no filter on that field".
/// </summary>
public sealed record ListWorkOrdersQuery
{
    /// <summary>When set, only work orders for this maintenance request are returned.</summary>
    public MaintenanceRequestId? RequestId { get; init; }

    /// <summary>When set, only work orders assigned to this vendor are returned.</summary>
    public VendorId? VendorId { get; init; }

    /// <summary>When set, only work orders with this status are returned.</summary>
    public WorkOrderStatus? Status { get; init; }

    /// <summary>Shared empty query that applies no filters.</summary>
    public static ListWorkOrdersQuery Empty { get; } = new();
}
