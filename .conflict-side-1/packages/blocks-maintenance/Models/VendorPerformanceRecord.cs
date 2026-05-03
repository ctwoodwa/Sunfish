using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// One entry in the append-only vendor-performance event log per
/// ADR 0058 §"Initial contract surface". Sourced from work-order
/// lifecycle events (<see cref="WorkOrder"/> completion / no-show /
/// late) + operator manual adjustments.
/// </summary>
public sealed record VendorPerformanceRecord
{
    /// <summary>Unique identifier.</summary>
    public required VendorPerformanceRecordId Id { get; init; }

    /// <summary>The vendor this entry pertains to.</summary>
    public required VendorId Vendor { get; init; }

    /// <summary>Categorical event type.</summary>
    public required VendorPerformanceEvent Event { get; init; }

    /// <summary>UTC timestamp the event occurred.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Actor who recorded the event (operator or system).</summary>
    public required ActorId RecordedBy { get; init; }

    /// <summary>Optional reference to the related work order (null for non-job events like InsuranceLapse).</summary>
    public WorkOrderId? RelatedWorkOrder { get; init; }

    /// <summary>Optional free-text note for context.</summary>
    public string? Notes { get; init; }
}
