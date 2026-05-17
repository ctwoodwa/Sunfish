using Sunfish.Blocks.WorkOrders.Events;
using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// Default <see cref="IWorkOrderService"/> backed by
/// <see cref="InMemoryWorkOrderRepository"/>. Production hosts wire
/// a SQLite-backed repository in a follow-on persistence hand-off;
/// the service shape stays the same.
/// </summary>
public sealed class InMemoryWorkOrderService : IWorkOrderService
{
    private readonly InMemoryWorkOrderRepository _repo;
    private readonly IWorkOrderEventPublisher _events;

    public InMemoryWorkOrderService(
        InMemoryWorkOrderRepository repo,
        IWorkOrderEventPublisher events)
    {
        _repo   = repo   ?? throw new ArgumentNullException(nameof(repo));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public async Task<WorkOrder> CreateAsync(
        TenantId tenantId, string title, WorkOrderKind kind, Priority priority, Guid createdBy,
        WorkOrderSeverity? severity = null,
        DateTimeOffset? dueBy = null,
        Guid? propertyId = null, Guid? unitId = null, Guid? assetId = null, Guid? deficiencyId = null,
        CancellationToken cancellationToken = default)
    {
        var wo = WorkOrder.Create(tenantId, title, kind, priority, createdBy, severity, dueBy);
        // Attach cross-cluster anchors so downstream queries (e.g., the
        // DeficiencyRaised handler's idempotency lookup) can find the
        // WO by deficiencyId / propertyId / etc.
        wo.AttachAnchors(
            propertyId:   propertyId,
            unitId:       unitId,
            assetId:      assetId,
            deficiencyId: deficiencyId);
        _repo.Upsert(wo);
        await _events.PublishWorkOrderCreatedAsync(
            new WorkOrderCreatedEvent(
                WorkOrderId:  wo.Id,
                TenantId:     wo.TenantId,
                Number:       wo.Number,
                Kind:         wo.Kind,
                Priority:     wo.Priority,
                PropertyId:   propertyId,
                UnitId:       unitId,
                AssetId:      assetId,
                DeficiencyId: deficiencyId,
                CreatedBy:    createdBy),
            cancellationToken).ConfigureAwait(false);
        return wo;
    }

    /// <inheritdoc />
    public Task<WorkOrder?> GetByIdAsync(TenantId tenantId, WorkOrderId workOrderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_repo.GetById(tenantId, workOrderId));

    /// <inheritdoc />
    public async Task<WorkOrder> AssignAsync(
        TenantId tenantId, WorkOrderId workOrderId,
        Guid? assignedToPartyId, Guid? contractorId, Guid assignedBy,
        CancellationToken cancellationToken = default)
    {
        var wo = RequireWorkOrder(tenantId, workOrderId);
        wo.Assign(assignedToPartyId, contractorId, assignedBy);
        _repo.Upsert(wo);
        await _events.PublishWorkOrderAssignedAsync(
            new WorkOrderAssignedEvent(
                WorkOrderId:       wo.Id,
                TenantId:          wo.TenantId,
                AssignedToPartyId: assignedToPartyId,
                ContractorId:      contractorId,
                AssignedBy:        assignedBy),
            cancellationToken).ConfigureAwait(false);
        return wo;
    }

    /// <inheritdoc />
    public async Task<WorkOrder> TransitionAsync(
        TenantId tenantId, WorkOrderId workOrderId,
        WorkOrderStatus newStatus, Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var wo = RequireWorkOrder(tenantId, workOrderId);
        if (wo.DeletedAt is not null)
            throw new InvalidOperationException(
                $"WorkOrder {wo.Id.Value} is soft-deleted; transitions are rejected.");
        wo.Transition(newStatus, updatedBy);
        _repo.Upsert(wo);
        if (newStatus == WorkOrderStatus.Completed)
        {
            await _events.PublishWorkOrderCompletedAsync(
                new WorkOrderCompletedEvent(
                    WorkOrderId: wo.Id,
                    TenantId:    wo.TenantId,
                    CompletedAt: wo.CompletedAt ?? DateTimeOffset.UtcNow,
                    CompletedBy: updatedBy),
                cancellationToken).ConfigureAwait(false);
        }
        return wo;
    }

    /// <inheritdoc />
    public Task SoftDeleteAsync(
        TenantId tenantId, WorkOrderId workOrderId, Guid deletedBy,
        CancellationToken cancellationToken = default)
    {
        var wo = RequireWorkOrder(tenantId, workOrderId);
        wo.SoftDelete(deletedBy);
        _repo.Upsert(wo);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RepairTicket> CreateRepairTicketAsync(
        TenantId tenantId, string title, Guid createdBy,
        string? description = null,
        Guid? requestedByPartyId = null, Guid? propertyId = null, Guid? unitId = null,
        CancellationToken cancellationToken = default)
    {
        var ticket = RepairTicket.Create(
            title:              title,
            createdBy:          createdBy,
            description:        description,
            requestedByPartyId: requestedByPartyId,
            propertyId:         propertyId,
            unitId:             unitId);
        _repo.UpsertTicket(ticket);
        return Task.FromResult(ticket);
    }

    private WorkOrder RequireWorkOrder(TenantId tenantId, WorkOrderId workOrderId)
    {
        var wo = _repo.GetById(tenantId, workOrderId);
        if (wo is null)
            throw new InvalidOperationException(
                $"WorkOrder {workOrderId.Value} not found in tenant {tenantId.Value} "
                + "(missing, soft-deleted, or cross-tenant).");
        return wo;
    }
}
