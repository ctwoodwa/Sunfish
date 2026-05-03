using Sunfish.Blocks.Maintenance.Models;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Optional filter parameters for <see cref="IMaintenanceService.ListWorkOrdersAsync"/>.
/// All filters are additive (AND). A <see langword="null"/> value means "no filter on that field".
/// </summary>
/// <remarks>
/// W#19 Phase 5 dropped the <c>RequestId</c> filter — the field no longer
/// lives on <see cref="WorkOrder"/>; W#19 Phase 5.1 will re-introduce
/// source-based filtering via an audit-query API.
/// </remarks>
public sealed record ListWorkOrdersQuery
{
    /// <summary>When set, only work orders assigned to this vendor are returned.</summary>
    public VendorId? VendorId { get; init; }

    /// <summary>When set, only work orders with this status are returned.</summary>
    public WorkOrderStatus? Status { get; init; }

    /// <summary>Shared empty query that applies no filters.</summary>
    public static ListWorkOrdersQuery Empty { get; } = new();
}
