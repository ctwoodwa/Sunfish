using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// Write surface for <see cref="WorkOrder"/> lifecycle. Wraps the
/// entity's mutation methods + emits <c>Work.*</c> events on
/// noteworthy transitions. Tenant-scoped — every read enforces
/// <see cref="WorkOrder.TenantId"/> equality against the caller-
/// supplied tenant.
/// </summary>
public interface IWorkOrderService
{
    /// <summary>Create a new <see cref="WorkOrder"/>; emits <c>Work.WorkOrderCreated</c>.</summary>
    Task<WorkOrder> CreateAsync(
        TenantId tenantId,
        string title,
        WorkOrderKind kind,
        Priority priority,
        Guid createdBy,
        WorkOrderSeverity? severity = null,
        DateTimeOffset? dueBy = null,
        Guid? propertyId = null,
        Guid? unitId = null,
        Guid? assetId = null,
        Guid? deficiencyId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Fetch a <see cref="WorkOrder"/> by id within the supplied tenant. Returns null when missing or tenant-mismatched.</summary>
    Task<WorkOrder?> GetByIdAsync(
        TenantId tenantId,
        WorkOrderId workOrderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign the work order to a party + / or contractor; emits
    /// <c>Work.WorkOrderAssigned</c>.
    /// </summary>
    Task<WorkOrder> AssignAsync(
        TenantId tenantId,
        WorkOrderId workOrderId,
        Guid? assignedToPartyId,
        Guid? contractorId,
        Guid assignedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition the work order to the supplied status. Throws
    /// <see cref="InvalidStatusTransitionException"/> when the
    /// transition violates <see cref="WorkOrderStatusMachine"/>.
    /// Emits <c>Work.WorkOrderCompleted</c> on transitions to
    /// <see cref="WorkOrderStatus.Completed"/>.
    /// </summary>
    Task<WorkOrder> TransitionAsync(
        TenantId tenantId,
        WorkOrderId workOrderId,
        WorkOrderStatus newStatus,
        Guid updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-delete; subsequent transitions are rejected.</summary>
    Task SoftDeleteAsync(
        TenantId tenantId,
        WorkOrderId workOrderId,
        Guid deletedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Create a <see cref="RepairTicket"/> sidecar; tenants + frontline staff invoke this before triage.</summary>
    Task<RepairTicket> CreateRepairTicketAsync(
        TenantId tenantId,
        string title,
        Guid createdBy,
        string? description = null,
        Guid? requestedByPartyId = null,
        Guid? propertyId = null,
        Guid? unitId = null,
        CancellationToken cancellationToken = default);
}
