using System.Collections.Concurrent;
using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// In-memory store for <see cref="WorkOrder"/> + <see cref="RepairTicket"/>
/// entities. Tenant-scoped reads enforce <see cref="WorkOrder.TenantId"/>
/// match — a read with a wrong tenant returns null, never the
/// cross-tenant row (hand-off H5 invariant).
/// </summary>
public sealed class InMemoryWorkOrderRepository
{
    private readonly ConcurrentDictionary<WorkOrderId, WorkOrder> _workOrders = new();
    private readonly ConcurrentDictionary<RepairTicketId, RepairTicket> _repairTickets = new();

    /// <summary>Insert or replace a <see cref="WorkOrder"/>.</summary>
    public void Upsert(WorkOrder workOrder)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        _workOrders[workOrder.Id] = workOrder;
    }

    /// <summary>
    /// Fetch by id + tenant. Returns null when missing or when the
    /// stored row's tenant does not match (security gate per H5).
    /// </summary>
    public WorkOrder? GetById(TenantId tenantId, WorkOrderId id)
    {
        if (!_workOrders.TryGetValue(id, out var wo)) return null;
        if (!wo.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)) return null;
        return wo;
    }

    /// <summary>Snapshot all non-deleted work-orders in a tenant. Used by tests + the kitchen-sink demo.</summary>
    public IReadOnlyList<WorkOrder> ListByTenant(TenantId tenantId)
        => _workOrders.Values
            .Where(wo => wo.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
                        && wo.DeletedAt is null)
            .ToList();

    /// <summary>
    /// Look up the work order created from a given deficiency, or
    /// null when no such work order exists. Powers the
    /// DeficiencyRaisedHandler idempotency check.
    /// </summary>
    public WorkOrder? FindByDeficiencyId(TenantId tenantId, Guid deficiencyId)
        => _workOrders.Values.FirstOrDefault(wo =>
               wo.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
               && wo.DeficiencyId == deficiencyId
               && wo.DeletedAt is null);

    /// <summary>Insert or replace a <see cref="RepairTicket"/>.</summary>
    public void UpsertTicket(RepairTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        _repairTickets[ticket.Id] = ticket;
    }
}
