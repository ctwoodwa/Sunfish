using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// A formal instruction to a vendor to perform work associated with a <see cref="MaintenanceRequest"/>.
/// </summary>
/// <param name="Id">Unique identifier for this work order.</param>
/// <param name="RequestId">The maintenance request that originated this work order.</param>
/// <param name="AssignedVendorId">The vendor assigned to perform the work.</param>
/// <param name="Status">Current lifecycle status of this work order.</param>
/// <param name="ScheduledDate">The date on which the work is scheduled to be performed.</param>
/// <param name="CompletedDate">The date the work was completed, or <see langword="null"/> if not yet complete.</param>
/// <param name="EstimatedCost">Estimated cost of the work in the property currency. Always <see cref="decimal"/>.</param>
/// <param name="ActualCost">Actual cost once work is completed, or <see langword="null"/> if not yet known.</param>
/// <param name="Notes">Free-form notes from the vendor or property manager.</param>
/// <param name="CreatedAtUtc">The instant this record was first persisted.</param>
public sealed record WorkOrder(
    WorkOrderId Id,
    MaintenanceRequestId RequestId,
    VendorId AssignedVendorId,
    WorkOrderStatus Status,
    DateOnly ScheduledDate,
    DateOnly? CompletedDate,
    decimal EstimatedCost,
    decimal? ActualCost,
    string? Notes,
    Instant CreatedAtUtc);
