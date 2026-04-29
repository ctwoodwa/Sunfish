using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Payload for creating a new <see cref="WorkOrder"/> directly (without a quote).
/// </summary>
/// <remarks>
/// Per W#19 Phase 5 (api-change-shape):
/// <list type="bullet">
/// <item><description><see cref="Tenant"/> is required (per <c>IMustHaveTenant</c>).</description></item>
/// <item><description><see cref="EstimatedCost"/> migrates from <c>decimal</c> to <see cref="Money"/>; pass <see langword="null"/> when not yet estimated.</description></item>
/// <item><description><see cref="RequestId"/> stays here as the source signal; the resulting <see cref="WorkOrder"/> drops <c>RequestId</c> per ADR 0053 A6 — the originating request rides in the first <c>WorkOrderCreated</c> audit record's payload (<c>source_kind</c> + <c>source_id</c>).</description></item>
/// </list>
/// </remarks>
public sealed record CreateWorkOrderRequest
{
    /// <summary>Owning tenant (per <c>IMustHaveTenant</c>).</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>The maintenance request originating this work order. Captured in the first <c>WorkOrderCreated</c> audit record; not persisted on the <see cref="WorkOrder"/> entity.</summary>
    public required MaintenanceRequestId RequestId { get; init; }

    /// <summary>The vendor assigned to perform the work.</summary>
    public required VendorId AssignedVendorId { get; init; }

    /// <summary>The date on which the work is scheduled.</summary>
    public required DateOnly ScheduledDate { get; init; }

    /// <summary>Estimated cost of the work; <see langword="null"/> when not yet estimated.</summary>
    public Money? EstimatedCost { get; init; }

    /// <summary>Optional notes for the vendor or property manager.</summary>
    public string? Notes { get; init; }
}
