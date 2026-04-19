using Sunfish.Blocks.Maintenance.Models;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>Payload for creating a new <see cref="WorkOrder"/> directly (without a quote).</summary>
public sealed record CreateWorkOrderRequest
{
    /// <summary>The maintenance request originating this work order.</summary>
    public required MaintenanceRequestId RequestId { get; init; }

    /// <summary>The vendor assigned to perform the work.</summary>
    public required VendorId AssignedVendorId { get; init; }

    /// <summary>The date on which the work is scheduled.</summary>
    public required DateOnly ScheduledDate { get; init; }

    /// <summary>Estimated cost of the work. Must be non-negative. Always <see cref="decimal"/>.</summary>
    public required decimal EstimatedCost { get; init; }

    /// <summary>Optional notes for the vendor or property manager.</summary>
    public string? Notes { get; init; }
}
