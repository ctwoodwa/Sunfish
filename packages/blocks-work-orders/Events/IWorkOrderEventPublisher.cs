using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkOrders.Events;

/// <summary>
/// Cross-cluster event-publication seam for work-order lifecycle
/// events per <c>cross-cluster-event-bus-design.md</c> §3.2
/// (<c>Work.*</c> catalog). Same shape as sibling cluster
/// publishers (financial / periods / tax) — local interface here
/// until the canonical <c>foundation-events</c> substrate ships.
/// </summary>
public interface IWorkOrderEventPublisher
{
    /// <summary>
    /// Emit <c>Work.WorkOrderCreated</c>. Idempotency key per
    /// catalog: <c>wo-created:{workOrderId}</c>.
    /// </summary>
    Task PublishWorkOrderCreatedAsync(
        WorkOrderCreatedEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emit <c>Work.WorkOrderAssigned</c>. Idempotency key per
    /// catalog: <c>wo-assigned:{workOrderId}:{occurredAtTicks}</c>
    /// (re-fire safe — reassignments are allowed).
    /// </summary>
    Task PublishWorkOrderAssignedAsync(
        WorkOrderAssignedEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emit <c>Work.WorkOrderCompleted</c>. Idempotency key per
    /// catalog: <c>wo-completed:{workOrderId}</c> (one-shot).
    /// </summary>
    Task PublishWorkOrderCompletedAsync(
        WorkOrderCompletedEvent payload,
        CancellationToken cancellationToken = default);
}

/// <summary>Payload for <c>Work.WorkOrderCreated</c>.</summary>
public sealed record WorkOrderCreatedEvent(
    WorkOrderId WorkOrderId,
    TenantId TenantId,
    string Number,
    WorkOrderKind Kind,
    Priority Priority,
    Guid? PropertyId,
    Guid? UnitId,
    Guid? AssetId,
    Guid? DeficiencyId,
    Guid CreatedBy);

/// <summary>Payload for <c>Work.WorkOrderAssigned</c>.</summary>
public sealed record WorkOrderAssignedEvent(
    WorkOrderId WorkOrderId,
    TenantId TenantId,
    Guid? AssignedToPartyId,
    Guid? ContractorId,
    Guid AssignedBy);

/// <summary>Payload for <c>Work.WorkOrderCompleted</c>.</summary>
public sealed record WorkOrderCompletedEvent(
    WorkOrderId WorkOrderId,
    TenantId TenantId,
    DateTimeOffset CompletedAt,
    Guid CompletedBy);
